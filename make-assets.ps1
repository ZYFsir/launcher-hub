# Legacy: extract hand-drawn draft assets into assets\draft only.
# Official brand assets live in assets\official and assets root; do NOT overwrite them with this script.
# Run: powershell -NoProfile -ExecutionPolicy Bypass -File make-assets.ps1
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$src  = Join-Path $root 'design\concept.png'
$out  = Join-Path $root 'assets\draft'
$img  = [System.Drawing.Bitmap]::FromFile($src)

function SaveFixed([string]$name, [int]$x, [int]$y, [int]$w, [int]$h, [bool]$transparentWhite) {
  $bmp = New-Object System.Drawing.Bitmap -ArgumentList $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  for ($py = 0; $py -lt $h; $py++) {
    for ($px = 0; $px -lt $w; $px++) {
      $c = $img.GetPixel($x + $px, $y + $py)
      if ($transparentWhite -and $c.R -gt 232 -and $c.G -gt 232 -and $c.B -gt 232) {
        $bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(0, 0, 0, 0))
      } else {
        $bmp.SetPixel($px, $py, [System.Drawing.Color]::FromArgb(255, $c.R, $c.G, $c.B))
      }
    }
  }
  $bmp.Save((Join-Path $out "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  Write-Host ("{0,-12} {1}x{2} at {3},{4}" -f $name, $w, $h, $x, $y)
}

# draft wordmarks (not used by the runtime; official assets are in assets\official)
SaveFixed 'llama_word'  65 506 262 65 $false
SaveFixed 'comfy_word' 185 433 139 53 $false
SaveFixed 'dsh_word'   116 597 213 41 $false
# square marks
SaveFixed 'comfy_mark' 381 432  56 55 $false
SaveFixed 'llama_mark' 381 515  53 51 $false
# hand-drawn glyphs with transparent paper background
SaveFixed 'home'       373 324  73 66 $true
SaveFixed 'dsh_mark'   380 578  64 56 $true

$img.Dispose()
Write-Host 'assets written to' $out
