# Generates src/PrinterSwitch/Assets/app.ico, app-256.png and the installer images.
# The artwork is drawn from plain shapes here, so it is entirely our own.
# Run with pwsh or powershell on Windows.
param([string]$Out = (Join-Path $PSScriptRoot '..\src\PrinterSwitch\Assets'))

Add-Type -AssemblyName System.Drawing
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $Out | Out-Null


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

    # Printer drawn from plain shapes (own artwork, no icon font involved).
    $white = [System.Drawing.Brushes]::White
    $blue = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 0, 90, 170))
    function RR([float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
        $p = New-Object System.Drawing.Drawing2D.GraphicsPath
        $p.AddArc($x, $y, 2*$r, 2*$r, 180, 90); $p.AddArc($x + $w - 2*$r, $y, 2*$r, 2*$r, 270, 90)
        $p.AddArc($x + $w - 2*$r, $y + $h - 2*$r, 2*$r, 2*$r, 0, 90); $p.AddArc($x, $y + $h - 2*$r, 2*$r, 2*$r, 90, 90)
        $p.CloseFigure(); return $p
    }
    $S = [float]$size
    $g.FillPath($white, (RR (0.32*$S) (0.18*$S) (0.36*$S) (0.22*$S) (0.03*$S)))        # paper in
    $g.FillPath($white, (RR (0.17*$S) (0.36*$S) (0.66*$S) (0.32*$S) (0.07*$S)))        # body
    $g.FillRectangle($blue, 0.27*$S, 0.55*$S, 0.46*$S, 0.05*$S)                        # output slot
    $g.FillPath($white, (RR (0.30*$S) (0.57*$S) (0.40*$S) (0.25*$S) (0.025*$S)))       # paper out
    if ($size -ge 24) {
        $g.FillRectangle($blue, 0.36*$S, 0.65*$S, 0.28*$S, [Math]::Max(1, 0.025*$S))   # text lines
        $g.FillRectangle($blue, 0.36*$S, 0.72*$S, 0.20*$S, [Math]::Max(1, 0.025*$S))
    }

    if ($size -ge 32) {
        # orange badge with a simplified USB symbol: "can also be USB"
        $b = $S * 0.44
        $x = $S - $b - $pad; $y = $S - $b - $pad
        $g.FillEllipse((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 202, 80, 16))), $x, $y, $b, $b)
        $g.DrawEllipse((New-Object System.Drawing.Pen ([System.Drawing.Color]::White), ([float]([Math]::Max(1, $S / 64.0)))), $x, $y, $b, $b)
        $cx = $x + $b / 2; $lw = [float]([Math]::Max(1.5, $b * 0.07))
        $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $lw
        $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
        $top = $y + $b * 0.20; $bot = $y + $b * 0.74
        $g.DrawLine($pen, $cx, $top + $b * 0.08, $cx, $bot)                             # stem
        $tri = [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF $cx, $top),
            (New-Object System.Drawing.PointF ($cx - $b * 0.08), ($top + $b * 0.12)),
            (New-Object System.Drawing.PointF ($cx + $b * 0.08), ($top + $b * 0.12)))
        $g.FillPolygon($white, $tri)                                                     # arrow head
        $g.FillEllipse($white, $cx - $b * 0.07, $bot - $b * 0.05, $b * 0.14, $b * 0.14)  # base dot
        $ly = $y + $b * 0.50; $ry = $y + $b * 0.42
        $g.DrawLines($pen, [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF $cx, ($ly + $b * 0.12)),
            (New-Object System.Drawing.PointF ($cx - $b * 0.17), ($ly)),
            (New-Object System.Drawing.PointF ($cx - $b * 0.17), ($ly - $b * 0.08))))  # left branch
        $g.FillEllipse($white, $cx - $b * 0.23, $ly - $b * 0.17, $b * 0.12, $b * 0.12)
        $g.DrawLines($pen, [System.Drawing.PointF[]]@(
            (New-Object System.Drawing.PointF $cx, ($ry + $b * 0.16)),
            (New-Object System.Drawing.PointF ($cx + $b * 0.17), ($ry + $b * 0.04)),
            (New-Object System.Drawing.PointF ($cx + $b * 0.17), ($ry - $b * 0.04))))  # right branch
        $g.FillRectangle($white, $cx + $b * 0.12, $ry - $b * 0.13, $b * 0.10, $b * 0.10)
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
