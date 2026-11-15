param([string]$Root=(Split-Path $PSScriptRoot -Parent),[string]$OutputDir='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
if(-not $OutputDir){$OutputDir=Join-Path $Root 'Deliverables/ArtPack_v09/Staging/SideHeadEquipment'}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$sheet=[System.Drawing.Bitmap]::new((Join-Path $Root 'Deliverables/ArtPack_v09/Staging/HeadGear4_candidate.png'))
$headset=[System.Drawing.Bitmap]::new((Join-Path $Root 'Deliverables/ArtPack_v09/Staging/Headset_side_candidate.png'))
$hood=[System.Drawing.Bitmap]::new((Join-Path $Root 'Deliverables/ArtPack_v09/Staging/Hood_side_candidate.png'))
$crops=@(
    [System.Drawing.Rectangle]::new(268,76,797,993),
    [System.Drawing.Rectangle]::new(778,264,277,177),
    [System.Drawing.Rectangle]::new(216,876,301,81),
    [System.Drawing.Rectangle]::new(766,748,297,313),
    [System.Drawing.Rectangle]::new(368,332,565,545)
)
$rects=@(
    @(202,34,127,134),
    @(210,102,122,78),
    @(223,94,103,33),
    @(203,40,132,140),
    @(203,38,130,125)
)
$dx=@(0,10,10,0,14,36);$dy=@(0,2,2,5,24,43)
try {
    for($id=0;$id -lt 5;$id++) {
        $source=if($id -eq 4){$headset}elseif($id -eq 0){$hood}else{$sheet}
        $atlas=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g=[System.Drawing.Graphics]::FromImage($atlas)
            try {
                $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
                $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                for($f=0;$f -lt 6;$f++) {
                    $cx=($f%3)*512;$cy=[int][Math]::Floor($f/3)*512
                    $r=$rects[$id]
                    $poseY=if($id -eq 0 -and $f -eq 4){16}elseif($id -eq 0 -and $f -eq 5){30}else{$dy[$f]}
                    $dest=[System.Drawing.Rectangle]::new($cx+$r[0]+$dx[$f],$cy+$r[1]+$poseY,$r[2],$r[3])
                    $g.DrawImage($source,$dest,$crops[$id],[System.Drawing.GraphicsUnit]::Pixel)
                }
            }finally{$g.Dispose()}
            $path=Join-Path $OutputDir "h$id.png"
            $atlas.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output $path
        }finally{$atlas.Dispose()}
    }
}finally{$sheet.Dispose();$headset.Dispose();$hood.Dispose()}
