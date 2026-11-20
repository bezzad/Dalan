# Installs the Dalan agent (AGT-10). Run in PowerShell: irm __DALAN_URL__/install.ps1 | iex
# Puts one file, dalan.exe, in %LOCALAPPDATA%\dalan and adds that folder to your PATH. Needs no admin and no .NET.
$ErrorActionPreference = 'Stop'
if (-not [Environment]::Is64BitOperatingSystem) { throw 'Sorry, the Dalan agent needs 64-bit Windows.' }

$dir = Join-Path $env:LOCALAPPDATA 'dalan'
New-Item -ItemType Directory -Force -Path $dir | Out-Null
$exe = Join-Path $dir 'dalan.exe'
Write-Host 'Downloading dalan-windows-x64.exe'
$ProgressPreference = 'SilentlyContinue'
Invoke-WebRequest -UseBasicParsing -Uri '__DALAN_URL__/downloads/dalan-windows-x64.exe' -OutFile "$exe.new"
Move-Item -Force "$exe.new" $exe
Write-Host "Installed: $exe ($(& $exe version))"

$path = [string][Environment]::GetEnvironmentVariable('Path', 'User')
if (($path -split ';') -notcontains $dir) {
  [Environment]::SetEnvironmentVariable('Path', ($path.TrimEnd(';') + ';' + $dir).TrimStart(';'), 'User')
  $env:Path += ';' + $dir
  Write-Host 'Open a new terminal so the command dalan is found.'
}
Write-Host 'Next: the login command from the My apps page.'
