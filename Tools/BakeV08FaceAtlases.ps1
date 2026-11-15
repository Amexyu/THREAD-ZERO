param([string]$Root=(Split-Path $PSScriptRoot -Parent),[int[]]$Styles=@(0,1,2,3,4))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$mask=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Intermediate/FaceOnly/_default.png'))
$dx=@(0,10,10,0,14,36)
$dy=@(0,2,2,5,8,25)
try {
    foreach($set in @(@('ThreadZero/Assets/Resources/Art/V07/Atlases','AtlasesFace'),@('Deliverables/ArtPack_v08/Intermediate/AtlasesWeaponOnly','AtlasesFaceWeaponClean'))) {
        $inputDir=Join-Path $Root $set[0]
        $outputDir=Join-Path $Root ('ThreadZero/Assets/Resources/Art/V08/'+$set[1])
        New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
        foreach($style in $Styles) {
            $source=[System.Drawing.Bitmap]::new((Join-Path $inputDir "Player_c$style.png"))
            try {
                $result=[System.Drawing.Bitmap]::new($source.Width,$source.Height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
                try {
                    $g=[System.Drawing.Graphics]::FromImage($result)
                    try {
                        $g.DrawImageUnscaled($source,0,0)
                        for($f=0;$f -lt 6;$f++) {
                            $cx=($f%3)*512;$cy=[int][Math]::Floor($f/3)*512
                            $g.DrawImage($mask,[System.Drawing.Rectangle]::new($cx+$dx[$f],$cy+$dy[$f],512,512))
                        }
                    } finally {$g.Dispose()}
                    # The fringe is part of the original animated silhouette and sits in front of the visor.
                    for($f=0;$f -lt 6;$f++) {
                        $cx=($f%3)*512;$cy=[int][Math]::Floor($f/3)*512
                        for($y=54;$y -lt 148;$y++) { for($x=170;$x -lt 365;$x++) {
                            $c=$source.GetPixel($cx+$x,$cy+$y)
                            if($c.A -lt 128) {continue}
                            $max=[Math]::Max($c.R,[Math]::Max($c.G,$c.B))
                            $min=[Math]::Min($c.R,[Math]::Min($c.G,$c.B))
                            if($max -gt 65 -and $max-$min -lt 30) {$result.SetPixel($cx+$x,$cy+$y,$c)}
                        } }
                    }
                    $out=Join-Path $outputDir "Player_c$style.png"
                    $result.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
                    Write-Output $out
                } finally {$result.Dispose()}
            } finally {$source.Dispose()}
        }
    }
} finally {$mask.Dispose()}
