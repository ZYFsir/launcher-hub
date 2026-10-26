# Starter script for the DSH example manifest.
# Adapt to your local DSH launcher; do not commit personal paths/tokens.
param(
    [string]$DshDir = $env:DSH_DIR,
    [string]$Script = $env:DSH_LAUNCH_SCRIPT,
    [int]$Port = 3080
)
$ErrorActionPreference = 'Stop'
if (-not $DshDir -or -not (Test-Path $DshDir)) { throw "Set DSH_DIR to your local DSH services directory." }
if (-not $Script) { $Script = Join-Path $DshDir 'dsh-launch.ps1' }
if (-not (Test-Path $Script)) { throw "Set DSH_LAUNCH_SCRIPT or place dsh-launch.ps1 under DSH_DIR." }
& $Script -NoOpen -Port $Port
