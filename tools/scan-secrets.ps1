# Fail if tracked text contains obvious launch-token values.
# Binary screenshots are not inspected; keep logs/screenshots out of commits.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$hits = @()
Get-ChildItem $root -Recurse -File -Include *.md,*.json,*.cs,*.ps1,*.txt,*.yml,*.yaml -ErrorAction SilentlyContinue |
    Where-Object { $_.FullName -notmatch '\\(\.git|bin|obj)\\' } |
    ForEach-Object {
        $m = Select-String -Path $_.FullName -Pattern '(?i)([?&]token=)[A-Za-z0-9_\-]{8,}' -AllMatches -ErrorAction SilentlyContinue
        foreach ($x in $m) { $hits += ($x.Path + ':' + $x.LineNumber) }
    }
if ($hits.Count -gt 0) {
    Write-Host 'FAIL token-like values found; rotate and redact:'
    $hits | Select-Object -Unique
    exit 1
}
Write-Host 'PASS no obvious token values in text files'
