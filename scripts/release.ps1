# Studio Theme release: build, package, and upload a DRAFT release to github.com/AlbatrossLabs/HS2StudioTheme.
# Run through release.bat. Nothing goes public until you press "Publish release" on GitHub.
#
#   Once: make a fine-grained token at https://github.com/settings/personal-access-tokens/new
#         Resource owner: AlbatrossLabs, Repository access: HS2StudioTheme, Permissions: Contents = Read and write
#         (or add HS2StudioTheme to the token Camera Sweep uses), then:
#         setx GH_StudioTheme "github_pat_..."   (and open a new window)
#
#   release.bat            build with the .NET SDK (falls back to the DLL installed in HS2)
#   release.bat -NoBuild   package the DLL installed in HS2 (the one you tested)
#
# Steps: pick the version -> check release-notes.md in Notepad -> build -> zip -> draft release with the zip
# and SHA-256 -> save the notes (docs\releases\<version>.md, CHANGELOG.md) -> empty release-notes.md -> open GitHub.
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path -Parent $PSScriptRoot)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$Repo      = 'AlbatrossLabs/HS2StudioTheme'
$HS2Dir    = 'X:\HS2 RX15'
$Installed = Join-Path $HS2Dir 'BepInEx\plugins\HS2StudioTheme\HS2StudioTheme.dll'
$PluginCs  = 'src\StudioThemePlugin.cs'
$Csproj    = 'HS2StudioTheme.csproj'
$NotesFile = 'release-notes.md'
$Utf8      = New-Object System.Text.UTF8Encoding $false

$NotesTemplate = @'
<!--
Release notes for the next version of Studio Theme, filled in as the code changes.
Optional: a sentence or two up here. Then one line per change under its heading ("- ..."); empty headings are left out.
release.bat opens this in Notepad before publishing, uploads it as a draft GitHub release, saves a copy to
docs\releases\<version>.md, adds it to CHANGELOG.md and empties this file for the next version.
-->

### ADD

### CHANGE

### REMOVE

### FIX
'@

function Say($text, $color = 'Gray') { Write-Host $text -ForegroundColor $color }
function Stop-Release($text) { Say "`n$text" Red; exit 1 }
function ReadText($path) { [IO.File]::ReadAllText((Resolve-Path $path), $Utf8) }
function WriteText($path, $text) { [IO.File]::WriteAllText((Join-Path (Get-Location) $path), $text, $Utf8) }

# ---- token -------------------------------------------------------------------------------------------------
$Token = $env:GH_StudioTheme
if (-not $Token) { $Token = $env:GH_TOKEN }
if (-not $Token) { $Token = $env:GITHUB_TOKEN }
if (-not $Token -and $env:GH_CameraSweep) {
    $Token = $env:GH_CameraSweep
    Say ' Using the Camera Sweep token (GH_CameraSweep): it needs access to HS2StudioTheme too.' Yellow
}
if (-not $Token) {
    Stop-Release @"
No GitHub token. Make one at https://github.com/settings/personal-access-tokens/new
  Resource owner: AlbatrossLabs - Repository access: HS2StudioTheme - Permissions: Contents = Read and write
Then once:  setx GH_StudioTheme "github_pat_..."   and open a new window.
"@
}
$Headers = @{
    Authorization          = "Bearer $Token"
    Accept                 = 'application/vnd.github+json'
    'X-GitHub-Api-Version' = '2022-11-28'
    'User-Agent'           = 'studiotheme-release'
}

function GitHub($method, $path, $body) {
    $uri = if ($path -like 'https://*') { $path } else { "https://api.github.com/repos/$Repo$path" }
    $req = @{ Method = $method; Uri = $uri; Headers = $Headers }
    if ($null -ne $body) {
        $req.Body = $Utf8.GetBytes(($body | ConvertTo-Json -Depth 5))
        $req.ContentType = 'application/json; charset=utf-8'
    }
    try { Invoke-RestMethod @req }
    catch {
        $code = 0
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
        switch ($code) {
            401 { Stop-Release 'GitHub rejected the token (expired or mistyped?).' }
            403 { Stop-Release "GitHub refused: the token needs 'Contents: Read and write' on $Repo." }
            404 { Stop-Release "Can't see $Repo (or that item): the token needs access to this repository." }
            default { Stop-Release "GitHub call failed ($method $path): $($_.Exception.Message)" }
        }
    }
}

# ---- version -----------------------------------------------------------------------------------------------
$source = ReadText $PluginCs
$m = [regex]::Match($source, 'const string Version\s*=\s*"([0-9.]+)"')
if (-not $m.Success) { Stop-Release "Couldn't find Version = ""x.y.z"" in $PluginCs." }
$current = $m.Groups[1].Value

