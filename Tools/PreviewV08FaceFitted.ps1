param([string]$Root=(Split-Path $PSScriptRoot -Parent),[int]$Style=1)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$source=[System.Drawing.Bitmap]::new((Join-Path $Root "ThreadZero/Assets/Resources/Art/V07/Atlases/Player_c$Style.png"))
$mask=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Intermediate/FaceOnly/_default.png'))
try {
    $canvas=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g=[System.Drawing.Graphics]::FromImage($canvas)
    try {
        $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F3F3EF'))
        $dx=@(0,10,10,0,14,36);$dy=@(0,2,2,5,8,25)
        for($f=0;$f -lt 6;$f++) {
            $cx=($f%3)*512;$cy=[int][Math]::Floor($f/3)*512
            $g.DrawImage($source,[System.Drawing.Rectangle]::new($cx,$cy,512,512),
                [System.Drawing.Rectangle]::new($cx,$cy,512,512),[System.Drawing.GraphicsUnit]::Pixel)
            $g.DrawImage($mask,[System.Drawing.Rectangle]::new($cx+$dx[$f],$cy+$dy[$f],512,512))
            # Restore the original gray fringe in front of the upper visor.
            for($y=54;$y -lt 148;$y++) { for($x=170;$x -lt 365;$x++) {
                $c=$source.GetPixel($cx+$x,$cy+$y)
                if($c.A -lt 128) { continue }
                $max=[Math]::Max($c.R,[Math]::Max($c.G,$c.B))
                $min=[Math]::Min($c.R,[Math]::Min($c.G,$c.B))
                if($max -gt 65 -and $max-$min -lt 30) {
                    $brush=[System.Drawing.SolidBrush]::new($c)
                    try {$g.FillRectangle($brush,$cx+$x,$cy+$y,1,1)}finally{$brush.Dispose()}
                }
            } }
        }
        $out=Join-Path $Root "Deliverables/ArtPack_v08/FaceFittedPreview_c$Style.png"
        $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
        Write-Output $out
    } finally {$g.Dispose();$canvas.Dispose()}
} finally {$source.Dispose();$mask.Dispose()}
