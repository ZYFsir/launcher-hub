# Validate apps.example manifests and placeholder icons without external packages.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$fail = 0

Get-ChildItem (Join-Path $root 'apps.example') -Directory | ForEach-Object {
    $path = Join-Path $_.FullName 'app.json'
    if (-not (Test-Path $path)) { Write-Host "FAIL missing manifest: $path"; $script:fail++; return }
    try { $app = Get-Content $path -Raw -Encoding UTF8 | ConvertFrom-Json }
    catch { Write-Host "FAIL invalid JSON: $path"; $script:fail++; return }

    foreach ($f in 'id','name','kind','launch','health','icon') {
        if ($null -eq $app.$f) { Write-Host "FAIL $($_.Name): missing $f"; $script:fail++; return }
    }
    if ($app.id -notmatch '^[a-z][a-z0-9-]{1,31}$') { Write-Host "FAIL $($app.id): invalid id"; $script:fail++ }
    if ($app.icon.source -eq 'placeholder') {
        if ($app.icon.license -notin @('CC0-1.0','CC BY-SA 4.0')) { Write-Host "FAIL $($app.id): placeholder must be CC0-1.0 or CC BY-SA 4.0"; $script:fail++ }
        $tile = Join-Path $root $app.icon.tile
        if (-not (Test-Path $tile)) { Write-Host "FAIL $($app.id): missing tile $tile"; $script:fail++ }
        else {
            $img = [System.Drawing.Image]::FromFile($tile)
            if ($img.Width -ne 1024 -or $img.Height -ne 1024) { Write-Host "FAIL $($app.id): tile must be 1024x1024"; $script:fail++ }
            $img.Dispose()
        }
    }
    if ($app.launch -and $app.launch.command -and ($app.launch.command -match '[/\\]')) {
        $cmd = Join-Path $root $app.launch.command
        if (-not (Test-Path $cmd)) { Write-Host "FAIL $($app.id): missing launch command $cmd"; $script:fail++ }
    }
    if ($app.icon.source -eq 'official') {
        if ($app.icon.tile -notmatch 'assets[\\/]official[\\/]') { Write-Host "FAIL $($app.id): official asset must live under assets/official"; $script:fail++ }
    }
    Write-Host "PASS $($app.id) $($app.name)"
}
if ($fail -gt 0) { Write-Host "FAIL $fail manifest issue(s)"; exit 1 }
Write-Host 'PASS all app manifests'
