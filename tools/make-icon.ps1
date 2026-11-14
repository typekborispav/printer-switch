# Generates src/PrinterSwitch/Assets/app.ico (and installer images) from the
# Segoe Fluent Icons glyphs, so the app icon matches Windows 11 iconography.
# Needs Windows 11 (or Windows 10 with Segoe MDL2 Assets). Run with pwsh or powershell.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\PrinterSwitch\Assets'))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null

$fonts = New-Object System.Drawing.Text.InstalledFontCollection
$glyphFont = if ($fonts.Families.Name -contains 'Segoe Fluent Icons') { 'Segoe Fluent Icons' } else { 'Segoe MDL2 Assets' }

function New-Frame([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(0.5, $size / 32.0)
    $r = $size * 0.22
    $w = $size - 2 * $pad
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($pad, $pad, 2*$r, 2*$r, 180, 90)
    $path.AddArc($pad + $w - 2*$r, $pad, 2*$r, 2*$r, 270, 90)
    $path.AddArc($pad + $w - 2*$r, $pad + $w - 2*$r, 2*$r, 2*$r, 0, 90)
    $path.AddArc($pad, $pad + $w - 2*$r, 2*$r, 2*$r, 90, 90)
    $path.CloseFigure()
    $rect = New-Object System.Drawing.RectangleF 0, 0, $size, $size
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect,
        ([System.Drawing.Color]::FromArgb(255, 0, 120, 212)),
        ([System.Drawing.Color]::FromArgb(255, 0, 76, 153)), 90.0
    $g.FillPath($brush, $path)

    $white = [System.Drawing.Brushes]::White
    $fmt = New-Object System.Drawing.StringFormat
    $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
    $font = New-Object System.Drawing.Font $glyphFont, ([float]($size * 0.56)), ([System.Drawing.GraphicsUnit]::Pixel)
    $g.DrawString([string][char]0xE749, $font, $white, (New-Object System.Drawing.RectangleF 0, ($size * 0.02), $size, $size), $fmt)

    if ($size -ge 32) {
        # small orange USB badge in the corner: "this printer can also be USB"
        $b = $size * 0.42
        $x = $size - $b - $pad; $y = $size - $b - $pad
        $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 202, 80, 16))), $x, $y, $b, $b)
        $g.DrawEllipse((New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float]([Math]::Max(1, $size / 48.0)))), $x, $y, $b, $b)
        $f2 = New-Object System.Drawing.Font $glyphFont, ([float]($b * 0.55)), ([System.Drawing.GraphicsUnit]::Pixel)
        $g.DrawString([string][char]0xE88E, $f2, $white, (New-Object System.Drawing.RectangleF $x, ($y + $b * 0.03), $b, $b), $fmt)
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 20, 24, 32, 40, 48, 64, 128, 256
$pngs = foreach ($s in $sizes) {
    $bmp = New-Frame $s
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256) { $bmp.Save((Join-Path $Out 'app-256.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $bmp.Dispose()
    ,$ms.ToArray()
}

# ICO container with PNG-compressed entries
$ico = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter $ico
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $pngs[$i].Length
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$len); $bw.Write([uint32]$offset)
    $offset += $len
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()
[System.IO.File]::WriteAllBytes((Join-Path $Out 'app.ico'), $ico.ToArray())
Write-Host "written $(Join-Path $Out 'app.ico')"

# Small image in the top right corner of the installer wizard (BMP, white background).
foreach ($px in 55, 110) {
    $frame = New-Frame ([int]($px * 0.9))
    $bmp = New-Object System.Drawing.Bitmap $px, ([int]($px * 58 / 55))
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    $g.DrawImage($frame, [int](($bmp.Width - $frame.Width) / 2), [int](($bmp.Height - $frame.Height) / 2))
    $g.Dispose()
    $name = if ($px -eq 55) { 'wizard-small.bmp' } else { 'wizard-small-200.bmp' }
    $bmp.Save((Join-Path $PSScriptRoot "..\installer\$name"), [System.Drawing.Imaging.ImageFormat]::Bmp)
}
Write-Host "written installer\wizard-small*.bmp"
