param([string]$Root = (Split-Path $PSScriptRoot -Parent), [int[]]$Styles = @(0,1,2,3,4), [switch]$HeadOnly, [switch]$KeepHead)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$inputDir=Join-Path $Root 'ThreadZero/Assets/Resources/Art/V07/Atlases'
$outputDir=Join-Path $Root $(if($KeepHead){'Deliverables/ArtPack_v08/Intermediate/AtlasesWeaponOnly'}elseif($HeadOnly){'Deliverables/ArtPack_v08/Intermediate/AtlasesHeadClean'}else{'Deliverables/ArtPack_v08/Intermediate/AtlasesClean'})
New-Item -ItemType Directory -Path $outputDir -Force | Out-Null

# Six poses share the 512x512 grid. These windows are deliberately limited to the old skull,
# leaving the collar, raised arm and the character's hands outside the destructive mask.
$headX=@(255,270,270,255,282,296)
$headY=@(105,108,108,111,119,125)
$headRx=@(93,91,91,93,78,88)
$headRy=@(77,75,75,77,78,73)
function Erase-Polygon($bitmap,[int]$cellX,[int]$cellY,[int[][]]$coords) {
    $points=@($coords | ForEach-Object { [System.Drawing.Point]::new(($cellX+$_[0]),($cellY+$_[1])) })
    $g=[System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $g.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::None
        $brush=[System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(0,0,0,0))
        try { $g.FillPolygon($brush,[System.Drawing.Point[]]$points) } finally { $brush.Dispose() }
    } finally { $g.Dispose() }
}

foreach($style in $Styles) {
    $source=[System.Drawing.Bitmap]::new((Join-Path $inputDir "Player_c$style.png"))
    try {
        $result=[System.Drawing.Bitmap]::new($source.Width,$source.Height,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g=[System.Drawing.Graphics]::FromImage($result)
            try { $g.DrawImageUnscaled($source,0,0) } finally { $g.Dispose() }
            for($frame=0;$frame -lt 6;$frame++) {
                $cellX=($frame%3)*512; $cellY=[int][Math]::Floor($frame/3)*512
                $rx=$headRx[$frame];$ry=$headRy[$frame]
                # Stop above the jacket collar. The replacement head overlaps the remaining neck.
                $bottom= $(if($frame -eq 5){158}else{160})
                if(-not $KeepHead) { for($ly=[Math]::Max(0,$headY[$frame]-$ry);$ly -lt [Math]::Min($bottom,$headY[$frame]+$ry);$ly++) {
                    for($lx=[Math]::Max(0,$headX[$frame]-$rx);$lx -lt [Math]::Min(390,$headX[$frame]+$rx);$lx++) {
                        $nx=($lx-$headX[$frame])/$rx;$ny=($ly-$headY[$frame])/$ry
                        if($nx*$nx+$ny*$ny -gt 1) { continue }
                        # The overhead melee pose has a hand and sword crossing the head window.
                        if($frame -eq 4 -and $lx -lt 220) { continue }
                        $px=$cellX+$lx;$py=$cellY+$ly
                        $c=$source.GetPixel($px,$py)
                        if($c.A -lt 8) { continue }
                        # Preserve the raised blade and the warm hand in the overhead swing.
                        $cyan=($c.B -gt $c.R+32 -and $c.G -gt $c.R+25)
                        if($frame -eq 4 -and ($cyan -or ($ly -lt 96 -and $lx -lt 310))) { continue }
                        $result.SetPixel($px,$py,[System.Drawing.Color]::FromArgb(0,0,0,0))
                    }
                } }
                if(-not $KeepHead -and $frame -ne 4) {
                    $extra= $(if($frame -eq 5){@(@(215,35),@(255,35),@(255,100),@(215,120))}else{@(@(158,25),@(217,25),@(217,142),@(158,150))})
                    Erase-Polygon $result $cellX $cellY $extra
                }
                # Remove the weapon painted into the old whole-frame image. Keep the grip hands.
                if(-not $HeadOnly) { switch($frame) {
                    0 {
                        # Replace the baked cyan hip pistol with neighbouring trouser cloth before drawing w0.
                        $patch=[System.Drawing.Graphics]::FromImage($result)
                        try {
                            $patch.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                            $patch.DrawImage($source,[System.Drawing.Rectangle]::new($cellX+207,$cellY+309,36,105),
                                [System.Drawing.Rectangle]::new($cellX+246,$cellY+309,36,105),[System.Drawing.GraphicsUnit]::Pixel)
                        } finally {$patch.Dispose()}
                    }
                    1 { Erase-Polygon $result $cellX $cellY @(@(343,145),@(431,145),@(431,195),@(358,195),@(343,204)) }
                    2 { Erase-Polygon $result $cellX $cellY @(@(360,116),@(512,116),@(512,223),@(360,206)) }
                    3 { Erase-Polygon $result $cellX $cellY @(@(278,276),@(375,143),@(412,143),@(320,286)) }
                    4 { Erase-Polygon $result $cellX $cellY @(@(208,77),@(300,16),@(329,20),@(235,137),@(209,125)) }
                    5 { Erase-Polygon $result $cellX $cellY @(@(343,250),@(512,250),@(512,315),@(343,315)) }
                } }
                if(-not $HeadOnly -and $frame -eq 4) {
                    # Catch small cyan fragments of the baked overhead blade without cutting the raised fist.
                    for($ly=20;$ly -lt 145;$ly++) { for($lx=175;$lx -lt 330;$lx++) {
                        $c=$source.GetPixel($cellX+$lx,$cellY+$ly)
                        if($c.A -lt 80 -or $c.R -gt 120 -or $c.G -lt 110 -or $c.B -lt $c.R+35) { continue }
                        for($oy=-3;$oy -le 3;$oy++) { for($ox=-3;$ox -le 3;$ox++) {
                            $xx=$lx+$ox;$yy=$ly+$oy
                            if($xx -lt 210 -and $yy -gt 90) { continue }
                            $result.SetPixel($cellX+$xx,$cellY+$yy,[System.Drawing.Color]::FromArgb(0,0,0,0))
                        } }
                    } }
                }
            }
            $path=Join-Path $outputDir "Player_c$style.png"
            $result.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
            Write-Output $path
        } finally {$result.Dispose()}
    } finally {$source.Dispose()}
}
