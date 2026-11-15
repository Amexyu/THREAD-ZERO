param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[System.Drawing.Bitmap]::new((Join-Path $Root 'Deliverables/ArtPack_v09/Staging/Headset_side_candidate.png'))
$outDir=Join-Path $Root 'Deliverables/ArtPack_v09/Staging/HeadsetPilot'
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
try {
    $atlas=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    try {
        $g=[System.Drawing.Graphics]::FromImage($atlas)
        try {
            $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
            $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $crop=[System.Drawing.Rectangle]::new(368,332,565,545)
            $dx=@(0,10,10,0,14,36);$dy=@(0,2,2,5,24,43)
            for($f=0;$f -lt 6;$f++) {
                $cx=($f%3)*512;$cy=[int][Math]::Floor($f/3)*512
                $dest=[System.Drawing.Rectangle]::new($cx+203+$dx[$f],$cy+38+$dy[$f],130,125)
                $g.DrawImage($source,$dest,$crop,[System.Drawing.GraphicsUnit]::Pixel)
            }
        }finally{$g.Dispose()}
        $path=Join-Path $outDir 'h4.png'
        $atlas.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output $path
    }finally{$atlas.Dispose()}
}finally{$source.Dispose()}
