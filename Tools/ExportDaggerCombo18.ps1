param([switch]$V2)
Add-Type -AssemblyName System.Drawing
$review = Join-Path $PSScriptRoot '..\Deliverables\ArtPack_v12\Review'
$frames = Join-Path $review $(if ($V2) {'Combo18v2Frames'} else {'Combo18Frames'})
New-Item -ItemType Directory -Path $frames -Force | Out-Null
$source = [System.Drawing.Bitmap]::FromFile((Join-Path $review $(if ($V2) {'Dagger_Combo18_v2.png'} else {'Dagger_Combo18.png'})))
try {
    for ($i=0; $i -lt 18; $i++) {
        $col=$i % 6; $row=[int][Math]::Floor($i/6)
        $sx=[int][Math]::Round($col*$source.Width/6.0)
        $sy=[int][Math]::Round($row*$source.Height/3.0)
        $right=[int][Math]::Round(($col+1)*$source.Width/6.0)
        $bottom=[int][Math]::Round(($row+1)*$source.Height/3.0)
        $canvas=New-Object System.Drawing.Bitmap 640,560
        $g=[System.Drawing.Graphics]::FromImage($canvas)
        try {
            $g.Clear($source.GetPixel($sx+4,$sy+4))
            $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            # Uniform scale for every pose; slight row baseline correction only.
            $dy=if ($V2) {@(6,9,8)[$row]} else {42+$row*2}
            $dest=New-Object System.Drawing.Rectangle 50,$dy,503,503
            $g.DrawImage($source,$dest,$sx,$sy,($right-$sx),($bottom-$sy),[System.Drawing.GraphicsUnit]::Pixel)
            $canvas.Save((Join-Path $frames ('pose_{0:D2}.png' -f $i)),[System.Drawing.Imaging.ImageFormat]::Png)
            $canvas.Save((Join-Path $frames ('pose_{0:D2}.gif' -f $i)),[System.Drawing.Imaging.ImageFormat]::Gif)
        } finally { $g.Dispose(); $canvas.Dispose() }
    }
} finally { $source.Dispose() }
if ($V2) { python (Join-Path $PSScriptRoot 'AssembleDaggerCombo.py') --combo18 --v2 }
else { python (Join-Path $PSScriptRoot 'AssembleDaggerCombo.py') --combo18 }
