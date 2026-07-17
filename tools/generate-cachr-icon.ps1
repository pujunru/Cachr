Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)
$frames = [System.Collections.Generic.List[byte[]]]::new()

foreach ($size in $sizes) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.Clear([System.Drawing.Color]::Transparent)

    $inset = [Math]::Max(1, $size * 0.04)
    $radius = $size * 0.23
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $diameter = $radius * 2
    $rect = [System.Drawing.RectangleF]::new($inset, $inset, $size - 2 * $inset, $size - 2 * $inset)
    $path.AddArc($rect.Left, $rect.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Top, $diameter, $diameter, 270, 90)
    $path.AddArc($rect.Right - $diameter, $rect.Bottom - $diameter, $diameter, $diameter, 0, 90)
    $path.AddArc($rect.Left, $rect.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    $graphics.FillPath([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 37, 99, 235)), $path)

    $penWidth = [Math]::Max(1.7, $size * 0.105)
    $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::White, $penWidth)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcInset = $size * 0.27
    $graphics.DrawArc($pen, $arcInset, $arcInset, $size - 2 * $arcInset, $size - 2 * $arcInset, 48, 264)

    $stream = [System.IO.MemoryStream]::new()
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames.Add($stream.ToArray())
    $stream.Dispose(); $pen.Dispose(); $path.Dispose(); $graphics.Dispose(); $bitmap.Dispose()
}

$output = Join-Path $PSScriptRoot "..\src\Cachr\Assets\Cachr.ico"
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($output)) | Out-Null
$file = [System.IO.File]::Create($output)
$writer = [System.IO.BinaryWriter]::new($file)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
$offset = 6 + 16 * $frames.Count
for ($i = 0; $i -lt $frames.Count; $i++) {
    $size = $sizes[$i]
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Dispose(); $file.Dispose()
