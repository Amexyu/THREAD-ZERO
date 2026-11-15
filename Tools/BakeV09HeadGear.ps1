param([string]$Root=(Split-Path $PSScriptRoot -Parent),[string]$OutputDir='')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
if(-not $OutputDir){$OutputDir=Join-Path $Root 'Deliverables/ArtPack_v09/Staging/HeadEquipment'}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
$bodyDir=Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/AtlasesFace'
$gearDir=Join-Path $Root 'Deliverables/ArtPack_v09/LegacyHeadEquipment'
for($style=0;$style -lt 5;$style++) {
    $body=[System.Drawing.Bitmap]::new((Join-Path $bodyDir "Player_c$style.png"))
    try {
        for($id=0;$id -lt 5;$id++) {
            $gear=[System.Drawing.Bitmap]::new((Join-Path $gearDir "h$id.png"))
            try {
                $result=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                try {
                    $g=[System.Drawing.Graphics]::FromImage($result)
                    try {$g.Clear([System.Drawing.Color]::FromArgb(0,0,0,0));$g.DrawImageUnscaled($gear,0,0)}finally{$g.Dispose()}
                    if($id -eq 1 -or $id -eq 2 -or $id -eq 4) {
                        for($frame=0;$frame -lt 6;$frame++) {
                            $cx=($frame%3)*512;$cy=[int][Math]::Floor($frame/3)*512
                            $shiftX=@(0,10,10,0,14,36)[$frame]
                            $shiftY=@(0,2,2,5,24,43)[$frame]
                            # Restore only the front fringe. The ear cup, visor rim and helmet
                            # shell stay visible; locks of hair hide the upper strap naturally.
                            $left=if($id -eq 4){248}else{245}
                            $top=if($id -eq 4){78}else{65}
                            $bottom=if($id -eq 2){118}else{150}
                            for($y=$top+$shiftY;$y -lt [Math]::Min(190,$bottom+$shiftY);$y++) {
                                for($x=$left+$shiftX;$x -lt [Math]::Min(385,352+$shiftX);$x++) {
                                    $c=$body.GetPixel($cx+$x,$cy+$y)
                                    if($c.A -lt 150){continue}
                                    $hi=[Math]::Max($c.R,[Math]::Max($c.G,$c.B))
                                    $lo=[Math]::Min($c.R,[Math]::Min($c.G,$c.B))
                                    if($hi -gt 68 -and $hi-$lo -lt 35){$result.SetPixel($cx+$x,$cy+$y,$c)}
                                }
                            }
                        }
                    }
                    $path=Join-Path $OutputDir "h${id}_c${style}.png"
                    $result.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
                    Write-Output $path
                }finally{$result.Dispose()}
            }finally{$gear.Dispose()}
        }
    }finally{$body.Dispose()}
}
