# Tekent het app-icoon (maansikkel met sterretjes) en schrijft een .ico met PNG-lagen in meerdere formaten.
# Gebruik (vanuit de projectroot):  powershell -NoProfile -File tools\make_icon.ps1 OledProtect\Assets\icon.ico
param([Parameter(Mandatory = $true)][string]$OutFile)

Add-Type -AssemblyName System.Drawing
$sizes = 16, 20, 24, 32, 48, 64, 128, 256

function New-IconPng([int]$s) {
    $bmp = New-Object System.Drawing.Bitmap $s, $s
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Achtergrond: een donkere, ronde "nachthemel" zodat het icoon ook op een lichte taakbalk leesbaar is.
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF 0, 0), (New-Object System.Drawing.PointF $s, $s),
        [System.Drawing.Color]::FromArgb(255, 28, 36, 64), [System.Drawing.Color]::FromArgb(255, 8, 10, 20))
    $pad = [Math]::Max(0.5, $s * 0.03)
    $g.FillEllipse($bg, $pad, $pad, $s - 2 * $pad, $s - 2 * $pad)

    # Maansikkel: grote cirkel min een verschoven cirkel.
    $moon = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = $s * 0.30; $cx = $s * 0.46; $cy = $s * 0.54
    $moon.AddEllipse($cx - $r, $cy - $r, 2 * $r, 2 * $r)
    $cut = New-Object System.Drawing.Drawing2D.GraphicsPath
    $cut.AddEllipse($cx - $r + $s * 0.13, $cy - $r - $s * 0.08, 2 * $r, 2 * $r)
    $region = New-Object System.Drawing.Region $moon
    $region.Exclude($cut)
    $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 243, 212, 140))
    $g.FillRegion($gold, $region)

    # Sterretjes (vierpuntig), de kleinste valt weg op heel kleine formaten.
    function Star($x, $y, $size, $color) {
        $b = New-Object System.Drawing.SolidBrush $color
        $pts = @(
            (New-Object System.Drawing.PointF $x, ($y - $size)),
            (New-Object System.Drawing.PointF ($x + $size * 0.28), ($y - $size * 0.28)),
            (New-Object System.Drawing.PointF ($x + $size), $y),
            (New-Object System.Drawing.PointF ($x + $size * 0.28), ($y + $size * 0.28)),
            (New-Object System.Drawing.PointF $x, ($y + $size)),
            (New-Object System.Drawing.PointF ($x - $size * 0.28), ($y + $size * 0.28)),
            (New-Object System.Drawing.PointF ($x - $size), $y),
            (New-Object System.Drawing.PointF ($x - $size * 0.28), ($y - $size * 0.28)))
        $g.FillPolygon($b, $pts)
    }
    Star ($s * 0.72) ($s * 0.30) ($s * 0.13) ([System.Drawing.Color]::FromArgb(255, 210, 225, 255))
    if ($s -ge 24) { Star ($s * 0.78) ($s * 0.62) ($s * 0.06) ([System.Drawing.Color]::FromArgb(255, 127, 167, 255)) }

    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    if ($s -ge 256) {
        # Large size as PNG (what Windows expects for 256 px).
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    } else {
        # Smaller sizes as a classic 32-bit DIB, which every reader (also System.Drawing) understands.
        $bw = New-Object System.IO.BinaryWriter $ms
        $bw.Write([UInt32]40); $bw.Write([Int32]$s); $bw.Write([Int32]($s * 2))
        $bw.Write([UInt16]1); $bw.Write([UInt16]32); $bw.Write([UInt32]0)
        $bw.Write([UInt32]($s * $s * 4)); $bw.Write([Int32]0); $bw.Write([Int32]0); $bw.Write([UInt32]0); $bw.Write([UInt32]0)
        for ($y = $s - 1; $y -ge 0; $y--) {            # bottom-up rows, BGRA
            for ($x = 0; $x -lt $s; $x++) {
                $c = $bmp.GetPixel($x, $y)
                $bw.Write([byte]$c.B); $bw.Write([byte]$c.G); $bw.Write([byte]$c.R); $bw.Write([byte]$c.A)
            }
        }
        $maskRow = [Math]::Ceiling($s / 32) * 4        # AND mask: all zero (alpha does the work)
        $bw.Write((New-Object byte[] ($maskRow * $s)))
        $bw.Flush()
    }
    $bmp.Dispose()
    return , $ms.ToArray()
}

$images = foreach ($s in $sizes) { , (New-IconPng $s) }

# ICO-bestand: header + directory + PNG-data.
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $len = $images[$i].Length
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$len); $w.Write([UInt32]$offset)
    $offset += $len
}
foreach ($img in $images) { $w.Write($img) }
$w.Flush()
[System.IO.File]::WriteAllBytes((Resolve-Path -LiteralPath (Split-Path $OutFile -Parent)).Path + "\" + (Split-Path $OutFile -Leaf), $out.ToArray())
"geschreven: $OutFile ($($out.Length) bytes, formaten: $($sizes -join ', '))"
