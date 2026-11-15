$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$source = Join-Path $root 'ThreadZero/Assets/Resources/Art/EquipmentAtlas.png'
$output = Join-Path $root 'Deliverables/ArtPack_v08/Runtime/Icons'
$unity = Join-Path $root 'ThreadZero/Assets/Resources/Art/V08/Icons'
New-Item -ItemType Directory -Force $output,$unity | Out-Null
$cells = @(
 @(23,55,237,182),@(298,9,237,265),@(560,71,281,138),@(856,70,260,163),@(1141,11,246,262),
 @(31,286,219,259),@(299,338,240,168),@(569,334,265,146),@(879,289,216,237),@(1162,293,219,230),
 @(16,565,246,242),@(307,561,209,266),@(567,562,252,241),@(863,561,233,254),@(1158,561,208,266),
 @(19,852,240,241),@(306,841,212,257),@(573,846,234,245),@(852,853,255,246),@(1146,847,238,240)
)
$atlas = [Drawing.Bitmap]::FromFile($source)
try {
    for ($i = 0; $i -lt 20; $i++) {
        $cell = $cells[$i]
        $id = @('w','h','c','b')[[int][Math]::Floor($i/5)] + ($i % 5)
        $scale = [Math]::Min(240.0 / $cell[2], 240.0 / $cell[3])
        $w = [int][Math]::Round($cell[2] * $scale)
        $h = [int][Math]::Round($cell[3] * $scale)
        $bitmap = New-Object Drawing.Bitmap(256,256,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g = [Drawing.Graphics]::FromImage($bitmap)
        try {
            $g.Clear([Drawing.Color]::Transparent)
            $g.InterpolationMode = [Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $src = New-Object Drawing.Rectangle($cell[0],$cell[1],$cell[2],$cell[3])
            $dst = New-Object Drawing.Rectangle([int][Math]::Round((256-$w)/2),[int][Math]::Round((256-$h)/2),$w,$h)
            $g.DrawImage($atlas,$dst,$src,[Drawing.GraphicsUnit]::Pixel)
        } finally { $g.Dispose() }
        try {
            $path = Join-Path $output ($id + '.png')
            $bitmap.Save($path,[Drawing.Imaging.ImageFormat]::Png)
            Copy-Item -LiteralPath $path -Destination (Join-Path $unity ($id + '.png')) -Force
        } finally { $bitmap.Dispose() }
    }
} finally { $atlas.Dispose() }
Write-Output 'Exported 20 equipment icons at 256x256 into v08 deliverable and Unity resources.'
