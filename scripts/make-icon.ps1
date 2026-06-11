# Generates app.ico for MouseToPad — the same gamepad glyph TrayController draws
# at runtime (green "enabled" variant), rendered at multiple sizes with
# PNG-compressed entries. Re-run if you ever change the glyph.
param([string]$OutPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "app.ico"))

Add-Type -AssemblyName System.Drawing

$sizes = 16, 24, 32, 48, 64, 128, 256
$images = @()

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    # the glyph is authored on a 32x32 grid (see TrayController.CreateIcon)
    $s = $size / 32.0
    $g.ScaleTransform($s, $s)
    $body = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 167, 69))
    $g.FillEllipse($body, 1, 8, 14, 16)      # left grip
    $g.FillEllipse($body, 17, 8, 14, 16)     # right grip
    $g.FillRectangle($body, 8, 8, 16, 14)    # bridge
    $dot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $g.FillEllipse($dot, 6, 13, 5, 5)        # d-pad blob
    $g.FillEllipse($dot, 21, 13, 5, 5)       # face-button blob
    $g.Dispose(); $body.Dispose(); $dot.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $images += , @($size, $ms.ToArray())
    $ms.Dispose()
}

# .ico container: ICONDIR + ICONDIRENTRY table + PNG blobs
$fs = [System.IO.File]::Create($OutPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([UInt16]0)                 # reserved
$bw.Write([UInt16]1)                 # type: icon
$bw.Write([UInt16]$images.Count)
$offset = 6 + 16 * $images.Count
foreach ($entry in $images) {
    $size = $entry[0]; $bytes = $entry[1]
    $dim = if ($size -ge 256) { 0 } else { $size }   # 0 means 256
    $bw.Write([Byte]$dim); $bw.Write([Byte]$dim)
    $bw.Write([Byte]0)               # palette colors
    $bw.Write([Byte]0)               # reserved
    $bw.Write([UInt16]1)             # planes
    $bw.Write([UInt16]32)            # bpp
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($entry in $images) { $bw.Write($entry[1]) }
$bw.Flush(); $bw.Close()

Write-Host "Wrote $OutPath ($($images.Count) sizes)"
