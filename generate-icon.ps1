Add-Type -AssemblyName System.Drawing

# PixelPick icon generator — random pixels variant
# Grid fills the entire rounded square (no padding), clipped to shape.
# Pixel colors are randomized each run. Crosshair centered on middle cell.

function New-IconBitmap($size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::None
    $g.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
    $g.Clear([System.Drawing.Color]::Transparent)

    $s      = [float]$size
    $radius = $s * 0.22
    $cells  = 5
    $cell   = $s / $cells

    # ── Clip to rounded square ─────────────────────────────────────────
    $clipPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $clipPath.AddArc(0, 0, $radius*2, $radius*2, 180, 90)
    $clipPath.AddArc($s - $radius*2, 0, $radius*2, $radius*2, 270, 90)
    $clipPath.AddArc($s - $radius*2, $s - $radius*2, $radius*2, $radius*2, 0, 90)
    $clipPath.AddArc(0, $s - $radius*2, $radius*2, $radius*2, 90, 90)
    $clipPath.CloseFigure()
    $g.SetClip($clipPath)

    # ── Random pixel colors (muted) ────────────────────────────────────
    $rng = New-Object System.Random

    function HslToColor($h, $s, $l) {
        $c  = (1 - [Math]::Abs(2*$l - 1)) * $s
        $x  = $c * (1 - [Math]::Abs(($h/60.0) % 2 - 1))
        $m  = $l - $c/2
        $hi = [int]($h / 60)
        switch ($hi) {
            0 { $rv=$c; $gv=$x; $bv=0 }
            1 { $rv=$x; $gv=$c; $bv=0 }
            2 { $rv=0;  $gv=$c; $bv=$x }
            3 { $rv=0;  $gv=$x; $bv=$c }
            4 { $rv=$x; $gv=0;  $bv=$c }
            default { $rv=$c; $gv=0; $bv=$x }
        }
        return [System.Drawing.Color]::FromArgb(255,
            [int](($rv+$m)*255), [int](($gv+$m)*255), [int](($bv+$m)*255))
    }

    for ($row = 0; $row -lt $cells; $row++) {
        for ($col = 0; $col -lt $cells; $col++) {
            $hue   = [float]($rng.Next(0, 360))
            $sat   = [float]($rng.Next(20, 55)) / 100.0
            $lit   = [float]($rng.Next(35, 70)) / 100.0
            $color = HslToColor $hue $sat $lit
            $brush = New-Object System.Drawing.SolidBrush($color)
            $g.FillRectangle($brush, [float]($col * $cell), [float]($row * $cell), [float]$cell, [float]$cell)
            $brush.Dispose()
        }
    }

    # ── Grid lines ─────────────────────────────────────────────────────
    $gridPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(40, 0, 0, 0), 1)
    for ($i = 0; $i -le $cells; $i++) {
        $x = [float]($i * $cell)
        $g.DrawLine($gridPen, $x, 0, $x, $s)
        $g.DrawLine($gridPen, 0, $x, $s, $x)
    }
    $gridPen.Dispose()

    # ── Crosshair on center cell ───────────────────────────────────────
    $mid = [Math]::Floor($cells / 2)
    $cx0 = [float]($mid * $cell)
    $cy0 = [float]($mid * $cell)
    $cx1 = $cx0 + [float]$cell
    $cy1 = $cy0 + [float]$cell
    $lx  = [float](($cx0 + $cx1) / 2)
    $ly  = [float](($cy0 + $cy1) / 2)

    $crossPen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [float][Math]::Max(2, $s * 0.05))
    $g.DrawLine($crossPen, 0,   $ly,  $cx0, $ly)
    $g.DrawLine($crossPen, $cx1, $ly,  $s,   $ly)
    $g.DrawLine($crossPen, $lx,  0,   $lx,  $cy0)
    $g.DrawLine($crossPen, $lx,  $cy1, $lx,  $s)
    $g.DrawRectangle($crossPen, $cx0, $cy0, [float]$cell, [float]$cell)
    $crossPen.Dispose()

    $g.ResetClip()
    $clipPath.Dispose()
    $g.Dispose()
    return $bmp
}

function Save-Ico($bitmaps, $path) {
    $pngs = $bitmaps | ForEach-Object {
        $ms = New-Object System.IO.MemoryStream
        $_.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        ,$ms.ToArray()
    }
    $fs = [System.IO.File]::OpenWrite($path)
    $w  = New-Object System.IO.BinaryWriter($fs)
    $w.Write([int16]0); $w.Write([int16]1); $w.Write([int16]$bitmaps.Count)
    $offset = 6 + 16 * $bitmaps.Count
    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $dim = $bitmaps[$i].Width
        $w.Write([byte]$(if ($dim -ge 256) { 0 } else { $dim }))
        $w.Write([byte]$(if ($dim -ge 256) { 0 } else { $dim }))
        $w.Write([byte]0); $w.Write([byte]0)
        $w.Write([int16]1); $w.Write([int16]32)
        $w.Write([int32]$pngs[$i].Length)
        $w.Write([int32]$offset)
        $offset += $pngs[$i].Length
    }
    $pngs | ForEach-Object { $w.Write($_) }
    $w.Dispose(); $fs.Dispose()
}

function Save-Png($bmp, $path) {
    $dir = Split-Path $path
    if ($dir -and !(Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
    $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-Resized($bmp, $size) {
    $dst = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($dst)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.DrawImage($bmp, 0, 0, $size, $size)
    $g.Dispose()
    return $dst
}

$root   = $PSScriptRoot
$master = New-IconBitmap 256

$icoSizes = @(16, 32, 48, 256)
$icoBmps  = $icoSizes | ForEach-Object { if ($_ -eq 256) { $master } else { New-Resized $master $_ } }
Save-Ico $icoBmps (Join-Path $root "Assets\icon.ico")
Write-Host "Written: Assets\icon.ico"

$storeSizes = @(
    @{ size = 44;  path = "Assets\Square44x44Logo.png" },
    @{ size = 150; path = "Assets\Square150x150Logo.png" },
    @{ size = 300; path = "Assets\StoreLogo.png" },
    @{ size = 620; path = "Assets\SplashScreen.png" }
)
foreach ($entry in $storeSizes) {
    $bmp = New-Resized $master $entry.size
    Save-Png $bmp (Join-Path $root $entry.path)
    Write-Host "Written: $($entry.path)"
    if ($bmp -ne $master) { $bmp.Dispose() }
}

$master.Dispose()
Write-Host "Done."
