#Requires -Version 7.0
<#
.SYNOPSIS
    Regenerates src/KiwiTraffic.App/Assets/app.ico.

.DESCRIPTION
    The icon is a filled accent-coloured circle with the product name in it.
    It is generated rather than hand-drawn so the mark stays in step with the
    widget's accent colour and so every size in the .ico is consistent.

    The generated .ico is committed; run this only when the mark itself changes.

.EXAMPLE
    pwsh -File scripts/make-app-icon.ps1
#>
[CmdletBinding()]
param(
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $OutputPath) {
    $repoRoot = Split-Path -Parent $PSScriptRoot
    $OutputPath = Join-Path $repoRoot 'src/KiwiTraffic.App/Assets/app.ico'
}

Add-Type -AssemblyName System.Drawing

# Windows picks the closest size; these cover the taskbar, Explorer and the
# large-icon views.
$sizes = @(16, 20, 24, 32, 48, 64, 128, 256)

$accent = [System.Drawing.Color]::FromArgb(255, 0x2D, 0x7F, 0xF9)

function New-MarkBitmap {
    param([int]$Size)

    $bitmap = [System.Drawing.Bitmap]::new($Size, $Size)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)

    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $inset = [math]::Max(0.5, $Size * 0.02)
        $diameter = $Size - (2 * $inset)

        $disc = [System.Drawing.SolidBrush]::new($accent)
        try {
            $graphics.FillEllipse($disc, $inset, $inset, $diameter, $diameter)
        }
        finally {
            $disc.Dispose()
        }

        # Four letters are unreadable below about 32 px, so the small sizes use
        # just the initial.
        $text = if ($Size -lt 32) { 'K' } else { 'KiWi' }
        $emSize = if ($Size -lt 32) { $Size * 0.64 } elseif ($Size -lt 64) { $Size * 0.38 } else { $Size * 0.36 }

        $font = [System.Drawing.Font]::new(
            'Segoe UI', $emSize, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
        $ink = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        $format = [System.Drawing.StringFormat]::new()

        try {
            $format.Alignment = [System.Drawing.StringAlignment]::Center
            $format.LineAlignment = [System.Drawing.StringAlignment]::Center

            $bounds = [System.Drawing.RectangleF]::new(0, 0, $Size, $Size)
            $graphics.DrawString($text, $font, $ink, $bounds, $format)
        }
        finally {
            $format.Dispose()
            $ink.Dispose()
            $font.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    return $bitmap
}

$images = foreach ($size in $sizes) {
    $bitmap = New-MarkBitmap -Size $size

    try {
        $buffer = [System.IO.MemoryStream]::new()
        try {
            $bitmap.Save($buffer, [System.Drawing.Imaging.ImageFormat]::Png)
            [pscustomobject]@{
                Size  = $size
                Bytes = $buffer.ToArray()
            }
        }
        finally {
            $buffer.Dispose()
        }
    }
    finally {
        $bitmap.Dispose()
    }
}

New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null

$file = [System.IO.File]::Create($OutputPath)
$writer = [System.IO.BinaryWriter]::new($file)

try {
    # ICONDIR
    $writer.Write([uint16]0)             # reserved
    $writer.Write([uint16]1)             # resource type: icon
    $writer.Write([uint16]$images.Count)

    # ICONDIRENTRY per image, then the payloads
    $offset = 6 + (16 * $images.Count)

    foreach ($image in $images) {
        # 256 is encoded as 0 in the single-byte dimension fields.
        $dimension = if ($image.Size -ge 256) { 0 } else { $image.Size }

        $writer.Write([byte]$dimension)
        $writer.Write([byte]$dimension)
        $writer.Write([byte]0)           # palette size (0 = truecolour)
        $writer.Write([byte]0)           # reserved
        $writer.Write([uint16]1)         # colour planes
        $writer.Write([uint16]32)        # bits per pixel
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$offset)

        $offset += $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write($image.Bytes)
    }
}
finally {
    $writer.Dispose()
    $file.Dispose()
}

$info = Get-Item $OutputPath
Write-Host "Wrote $($info.FullName) ($([math]::Round($info.Length / 1KB, 1)) KB, $($images.Count) sizes)"
