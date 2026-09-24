$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$workspace = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$iconDirectory = Join-Path $workspace 'src/NetworkStats.App/Resources/AppIcon'
$preview = Join-Path $workspace 'docs/images/app-icon.png'
$windowsIcon = Join-Path $workspace 'src/NetworkStats.App/Platforms/Windows/app.ico'

# The shared MAUI SVG uses rectangles; render those same shapes for PNG and ICO.
$canvas = [System.Drawing.Bitmap]::new(1024, 1024)
$graphics = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    [xml]$svg = Get-Content -LiteralPath (Join-Path $iconDirectory 'appicon.svg') -Raw
    $viewBox = @($svg.svg.viewBox -split '\s+' | ForEach-Object {
        [single]::Parse($_, [System.Globalization.CultureInfo]::InvariantCulture)
    })
    $graphics.ScaleTransform($canvas.Width / $viewBox[2], $canvas.Height / $viewBox[3])
    $graphics.TranslateTransform(-$viewBox[0], -$viewBox[1])
    foreach ($rect in $svg.svg.rect) {
        $x = [single]$rect.x; $y = [single]$rect.y
        $width = [single]$rect.width; $height = [single]$rect.height
        $brush = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($rect.fill))
        $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $diameter = 2 * [single]$rect.rx
            if ($diameter -gt 0) {
                $path.AddArc($x, $y, $diameter, $diameter, 180, 90)
                $path.AddArc($x + $width - $diameter, $y, $diameter, $diameter, 270, 90)
                $path.AddArc($x + $width - $diameter, $y + $height - $diameter, $diameter, $diameter, 0, 90)
                $path.AddArc($x, $y + $height - $diameter, $diameter, $diameter, 90, 90)
                $path.CloseFigure()
            } else {
                $path.AddRectangle([System.Drawing.RectangleF]::new($x, $y, $width, $height))
            }
            $graphics.FillPath($brush, $path)
        } finally { $path.Dispose(); $brush.Dispose() }
    }
    $canvas.Save($preview, [System.Drawing.Imaging.ImageFormat]::Png)
    $sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
    $frames = foreach ($size in $sizes) {
        $bitmap = [System.Drawing.Bitmap]::new($size, $size)
        $resizer = [System.Drawing.Graphics]::FromImage($bitmap)
        $stream = [System.IO.MemoryStream]::new()
        $attributes = [System.Drawing.Imaging.ImageAttributes]::new()
        try {
            $resizer.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $resizer.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            # Mirror edge pixels so bicubic resizing does not introduce a transparent border.
            $attributes.SetWrapMode([System.Drawing.Drawing2D.WrapMode]::TileFlipXY)
            $resizer.DrawImage($canvas, [System.Drawing.Rectangle]::new(0, 0, $size, $size),
                0, 0, $canvas.Width, $canvas.Height, [System.Drawing.GraphicsUnit]::Pixel, $attributes)
            $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
            ,$stream.ToArray()
        } finally { $attributes.Dispose(); $stream.Dispose(); $resizer.Dispose(); $bitmap.Dispose() }
    }
    $writer = [System.IO.BinaryWriter]::new([System.IO.File]::Create($windowsIcon))
    try {
        $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
        $offset = 6 + 16 * $sizes.Count
        for ($index = 0; $index -lt $sizes.Count; $index++) {
            $dimension = [byte]($sizes[$index] % 256)
            $writer.Write($dimension); $writer.Write($dimension)
            $writer.Write([byte]0); $writer.Write([byte]0)
            $writer.Write([uint16]1); $writer.Write([uint16]32)
            $writer.Write([uint32]$frames[$index].Length); $writer.Write([uint32]$offset)
            $offset += $frames[$index].Length
        }
        foreach ($frame in $frames) { $writer.Write([byte[]]$frame) }
    } finally { $writer.Dispose() }
} finally { $graphics.Dispose(); $canvas.Dispose() }
Write-Output "Updated $windowsIcon and $preview from the shared SVG."
