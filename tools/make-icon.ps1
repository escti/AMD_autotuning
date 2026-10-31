<#
.SYNOPSIS
    Gera o icone multi-resolucao do AMD AutoTuning & Optimizer (EFF5).
.DESCRIPTION
    Renderiza a arte vetorial em cada tamanho alvo e empacota um .ico
    com frames comprimidos em PNG (suportado pelo Windows Vista+).
#>

param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\AMDAutoTuning\Assets\app.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256)

function New-IconBitmap {
    param([int]$Size)

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $Size / 256.0
    $r = 48 * $s

    # --- Background: rounded rect with vertical gradient ---
    $bgPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bgPath.AddArc(0, 0, $r, $r, 180, 90)
    $bgPath.AddArc($Size - $r, 0, $r, $r, 270, 90)
    $bgPath.AddArc($Size - $r, $Size - $r, $r, $r, 0, 90)
    $bgPath.AddArc(0, $Size - $r, $r, $r, 90, 90)
    $bgPath.CloseFigure()

    $bgRect = New-Object System.Drawing.RectangleF -ArgumentList 0, 0, ([float]$Size), ([float]$Size)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList $bgRect, ([System.Drawing.ColorTranslator]::FromHtml("#1B2130")), ([System.Drawing.ColorTranslator]::FromHtml("#0A0C11")), ([float]90)
    $g.FillPath($bgBrush, $bgPath)
    $bgBrush.Dispose()

    # --- Inner border highlight ---
    $borderWidth = [float][Math]::Max(1.0, $Size * 0.011)
    $borderPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.ColorTranslator]::FromHtml("#2E3852")), $borderWidth
    $g.DrawPath($borderPen, $bgPath)
    $borderPen.Dispose()

    # --- Lightning bolt ---
    $rawPoints = @(
        @(162, 30),
        @(76, 144),
        @(118, 144),
        @(94, 228),
        @(180, 106),
        @(136, 106)
    )

    $pts = New-Object 'System.Drawing.PointF[]' $rawPoints.Count
    for ($i = 0; $i -lt $rawPoints.Count; $i++) {
        $px = [float]($rawPoints[$i][0] * $s)
        $py = [float]($rawPoints[$i][1] * $s)
        $pts[$i] = New-Object System.Drawing.PointF -ArgumentList $px, $py
    }

    $boltPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $boltPath.AddPolygon($pts)

    # Glow pass (subtle outer stroke)
    $glowPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(70, 239, 68, 68)), ([float]($Size * 0.035))
    $glowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($glowPen, $boltPath)
    $glowPen.Dispose()

    # Gradient body (rectangle-based, 90 degrees = top to bottom)
    $boltRect = New-Object System.Drawing.RectangleF -ArgumentList ([float](85 * $s)), ([float](30 * $s)), ([float](90 * $s)), ([float](196 * $s))
    $boltBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList $boltRect, ([System.Drawing.ColorTranslator]::FromHtml("#FF7A5C")), ([System.Drawing.ColorTranslator]::FromHtml("#C81E1E")), ([float]90)
    $g.FillPath($boltBrush, $boltPath)
    $boltBrush.Dispose()

    # Crisp inner outline
    if ($Size -ge 32) {
        $innerPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(90, 255, 255, 255)), ([float][Math]::Max(0.8, $Size * 0.004))
        $innerPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($innerPen, $boltPath)
        $innerPen.Dispose()
    }

    $boltPath.Dispose()
    $bgPath.Dispose()
    $g.Dispose()

    return $bmp
}

# --- Build PNG frames ---
$frames = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap -Size $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += [PSCustomObject]@{ Size = $size; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

# --- Assemble ICO container ---
$outDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

# ICONDIR
$writer.Write([UInt16]0)                  # reserved
$writer.Write([UInt16]1)                  # type = icon
$writer.Write([UInt16]$frames.Count)      # image count

# Header size: 6 + 16 * count
$offset = 6 + (16 * $frames.Count)

foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $writer.Write([Byte]$dim)             # width
    $writer.Write([Byte]$dim)             # height
    $writer.Write([Byte]0)                # color palette count
    $writer.Write([Byte]0)                # reserved
    $writer.Write([UInt16]1)              # color planes
    $writer.Write([UInt16]32)             # bits per pixel
    $writer.Write([UInt32]$f.Bytes.Length)# bytes in resource
    $writer.Write([UInt32]$offset)        # offset
    $offset += $f.Bytes.Length
}

foreach ($f in $frames) {
    $writer.Write($f.Bytes)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
$writer.Close()
$stream.Dispose()

$file = Get-Item $OutputPath
Write-Host "Icone gerado: $($file.FullName) ($([math]::Round($file.Length/1KB,1)) KB, $($frames.Count) tamanhos)" -ForegroundColor Green
