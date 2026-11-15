param(
    [string]$Root = (Split-Path $PSScriptRoot -Parent)
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourceDir = Join-Path $Root 'Deliverables/ArtPack_v08/Source'
$targets = @(
    (Join-Path $Root 'Deliverables/ArtPack_v08/Runtime/Head'),
    (Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/Head')
)
foreach ($target in $targets) {
    New-Item -ItemType Directory -Path $target -Force | Out-Null
}

function Export-Head([string]$sourceName, [string]$targetName) {
    $sourcePath = Join-Path $sourceDir $sourceName
    $source = [System.Drawing.Image]::FromFile($sourcePath)
    try {
        $canvas = [System.Drawing.Bitmap]::new(512, 512, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $graphics = [System.Drawing.Graphics]::FromImage($canvas)
            try {
                $graphics.Clear([System.Drawing.Color]::Transparent)
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceOver
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
                # The mount ends at (256,112), just inside the c0 raised collar.
                $width = 100
                $height = [int][Math]::Round($source.Height * $width / $source.Width)
                $graphics.DrawImage($source, [System.Drawing.Rectangle]::new(206, (112 - $height), $width, $height))
            } finally { $graphics.Dispose() }
            foreach ($dir in $targets) {
                $path = Join-Path $dir $targetName
                $canvas.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
                Write-Output $path
            }
        } finally { $canvas.Dispose() }
    } finally { $source.Dispose() }
}

Export-Head 'DefaultHead.png' '_default.png'
Export-Head 'HoodHead.png' 'h0.png'
