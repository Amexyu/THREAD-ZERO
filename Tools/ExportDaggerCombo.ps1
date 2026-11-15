Add-Type -AssemblyName System.Drawing
$review = Join-Path $PSScriptRoot '..\Deliverables\ArtPack_v12\Review'
$frames = Join-Path $review 'Combo6Frames'
New-Item -ItemType Directory -Path $frames -Force | Out-Null
$source = [System.Drawing.Bitmap]::FromFile((Join-Path $review 'Dagger_Combo6.png'))
$shiftX = @(-10,7,69,14,35,73)
try {
    for ($i = 0; $i -lt 6; $i++) {
        $canvas = New-Object System.Drawing.Bitmap 640,560
        $g = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $g.Clear([System.Drawing.Color]::FromArgb(224,221,216))
            $col = $i % 3
            $row = [int][Math]::Floor($i / 3)
            $dy = if ($row -eq 0) { 0 } else { 18 }
            $dest = New-Object System.Drawing.Rectangle (50+$shiftX[$i]),(20+$dy),512,512
            $g.DrawImage($source,$dest,($col*512),($row*512),512,512,[System.Drawing.GraphicsUnit]::Pixel)
            $canvas.Save((Join-Path $frames ('pose_{0}.png' -f $i)),[System.Drawing.Imaging.ImageFormat]::Png)
            $canvas.Save((Join-Path $frames ('pose_{0}.gif' -f $i)),[System.Drawing.Imaging.ImageFormat]::Gif)
        } finally { $g.Dispose(); $canvas.Dispose() }
    }
} finally { $source.Dispose() }
python (Join-Path $PSScriptRoot 'AssembleDaggerCombo.py')
