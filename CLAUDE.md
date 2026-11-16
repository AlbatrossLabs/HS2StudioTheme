# Studio Theme - notes for Claude

## Release notes
Every change a user would notice goes into `release-notes.md` as it is made: one line under ADD, CHANGE,
REMOVE or FIX, written for players (what it does for them, not how). `release.bat` shows the file in Notepad,
publishes it as a draft GitHub release, copies it to `docs/releases/<version>.md` and `CHANGELOG.md`, then empties it.

## Releasing
`release.bat` (wrapper for `scripts/release.ps1`): asks for the version (default: `Version` in
`src/StudioThemePlugin.cs`, which it updates along with the csproj), builds (with `-p:SkipDeploy=true`, so the
DLL installed in HS2 is left alone), zips into `releases/` (ignored by git) and uploads a draft release to
AlbatrossLabs/HS2StudioTheme with the zip and its SHA-256. `release.bat -NoBuild` packages the installed DLL.
Token: user variable `GH_StudioTheme` (fine-grained, Contents read/write on that repo); falls back to
GH_TOKEN, GITHUB_TOKEN, then GH_CameraSweep.
Zip layout: `BepInEx/plugins/HS2StudioTheme/` holding the DLL, README.txt (from `packaging/README.txt`),
CHANGES.txt (this version's notes), LICENSE.txt and LICENSE-tabler-icons.txt.

## Branding
Albatross Labs (not Linkira Studios).

## Working copy
The user commits and pushes with GitHub Desktop. Don't rewrite history or force-push.
