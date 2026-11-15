param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$sourceDir = Join-Path $Root 'Deliverables/ArtPack_v08/Source'
$targets = @(
    (Join-Path $Root 'Deliverables/ArtPack_v08/Runtime/Weapon'),
    (Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/Weapon')
)
foreach ($target in $targets) { New-Item -ItemType Directory -Path $target -Force | Out-Null }

function Export-Weapon([string]$id,[double]$gripX,[double]$gripY,[int]$width) {
    $source = [System.Drawing.Image]::FromFile((Join-Path $sourceDir "Weapon_$id.png"))
    try {
        $canvas = [System.Drawing.Bitmap]::new(512,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        try {
            $g = [System.Drawing.Graphics]::FromImage($canvas)
            try {
                $g.Clear([System.Drawing.Color]::Transparent)
                $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
                # Weapon grip is fixed at (256,256); each body pose later supplies its grip transform.
                $height = [int][Math]::Round($source.Height * $width / $source.Width)
                $left = [int][Math]::Round(256 - $gripX * $width)
                $top = [int][Math]::Round(256 - $gripY * $height)
                $g.DrawImage($source,[System.Drawing.Rectangle]::new($left,$top,$width,$height))
            } finally { $g.Dispose() }
            foreach($dir in $targets) {
                $path=Join-Path $dir "$id.png"
                $canvas.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
                Write-Output $path
            }
        } finally { $canvas.Dispose() }
    } finally { $source.Dispose() }
}

Export-Weapon 'w0' .40 .60 270
Export-Weapon 'w1' .17 .75 270
Export-Weapon 'w2' .36 .72 300
Export-Weapon 'w3' .38 .75 280
Export-Weapon 'w4' .17 .75 300
