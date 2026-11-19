# 生成 FormaToys 的多尺寸应用图标（.ico，PNG 压缩条目），无需外部素材。
param(
    [string]$Out = "src\FormaToys.App\Assets\app.ico"
)

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [Math]::Max(1, [int]($Size * 0.06))
    $rect = New-Object System.Drawing.Rectangle $pad, $pad, ($Size - 2 * $pad), ($Size - 2 * $pad)
    $radius = [int]($Size * 0.22)

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
    $path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
    $path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $start = [System.Drawing.Color]::FromArgb(255, 24, 128, 96)
    $end = [System.Drawing.Color]::FromArgb(255, 13, 82, 62)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $start, $end, 45.0
    $g.FillPath($brush, $path)

    # 中间一枚向右的箭头，代表“转成另一种格式”。
    $arrow = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cx = $Size / 2.0
    $cy = $Size / 2.0
    $w = $Size * 0.46
    $h = $Size * 0.34
    $shaft = $Size * 0.10

    $left = $cx - $w / 2
    $right = $cx + $w / 2
    $top = $cy - $h / 2
    $bottom = $cy + $h / 2

    $arrow.AddPolygon([System.Drawing.PointF[]]@(
        (New-Object System.Drawing.PointF ($left), ($cy - $shaft / 2)),
        (New-Object System.Drawing.PointF ($right - $h * 0.55), ($cy - $shaft / 2)),
        (New-Object System.Drawing.PointF ($right - $h * 0.55), $top),
        (New-Object System.Drawing.PointF ($right), $cy),
        (New-Object System.Drawing.PointF ($right - $h * 0.55), $bottom),
        (New-Object System.Drawing.PointF ($right - $h * 0.55), ($cy + $shaft / 2)),
        (New-Object System.Drawing.PointF ($left), ($cy + $shaft / 2))
    ))

    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.FillPath($white, $arrow)

    $white.Dispose()
    $brush.Dispose()
    $arrow.Dispose()
    $path.Dispose()
    $g.Dispose()

    return $bmp
}

$pngs = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap -Size $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += , @($size, $ms.ToArray())
    $ms.Dispose()
    $bmp.Dispose()
}

$dir = Split-Path -Parent $Out
if ($dir -and -not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

$fs = [System.IO.File]::Create((Join-Path (Get-Location) $Out))
$bw = New-Object System.IO.BinaryWriter $fs

$bw.Write([UInt16]0)          # reserved
$bw.Write([UInt16]1)          # type = icon
$bw.Write([UInt16]$pngs.Count)

$offset = 6 + 16 * $pngs.Count
foreach ($entry in $pngs) {
    $size = $entry[0]
    $data = $entry[1]
    $bw.Write([Byte]($(if ($size -ge 256) { 0 } else { $size })))
    $bw.Write([Byte]($(if ($size -ge 256) { 0 } else { $size })))
    $bw.Write([Byte]0)        # palette
    $bw.Write([Byte]0)        # reserved
    $bw.Write([UInt16]1)      # planes
    $bw.Write([UInt16]32)     # bpp
    $bw.Write([UInt32]$data.Length)
    $bw.Write([UInt32]$offset)
    $offset += $data.Length
}

foreach ($entry in $pngs) {
    $bw.Write($entry[1])
}

$bw.Flush()
$bw.Dispose()
$fs.Dispose()

"icon written: $Out ($($pngs.Count) sizes)"
