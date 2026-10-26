# Build app.ico for LauncherHub (PNG-compressed multi-size ICO).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$out = Join-Path (Split-Path -Parent $MyInvocation.MyCommand.Path) 'app.ico'
$sizes = @(256, 64, 48, 32, 16)
$pngs = @()

function New-RoundedPath([System.Drawing.RectangleF]$r, [float]$rad) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $rad * 2
    $p.AddArc($r.X, $r.Y, $d, $d, 180, 90)
    $p.AddArc($r.Right - $d, $r.Y, $d, $d, 270, 90)
    $p.AddArc($r.Right - $d, $r.Bottom - $d, $d, $d, 0, 90)
    $p.AddArc($r.X, $r.Bottom - $d, $d, $d, 90, 90)
    $p.CloseFigure()
    return $p
}

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $s / 256.0
    $bgRect = New-Object System.Drawing.RectangleF(0, 0, $s, $s)
    $bgPath = New-RoundedPath $bgRect ([float](56 * $scale))
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($bgRect,
        [System.Drawing.Color]::FromArgb(255, 31, 35, 46),
        [System.Drawing.Color]::FromArgb(255, 13, 15, 20), 90.0)
    $g.FillPath($brush, $bgPath)
    $brush.Dispose()

    $pad = 30 * $scale
    $gap = 10 * $scale
    $cell = ($s - 2 * $pad - $gap) / 2.0
    $rad = [float](18 * $scale)
    $colors = @(
        [System.Drawing.Color]::FromArgb(255, 249, 115, 22),
        [System.Drawing.Color]::FromArgb(255, 215, 226, 43),
        [System.Drawing.Color]::FromArgb(255, 77, 107, 254),
        [System.Drawing.Color]::FromArgb(255, 232, 237, 245)
    )
    for ($i = 0; $i -lt 4; $i++) {
        $cx = $pad + ($i % 2) * ($cell + $gap)
        $cy = $pad + [math]::Floor($i / 2) * ($cell + $gap)
        $cr = New-Object System.Drawing.RectangleF($cx, $cy, $cell, $cell)
        $cp = New-RoundedPath $cr $rad
        $cb = New-Object System.Drawing.SolidBrush($colors[$i])
        $g.FillPath($cb, $cp)
        $cb.Dispose()
        $cp.Dispose()
    }
    $bgPath.Dispose()
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += ,$ms.ToArray()
    $ms.Dispose()
}

$count = $pngs.Count
$header = New-Object byte[] 6
$header[2] = 1
$header[4] = [byte]$count
$entries = New-Object byte[] (16 * $count)
$offset = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $s = $sizes[$i]
    $o = $i * 16
    $entries[$o] = if ($s -ge 256) { 0 } else { [byte]$s }
    $entries[$o + 1] = if ($s -ge 256) { 0 } else { [byte]$s }
    $entries[$o + 2] = 0
    $entries[$o + 3] = 0
    $entries[$o + 4] = 1
    $entries[$o + 5] = 0
    $entries[$o + 6] = 32
    $entries[$o + 7] = 0
    [BitConverter]::GetBytes([int]$pngs[$i].Length).CopyTo($entries, $o + 8)
    [BitConverter]::GetBytes([int]$offset).CopyTo($entries, $o + 12)
    $offset += $pngs[$i].Length
}

$fs = [System.IO.File]::Create($out)
$fs.Write($header, 0, $header.Length)
$fs.Write($entries, 0, $entries.Length)
foreach ($png in $pngs) { $fs.Write($png, 0, $png.Length) }
$fs.Close()
Write-Host ("icon: $out ({0} bytes)" -f (Get-Item $out).Length)
