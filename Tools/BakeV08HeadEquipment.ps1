param([string]$Root=(Split-Path $PSScriptRoot -Parent),[string]$OutputDir='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$iconDir=Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/Icons'
$outDir=if($OutputDir){$OutputDir}else{Join-Path $Root 'Deliverables/ArtPack_v09/LegacyHeadEquipment'}
New-Item -ItemType Directory -Path $outDir -Force | Out-Null
# Frame zero positions in the same 512px coordinate system as the player atlases.
# These are equipment surfaces only: never replace the animated neck or collar.
$rects=@(
    @(185,24,157,151), # hood
    @(207,88,131,94),  # respirator and straps
    @(227,83,111,64),  # visor
    @(202,47,130,118), # full helmet
    @(203,45,128,116)  # cat-ear terminal, left cup lands by the visible ear
)
$dx=@(0,10,10,0,14,36);$dy=@(0,2,2,5,24,43)
for($id=0;$id -lt 5;$id++) {
    $icon=[System.Drawing.Bitmap]::new((Join-Path $iconDir "h$id.png"))
    try {
        if($id -eq 4) {
            # The icon shows both ear cups from a three-quarter angle. On the side-view
            # player the far cup would cover the face; keep the band and near cup only.
            for($iy=92;$iy -lt $icon.Height;$iy++) { for($ix=166;$ix -lt $icon.Width;$ix++) {
                $icon.SetPixel($ix,$iy,[System.Drawing.Color]::FromArgb(0,0,0,0))
            } }
        }
        $atlas=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g=[System.Drawing.Graphics]::FromImage($atlas)
            try {
                $g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0))
                $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                for($frame=0;$frame -lt 6;$frame++) {
                    $x=($frame%3)*512+$rects[$id][0]+$dx[$frame]
                    $y=[int][Math]::Floor($frame/3)*512+$rects[$id][1]+$dy[$frame]
                    $g.DrawImage($icon,[System.Drawing.Rectangle]::new($x,$y,$rects[$id][2],$rects[$id][3]))
                }
            } finally {$g.Dispose()}
            $out=Join-Path $outDir "h$id.png"
            $atlas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output $out
        } finally {$atlas.Dispose()}
    } finally {$icon.Dispose()}
}
