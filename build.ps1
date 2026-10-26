# Build LauncherHub.exe with the .NET Framework 4.8 C# 5 compiler.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1
$ErrorActionPreference = 'Stop'
$dir = Split-Path -Parent $MyInvocation.MyCommand.Path
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "csc.exe not found: $csc" }

$src = Get-ChildItem (Join-Path $dir 'src') -Filter *.cs | ForEach-Object { $_.FullName }
$exe = Join-Path $dir 'LauncherHub.exe'
$ico = Join-Path $dir 'app.ico'

$cscArgs = @(
    '/nologo',
    '/noconfig',
    '/target:winexe',
    '/optimize+',
    '/codepage:65001',
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Web.Extensions.dll',
    '/r:System.Management.dll',
    ('/out:"' + $exe + '"')
)
if (Test-Path $ico) { $cscArgs += ('/win32icon:"' + $ico + '"') }
foreach ($s in $src) { $cscArgs += ('"' + $s + '"') }

& $csc $cscArgs
if ($LASTEXITCODE -ne 0) { throw "C# compile failed ($LASTEXITCODE)" }
Write-Host ("Built: " + $exe + " (" + (Get-Item $exe).Length + " bytes)")
