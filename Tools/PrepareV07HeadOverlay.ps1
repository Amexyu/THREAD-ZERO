param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$sourceDir=Join-Path $Root 'Deliverables/ArtPack_v08/Source'
$targetDir=Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/HeadOverlay'
New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
foreach($entry in @(@('DefaultHead.png','_default.png'),@('HoodHead.png','h0.png'))) {
    $source=[System.Drawing.Image]::FromFile((Join-Path $sourceDir $entry[0]))
    try {
        $canvas=[System.Drawing.Bitmap]::new(512,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g=[System.Drawing.Graphics]::FromImage($canvas)
            try {
                $g.Clear([System.Drawing.Color]::Transparent)
                $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                $width=165; $height=[int][Math]::Round($source.Height*$width/$source.Width)
                $g.DrawImage($source,[System.Drawing.Rectangle]::new(174,181-$height,$width,$height))
            } finally {$g.Dispose()}
            $path=Join-Path $targetDir $entry[1]
            $canvas.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output $path
        } finally {$canvas.Dispose()}
    } finally {$source.Dispose()}
}
