[CmdletBinding()]
param(
    [string]$Source,
    [string]$Destination,
    [int[]]$Sizes = @(16, 24, 32, 48, 64, 128, 256)
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($Source)) {
    $Source = Join-Path $PSScriptRoot '..\src\CodexMonitor.App\Assets\CodexMonitor.png'
}

if ([string]::IsNullOrWhiteSpace($Destination)) {
    $Destination = Join-Path $PSScriptRoot '..\src\CodexMonitor.App\Assets\CodexMonitor.ico'
}

$sourcePath = [System.IO.Path]::GetFullPath($Source)
$destinationPath = [System.IO.Path]::GetFullPath($Destination)
$destinationDirectory = [System.IO.Path]::GetDirectoryName($destinationPath)

if (-not [System.IO.File]::Exists($sourcePath)) {
    throw "Icon source was not found: $sourcePath"
}

if ($null -eq $Sizes -or $Sizes.Count -eq 0) {
    throw 'At least one icon size is required.'
}

$normalizedSizes = @($Sizes | Sort-Object -Unique)
foreach ($size in $normalizedSizes) {
    if ($size -lt 1 -or $size -gt 256) {
        throw "Icon size must be between 1 and 256 pixels: $size"
    }
}

if (-not [System.IO.Directory]::Exists($destinationDirectory)) {
    [System.IO.Directory]::CreateDirectory($destinationDirectory) | Out-Null
}

Add-Type -AssemblyName System.Drawing

$sourceImage = $null
$iconImages = [System.Collections.Generic.List[byte[]]]::new()
$temporaryPath = "$destinationPath.tmp"

try {
    $sourceImage = [System.Drawing.Image]::FromFile($sourcePath)

    foreach ($size in $normalizedSizes) {
        $bitmap = [System.Drawing.Bitmap]::new(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = $null
        $memoryStream = $null

        try {
            $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.DrawImage($sourceImage, 0, 0, $size, $size)

            $memoryStream = [System.IO.MemoryStream]::new()
            $bitmap.Save($memoryStream, [System.Drawing.Imaging.ImageFormat]::Png)
            $iconImages.Add($memoryStream.ToArray())
        }
        finally {
            if ($null -ne $memoryStream) {
                $memoryStream.Dispose()
            }

            if ($null -ne $graphics) {
                $graphics.Dispose()
            }

            $bitmap.Dispose()
        }
    }

    $fileStream = $null
    $writer = $null

    try {
        $fileStream = [System.IO.File]::Open(
            $temporaryPath,
            [System.IO.FileMode]::Create,
            [System.IO.FileAccess]::Write,
            [System.IO.FileShare]::None)
        $writer = [System.IO.BinaryWriter]::new($fileStream)

        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$iconImages.Count)

        $imageOffset = 6 + (16 * $iconImages.Count)
        for ($index = 0; $index -lt $iconImages.Count; $index++) {
            $size = $normalizedSizes[$index]
            $dimensionByte = if ($size -eq 256) { 0 } else { $size }
            $imageBytes = $iconImages[$index]

            $writer.Write([byte]$dimensionByte)
            $writer.Write([byte]$dimensionByte)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$imageBytes.Length)
            $writer.Write([uint32]$imageOffset)

            $imageOffset += $imageBytes.Length
        }

        foreach ($imageBytes in $iconImages) {
            $writer.Write($imageBytes)
        }
    }
    finally {
        if ($null -ne $writer) {
            $writer.Dispose()
        }
        elseif ($null -ne $fileStream) {
            $fileStream.Dispose()
        }
    }

    [System.IO.File]::Copy($temporaryPath, $destinationPath, $true)
    [System.IO.File]::Delete($temporaryPath)
}
finally {
    if ($null -ne $sourceImage) {
        $sourceImage.Dispose()
    }

    if ([System.IO.File]::Exists($temporaryPath)) {
        [System.IO.File]::Delete($temporaryPath)
    }
}

Get-Item -LiteralPath $destinationPath
