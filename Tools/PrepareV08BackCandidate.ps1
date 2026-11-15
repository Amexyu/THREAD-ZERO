param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$source=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Source/Back_b0.png'))
try {
    $canvas=[System.Drawing.Bitmap]::new(512,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $g=[System.Drawing.Graphics]::FromImage($canvas)
        try {
            $g.Clear([System.Drawing.Color]::Transparent)
            $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            # Temporary mount, behind the body; final position depends on c0 pose anchors.
            $width=108; $height=[int][Math]::Round($source.Height*$width/$source.Width)
            $g.DrawImage($source,[System.Drawing.Rectangle]::new(158,142,$width,$height))
        } finally { $g.Dispose() }
        foreach($sub in @('Deliverables/ArtPack_v08/Runtime/Back','ThreadZero/Assets/Resources/Art/V08/Back')) {
            $dir=Join-Path $Root $sub
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $path=Join-Path $dir 'b0.png'
            $canvas.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output $path
        }
    } finally { $canvas.Dispose() }
} finally { $source.Dispose() }
