@echo off
rem Studio Theme release: check the notes, build, zip and upload a DRAFT release to GitHub.
rem Nothing is public until you press "Publish release" on GitHub. Details: scripts\release.ps1
rem Needs a token in the GH_StudioTheme user variable (once: setx GH_StudioTheme "github_pat_...").
rem   release.bat            build with the .NET SDK (falls back to the DLL installed in HS2)
rem   release.bat -NoBuild   package the DLL installed in HS2
setlocal
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "scripts\release.ps1" %*
if errorlevel 1 (
  echo.
  echo Release stopped - nothing was published.
)
pause
