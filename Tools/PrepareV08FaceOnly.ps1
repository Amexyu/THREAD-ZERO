param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Source/DefaultHead.png'))
try {
    $cut=[System.Drawing.Bitmap]::new(800,650,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $g=[System.Drawing.Graphics]::FromImage($cut)
        $path=[System.Drawing.Drawing2D.GraphicsPath]::new()
        try {
            $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
            $points=[System.Drawing.Point[]]@(
                [System.Drawing.Point]::new(20,110),[System.Drawing.Point]::new(140,28),
                [System.Drawing.Point]::new(670,22),[System.Drawing.Point]::new(790,80),
                [System.Drawing.Point]::new(770,285),[System.Drawing.Point]::new(700,450),
                [System.Drawing.Point]::new(635,620),[System.Drawing.Point]::new(300,630),
                [System.Drawing.Point]::new(85,480),[System.Drawing.Point]::new(20,300))
            $path.AddPolygon($points)
            $g.SetClip($path)
            $g.DrawImage($source,[System.Drawing.Rectangle]::new(0,0,800,650),
                [System.Drawing.Rectangle]::new(250,400,800,650),[System.Drawing.GraphicsUnit]::Pixel)
        } finally {$path.Dispose();$g.Dispose()}
        $canvas=[System.Drawing.Bitmap]::new(512,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $gc=[System.Drawing.Graphics]::FromImage($canvas)
            try {
                $gc.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
                $gc.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                # Frame-0 facial placement, with the original hair, ears, neck and collar retained.
                $gc.DrawImage($cut,[System.Drawing.Rectangle]::new(242,115,80,68))
            } finally {$gc.Dispose()}
            $dir=Join-Path $Root 'Deliverables/ArtPack_v08/Intermediate/FaceOnly'
            New-Item -ItemType Directory -Path $dir -Force | Out-Null
            $out=Join-Path $dir '_default.png'
            $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
            Copy-Item -LiteralPath $out -Destination (Join-Path $dir 'h0.png') -Force
            Write-Output $out
        } finally {$canvas.Dispose()}
    } finally {$cut.Dispose()}
} finally {$source.Dispose()}
