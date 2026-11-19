[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$projectRoot = Split-Path -Parent $PSScriptRoot
$assetsDirectory = Join-Path $projectRoot "assets"
$pngPath = Join-Path $assetsDirectory "proxy-env-manager-logo.png"
$iconPath = Join-Path $assetsDirectory "proxy-env-manager.ico"
$iconSizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$masterSize = 1024
$designSize = 256.0
$scaleFactor = $masterSize / $designSize

function New-RoundedRectanglePath {
    param(
        [single]$X,
        [single]$Y,
        [single]$Width,
        [single]$Height,
        [single]$Radius
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $diameter = $Radius * 2
    $path.AddArc($X, $Y, $diameter, $diameter, 180, 90)
    $path.AddArc($X + $Width - $diameter, $Y, $diameter, $diameter, 270, 90)
    $path.AddArc($X + $Width - $diameter, $Y + $Height - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($X, $Y + $Height - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Convert-DesignPoint {
    param([single]$Value)
    return [single]($Value * $scaleFactor)
}

function Draw-ArrowMark {
    param(
        [System.Drawing.Graphics]$Graphics,
        [System.Drawing.Color]$Color,
        [single]$OffsetY
    )

    $pen = New-Object System.Drawing.Pen($Color, (Convert-DesignPoint 22))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $brush = New-Object System.Drawing.SolidBrush($Color)

    try {
        $topY = Convert-DesignPoint (95 + $OffsetY)
        $bottomY = Convert-DesignPoint (161 + $OffsetY)
        $Graphics.DrawLine(
            $pen,
            (Convert-DesignPoint 75),
            $topY,
            (Convert-DesignPoint 162),
            $topY)
        $Graphics.DrawLine(
            $pen,
            (Convert-DesignPoint 181),
            $bottomY,
            (Convert-DesignPoint 94),
            $bottomY)

        $topHead = New-Object System.Drawing.PointF[] 3
        $topHead[0] = New-Object System.Drawing.PointF((Convert-DesignPoint 190), (Convert-DesignPoint (95 + $OffsetY)))
        $topHead[1] = New-Object System.Drawing.PointF((Convert-DesignPoint 151), (Convert-DesignPoint (59 + $OffsetY)))
        $topHead[2] = New-Object System.Drawing.PointF((Convert-DesignPoint 151), (Convert-DesignPoint (131 + $OffsetY)))
        $Graphics.FillPolygon($brush, $topHead)

        $bottomHead = New-Object System.Drawing.PointF[] 3
        $bottomHead[0] = New-Object System.Drawing.PointF((Convert-DesignPoint 66), (Convert-DesignPoint (161 + $OffsetY)))
        $bottomHead[1] = New-Object System.Drawing.PointF((Convert-DesignPoint 105), (Convert-DesignPoint (125 + $OffsetY)))
        $bottomHead[2] = New-Object System.Drawing.PointF((Convert-DesignPoint 105), (Convert-DesignPoint (197 + $OffsetY)))
        $Graphics.FillPolygon($brush, $bottomHead)
    }
    finally {
        $brush.Dispose()
        $pen.Dispose()
    }
}

$master = New-Object System.Drawing.Bitmap(
    $masterSize,
    $masterSize,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$graphics = [System.Drawing.Graphics]::FromImage($master)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$graphics.Clear([System.Drawing.Color]::Transparent)

$tileRect = New-Object System.Drawing.RectangleF(
    (Convert-DesignPoint 8),
    (Convert-DesignPoint 8),
    (Convert-DesignPoint 240),
    (Convert-DesignPoint 240))
$tilePath = New-RoundedRectanglePath `
    (Convert-DesignPoint 8) `
    (Convert-DesignPoint 8) `
    (Convert-DesignPoint 240) `
    (Convert-DesignPoint 240) `
    (Convert-DesignPoint 55)
$gradient = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $tileRect,
    [System.Drawing.Color]::FromArgb(59, 136, 239),
    [System.Drawing.Color]::FromArgb(23, 77, 171),
    90.0)
$graphics.FillPath($gradient, $tilePath)

$glossRect = New-Object System.Drawing.RectangleF(
    (Convert-DesignPoint 8),
    (Convert-DesignPoint 8),
    (Convert-DesignPoint 240),
    (Convert-DesignPoint 240))
$gloss = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $glossRect,
    [System.Drawing.Color]::FromArgb(44, 255, 255, 255),
    [System.Drawing.Color]::FromArgb(0, 255, 255, 255),
    90.0)
$graphics.FillPath($gloss, $tilePath)

$borderPen = New-Object System.Drawing.Pen(
    [System.Drawing.Color]::FromArgb(40, 255, 255, 255),
    (Convert-DesignPoint 1.5))
$graphics.DrawPath($borderPen, $tilePath)

Draw-ArrowMark -Graphics $graphics -Color ([System.Drawing.Color]::FromArgb(48, 8, 33, 79)) -OffsetY 2
Draw-ArrowMark -Graphics $graphics -Color ([System.Drawing.Color]::White) -OffsetY 0

$preview = New-Object System.Drawing.Bitmap(
    512,
    512,
    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$previewGraphics = [System.Drawing.Graphics]::FromImage($preview)
$previewGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$previewGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
$previewGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
$previewGraphics.DrawImage(
    $master,
    (New-Object System.Drawing.Rectangle(0, 0, 512, 512)),
    0,
    0,
    $masterSize,
    $masterSize,
    [System.Drawing.GraphicsUnit]::Pixel)
$preview.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

$entries = New-Object System.Collections.Generic.List[object]
foreach ($size in $iconSizes) {
    $bitmap = New-Object System.Drawing.Bitmap(
        $size,
        $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bitmapGraphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $bitmapGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $bitmapGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $bitmapGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $bitmapGraphics.DrawImage(
        $master,
        (New-Object System.Drawing.Rectangle(0, 0, $size, $size)),
        0,
        0,
        $masterSize,
        $masterSize,
        [System.Drawing.GraphicsUnit]::Pixel)

    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $locked = $bitmap.LockBits(
        $rect,
        [System.Drawing.Imaging.ImageLockMode]::ReadOnly,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $sourceStride = [Math]::Abs($locked.Stride)
    $sourcePixels = New-Object byte[] ($sourceStride * $size)
    [System.Runtime.InteropServices.Marshal]::Copy(
        $locked.Scan0,
        $sourcePixels,
        0,
        $sourcePixels.Length)
    $bitmap.UnlockBits($locked)

    $maskStride = [int]([Math]::Floor(($size + 31) / 32) * 4)
    $imageSize = $size * $size * 4 + $maskStride * $size
    $entryStream = New-Object System.IO.MemoryStream
    $writer = New-Object System.IO.BinaryWriter($entryStream)

    $writer.Write([uint32]40)
    $writer.Write([int32]$size)
    $writer.Write([int32]($size * 2))
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]0)
    $writer.Write([uint32]$imageSize)
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)

    $pixelRow = New-Object byte[] ($size * 4)
    for ($y = $size - 1; $y -ge 0; $y--) {
        [System.Buffer]::BlockCopy($sourcePixels, $y * $sourceStride, $pixelRow, 0, $pixelRow.Length)
        $writer.Write($pixelRow)
    }

    $maskRow = New-Object byte[] $maskStride
    for ($y = 0; $y -lt $size; $y++) {
        $writer.Write($maskRow)
    }

    $writer.Flush()
    $entries.Add([pscustomobject]@{
        Size = $size
        Data = $entryStream.ToArray()
    })
    $writer.Dispose()
    $entryStream.Dispose()
    $bitmapGraphics.Dispose()
    $bitmap.Dispose()
}

$iconStream = New-Object System.IO.MemoryStream
$iconWriter = New-Object System.IO.BinaryWriter($iconStream)
$iconWriter.Write([uint16]0)
$iconWriter.Write([uint16]1)
$iconWriter.Write([uint16]$entries.Count)

$dataOffset = 6 + (16 * $entries.Count)
foreach ($entry in $entries) {
    $dimension = [byte]$(if ($entry.Size -ge 256) { 0 } else { $entry.Size })
    $iconWriter.Write($dimension)
    $iconWriter.Write($dimension)
    $iconWriter.Write([byte]0)
    $iconWriter.Write([byte]0)
    $iconWriter.Write([uint16]1)
    $iconWriter.Write([uint16]32)
    $iconWriter.Write([uint32]$entry.Data.Length)
    $iconWriter.Write([uint32]$dataOffset)
    $dataOffset += $entry.Data.Length
}

foreach ($entry in $entries) {
    $iconWriter.Write($entry.Data)
}

$iconWriter.Flush()
[System.IO.File]::WriteAllBytes($iconPath, $iconStream.ToArray())
$iconWriter.Dispose()
$iconStream.Dispose()

$borderPen.Dispose()
$gloss.Dispose()
$gradient.Dispose()
$tilePath.Dispose()
$previewGraphics.Dispose()
$preview.Dispose()
$graphics.Dispose()
$master.Dispose()

Write-Host "Generated: $pngPath"
Write-Host "Generated: $iconPath"
Write-Host ("ICO frames: " + ($iconSizes -join ", "))