Say "`n Studio Theme release" Cyan
Say " --------------------"
$ver = (Read-Host " Version to release [$current]").Trim().TrimStart('v')
if (-not $ver) { $ver = $current }
if ($ver -notmatch '^\d+\.\d+\.\d+$') { Stop-Release """$ver"" isn't a version like 1.0.0." }
$tag = "v$ver"

# Already published? (A draft for the same version is reused.)
$releases = @(GitHub GET '/releases?per_page=100')
$same = @($releases | Where-Object { $_.tag_name -eq $tag })
$published = $same | Where-Object { -not $_.draft } | Select-Object -First 1
if ($published) { Stop-Release "$tag is already published ($($published.html_url)). Release a new version number." }

if ($ver -ne $current) {
    WriteText $PluginCs ([regex]::Replace($source, '(const string Version\s*=\s*")[0-9.]+(")', "`${1}$ver`${2}"))
    Say " Version set to $ver in $PluginCs." Yellow
}
$proj = ReadText $Csproj
if ($proj -notmatch "<Version>$([regex]::Escape($ver))</Version>") {
    WriteText $Csproj ([regex]::Replace($proj, '<Version>[^<]*</Version>', "<Version>$ver</Version>"))
    Say " Version set to $ver in $Csproj." Yellow
}

# ---- release notes -----------------------------------------------------------------------------------------
$saved = "docs\releases\$ver.md"
if (-not (Test-Path $NotesFile)) { WriteText $NotesFile $NotesTemplate }

function Get-Entries($raw) { ([regex]::Matches(($raw -replace '(?s)<!--.*?-->', ''), '(?m)^\s*[-*]\s+\S')).Count }

# Re-running a version whose notes were already saved (draft not published yet): start from that copy.
if ((Get-Entries (ReadText $NotesFile)) -eq 0 -and (Test-Path $saved)) {
    Copy-Item $saved $NotesFile -Force
    Say " Using the notes saved for $ver earlier ($saved)." Yellow
}

Say "`n Check the release notes, save, then close Notepad to continue..." Cyan
Start-Process notepad.exe -ArgumentList ('"' + (Resolve-Path $NotesFile).Path + '"') -Wait
$notesRaw = ReadText $NotesFile
if ((Get-Entries $notesRaw) -eq 0) { Stop-Release "$NotesFile has no entries (""- ..."") under ADD, CHANGE, REMOVE or FIX. Add what changed in $ver and run again." }

# Publishable notes: no comment, no empty headings.
function Get-PublishNotes($raw) {
    $text = ($raw -replace '(?s)<!--.*?-->', '') -replace "`r`n", "`n"
    $parts = [regex]::Split($text, '(?m)^(?=###\s)')
    $out = @()
    foreach ($p in $parts) {
        $t = $p.Trim()
        if (-not $t) { continue }
        if ($t -match '^###\s' -and ([regex]::Matches($t, '(?m)^\s*[-*]\s+\S')).Count -eq 0) { continue }
        $out += $t
    }
    ($out -join "`n`n").Trim()
}
$notes = Get-PublishNotes $notesRaw

# ---- git state ---------------------------------------------------------------------------------------------
$git = Get-Command git -ErrorAction SilentlyContinue
$gitWarning = $null
if ($git) {
    $ahead = (& git rev-list --count '@{u}..HEAD' 2>$null)
    $dirty = @(& git status --porcelain -- src res HS2StudioTheme.csproj packaging 2>$null)
    if ($ahead -and [int]$ahead -gt 0) { $gitWarning = "$ahead commit(s) not pushed yet." }
    if ($dirty.Count -gt 0) { $gitWarning = (($gitWarning, "Uncommitted source changes (e.g. the version bump).") -join ' ').Trim() }
}

# ---- build -------------------------------------------------------------------------------------------------
# The build reads TextMeshPro and IL.dll from the game folder (HS2Dir), so it needs the game installed;
# SkipDeploy keeps it from overwriting the DLL installed in HS2.
$dll = $null
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $NoBuild -and $dotnet) {
    Say "`n Building Release $ver..." Cyan
    & dotnet build $Csproj -c Release -nologo -v:minimal "-p:Version=$ver" "-p:HS2Dir=$HS2Dir" '-p:SkipDeploy=true'
    if ($LASTEXITCODE -ne 0) { Stop-Release 'Build failed (see above). Run "release.bat -NoBuild" to package the installed DLL instead.' }
    $dll = 'bin\Release\HS2StudioTheme.dll'
} else {
    if (-not $NoBuild) { Say ' .NET SDK not found: packaging the DLL installed in HS2.' Yellow }
    if (-not (Test-Path $Installed)) { Stop-Release "No DLL at $Installed." }
    $dll = $Installed
    # The plugin's version string is in the DLL as UTF-16; make sure it's the one being released.
    $bytes = [IO.File]::ReadAllBytes($Installed)
    $even = [Text.Encoding]::Unicode.GetString($bytes)
    $odd = [Text.Encoding]::Unicode.GetString($bytes, 1, $bytes.Length - 1)
    if (-not ($even.Contains($ver) -or $odd.Contains($ver))) { Stop-Release "The installed DLL isn't version $ver. Install the $ver build first, or build with the .NET SDK." }
}
$dllInfo = Get-Item $dll
Say (" DLL: {0}  ({1})" -f $dllInfo.FullName, $dllInfo.LastWriteTime)

# ---- package -----------------------------------------------------------------------------------------------
foreach ($f in 'packaging\README.txt', 'LICENSE', 'res\LICENSE-tabler-icons.txt') {
    if (-not (Test-Path $f)) { Stop-Release "Missing $f." }
}

New-Item -ItemType Directory -Force releases | Out-Null
$zipName = "HS2StudioTheme-v$ver.zip"
$zip = Join-Path (Resolve-Path releases) $zipName
if (Test-Path $zip) { Remove-Item $zip -Force }

$readme = (ReadText 'packaging\README.txt').Replace('{VERSION}', $ver)
$changes = "Studio Theme v$ver`n`n" + $notes
$crlf = { param($s) ($s -replace "`r`n", "`n") -replace "`n", "`r`n" }

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dir = 'BepInEx/plugins/HS2StudioTheme/'
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    $level = [IO.Compression.CompressionLevel]::Optimal
    [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, (Resolve-Path $dll), $dir + 'HS2StudioTheme.dll', $level) | Out-Null
    foreach ($pair in @(@('README.txt', $readme), @('CHANGES.txt', $changes),
                        @('LICENSE.txt', (ReadText 'LICENSE')), @('LICENSE-tabler-icons.txt', (ReadText 'res\LICENSE-tabler-icons.txt')))) {
        $entry = $archive.CreateEntry($dir + $pair[0], $level)
        $w = New-Object IO.StreamWriter($entry.Open(), $Utf8)
        try { $w.Write((& $crlf $pair[1])) } finally { $w.Dispose() }
    }
} finally { $archive.Dispose() }

$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Say (" Zip: {0}  ({1:N0} bytes)" -f $zip, (Get-Item $zip).Length)
Say " SHA-256: $sha"

# ---- draft release -----------------------------------------------------------------------------------------
$body = $notes + "`n`n### Install`nExtract the zip into your Honey Select 2 folder (the one with HoneySelect2.exe). " +
        "Everything goes to ``BepInEx\plugins\HS2StudioTheme\``, with README.txt next to the DLL. " +
        "Settings: F1 > Studio Theme (turning *Enabled* off puts Studio's original look back straight away).`n`n" +
        "### SHA-256`n| File | SHA-256 |`n| --- | --- |`n| ``$zipName`` | ``$sha`` |`n"
$title = "Studio Theme v$ver"

Say "`n Uploading the draft release $tag..." Cyan
$draft = $same | Where-Object { $_.draft } | Sort-Object id | Select-Object -First 1
if ($draft) {
    $draft = GitHub PATCH "/releases/$($draft.id)" @{ name = $title; body = $body; target_commitish = 'main' }
    foreach ($a in @(GitHub GET "/releases/$($draft.id)/assets")) {
        if ($a.name -eq $zipName) { GitHub DELETE "/releases/assets/$($a.id)" $null | Out-Null }
    }
    Say ' Updated the existing draft.'
} else {
    $draft = GitHub POST '/releases' @{ tag_name = $tag; target_commitish = 'main'; name = $title; body = $body; draft = $true; prerelease = $false }
    Say ' Created the draft.'
}

try {
    Invoke-RestMethod -Method Post -Headers $Headers -ContentType 'application/zip' -InFile $zip `
        -Uri "https://uploads.github.com/repos/$Repo/releases/$($draft.id)/assets?name=$zipName" | Out-Null
} catch {
    Stop-Release "The draft is there but the zip upload failed: $($_.Exception.Message). Run release.bat again to retry."
}
Say " Zip attached." Green

# ---- keep the notes, reset for the next version ------------------------------------------------------------
New-Item -ItemType Directory -Force 'docs\releases' | Out-Null
WriteText $saved $notesRaw
$log = if (Test-Path 'CHANGELOG.md') { ReadText 'CHANGELOG.md' } else { "# Changelog`n" }
$section = "## v$ver`n`n$notes`n"
$existing = [regex]::Match($log, "(?ms)^## v$([regex]::Escape($ver))\b.*?(?=^## |\z)")
if ($existing.Success) { $log = $log.Remove($existing.Index, $existing.Length).Insert($existing.Index, "$section`n") }
else {
    $first = [regex]::Match($log, '(?m)^## ')
    if ($first.Success) { $log = $log.Insert($first.Index, "$section`n") } else { $log = $log.TrimEnd() + "`n`n$section" }
}
WriteText 'CHANGELOG.md' $log
WriteText $NotesFile $NotesTemplate
Say " Notes saved to $saved and CHANGELOG.md; $NotesFile is empty again for the next version."

# ---- done --------------------------------------------------------------------------------------------------
Start-Process "https://github.com/$Repo/releases"
Say "`n Draft $tag is on GitHub. Check it there and press Publish release." Green
Say ' The tag is made from GitHub''s main when you publish, so push first:' Yellow
Say '   commit the version bump, CHANGELOG.md, docs\releases and release-notes.md in GitHub Desktop and push.' Yellow
if ($gitWarning) { Say "   Right now: $gitWarning" Yellow }
exit 0
