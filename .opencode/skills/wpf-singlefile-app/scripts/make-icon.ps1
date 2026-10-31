<#
.SYNOPSIS
    Gera um arquivo .ico multi-resolucao a partir de arte vetorial desenhada em GDI+.

.DESCRIPTION
    Renderiza a mesma arte em 16, 24, 32, 48, 64, 128 e 256 px e empacota um ICO
    com frames comprimidos em PNG (suportado pelo Windows Vista+).

    Gere cada tamanho nativamente (escalando as coordenadas), em vez de reduzir
    uma imagem grande: e o que mantem nitidez em 16/24 px.

.PARAMETER OutputPath
    Caminho do .ico de saida.

.PARAMETER AccentColor
    Cor principal do glifo (hex). Padrao: vermelho.

.EXAMPLE
    .\make-icon.ps1 -OutputPath ..\App\Assets\app.ico

.NOTES
    Personalize a funcao New-IconBitmap para desenhar o seu glifo.
    Salve este arquivo em UTF-8 COM BOM se inserir acentos.
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$AccentColor = "#DC2626",
    [string]$AccentLight = "#FF7A5C",
    [string]$BackgroundTop = "#1B2130",
    [string]$BackgroundBottom = "#0A0C11",
    [string]$BorderColor = "#2E3852"
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
    $radius = 48 * $s

    # Fundo arredondado com gradiente vertical
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bg.AddArc(0, 0, $radius, $radius, 180, 90)
    $bg.AddArc($Size - $radius, 0, $radius, $radius, 270, 90)
    $bg.AddArc($Size - $radius, $Size - $radius, $radius, $radius, 0, 90)
    $bg.AddArc(0, $Size - $radius, $radius, $radius, 90, 90)
    $bg.CloseFigure()

    $bgRect = New-Object System.Drawing.RectangleF -ArgumentList 0, 0, ([float]$Size), ([float]$Size)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList $bgRect,
        ([System.Drawing.ColorTranslator]::FromHtml($BackgroundTop)),
        ([System.Drawing.ColorTranslator]::FromHtml($BackgroundBottom)), ([float]90)
    $g.FillPath($bgBrush, $bg)
    $bgBrush.Dispose()

    $borderPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.ColorTranslator]::FromHtml($BorderColor)), ([float][Math]::Max(1.0, $Size * 0.011))
    $g.DrawPath($borderPen, $bg)
    $borderPen.Dispose()

    # ---------------- GLYPH ----------------
    # Relampago. Substitua por seu proprio poligono/curvas.
    $rawPoints = @(
        @(162, 30), @(76, 144), @(118, 144),
        @(94, 228), @(180, 106), @(136, 106)
    )

    $pts = New-Object 'System.Drawing.PointF[]' $rawPoints.Count
    for ($i = 0; $i -lt $rawPoints.Count; $i++) {
        $pts[$i] = New-Object System.Drawing.PointF -ArgumentList ([float]($rawPoints[$i][0] * $s)), ([float]($rawPoints[$i][1] * $s))
    }

    $glyph = New-Object System.Drawing.Drawing2D.GraphicsPath
    $glyph.AddPolygon($pts)

    # Glow
    $glowPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(70, 239, 68, 68)), ([float]($Size * 0.035))
    $glowPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($glowPen, $glyph)
    $glowPen.Dispose()

    # Corpo com gradiente (brush retangular evita OutOfMemory de gradiente por pontos)
    $glyphRect = New-Object System.Drawing.RectangleF -ArgumentList ([float](76 * $s)), ([float](30 * $s)), ([float](104 * $s)), ([float](198 * $s))
    $glyphBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush -ArgumentList $glyphRect,
        ([System.Drawing.ColorTranslator]::FromHtml($AccentLight)),
        ([System.Drawing.ColorTranslator]::FromHtml($AccentColor)), ([float]90)
    $g.FillPath($glyphBrush, $glyph)
    $glyphBrush.Dispose()

    if ($Size -ge 32) {
        $innerPen = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(90, 255, 255, 255)), ([float][Math]::Max(0.8, $Size * 0.004))
        $innerPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        $g.DrawPath($innerPen, $glyph)
        $innerPen.Dispose()
    }
    # -------------- FIM DO GLYPH --------------

    $glyph.Dispose()
    $bg.Dispose()
    $g.Dispose()

    return $bmp
}

# Gera os frames PNG
$frames = @()
foreach ($size in $sizes) {
    $bmp = New-IconBitmap -Size $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += [PSCustomObject]@{ Size = $size; Bytes = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

# Monta o container ICO
$outDir = Split-Path -Parent $OutputPath
if ($outDir -and -not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([UInt16]0)              # reserved
$writer.Write([UInt16]1)              # type = icon
$writer.Write([UInt16]$frames.Count)  # image count

$offset = 6 + (16 * $frames.Count)

foreach ($f in $frames) {
    $dim = if ($f.Size -ge 256) { 0 } else { $f.Size }
    $writer.Write([Byte]$dim)
    $writer.Write([Byte]$dim)
    $writer.Write([Byte]0)            # palette
    $writer.Write([Byte]0)            # reserved
    $writer.Write([UInt16]1)          # planes
    $writer.Write([UInt16]32)         # bpp
    $writer.Write([UInt32]$f.Bytes.Length)
    $writer.Write([UInt32]$offset)
    $offset += $f.Bytes.Length
}

foreach ($f in $frames) { $writer.Write($f.Bytes) }

$writer.Flush()
[System.IO.File]::WriteAllBytes($OutputPath, $stream.ToArray())
$writer.Close()
$stream.Dispose()

$file = Get-Item $OutputPath
Write-Host "Icone gerado: $($file.FullName) ($([math]::Round($file.Length / 1KB, 1)) KB, $($frames.Count) tamanhos)" -ForegroundColor Green
