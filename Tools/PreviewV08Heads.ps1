param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$out = Join-Path $Root 'Deliverables/ArtPack_v08/HeadPreview.png'
$canvas = [System.Drawing.Bitmap]::new(1024, 768, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g = [System.Drawing.Graphics]::FromImage($canvas)
try {
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $light = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F3F3EF'))
    $dark = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#1C1F22'))
    $ink = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(32,36,40))
    $white = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
    $font = [System.Drawing.Font]::new('Arial', 20)
    try {
        $names = @('DefaultHead.png','HoodHead.png')
        for ($col=0; $col -lt 2; $col++) {
            $source = [System.Drawing.Image]::FromFile((Join-Path $Root "Deliverables/ArtPack_v08/Source/$($names[$col])"))
            try {
                for ($row=0; $row -lt 2; $row++) {
                    $x = $col * 512; $y = $row * 384
                    $g.FillRectangle($(if ($row -eq 0) {$light} else {$dark}), $x, $y, 512, 384)
                    $width=300; $height=[int][Math]::Round($source.Height * $width / $source.Width)
                    $g.DrawImage($source, [System.Drawing.Rectangle]::new(($x+106),($y+48),$width,$height))
                    $label = $(if ($col -eq 0) {'DEFAULT / gray hair'} else {'h0 / hood'})
                    $g.DrawString($label,$font,$(if ($row -eq 0) {$ink} else {$white}),($x+22),($y+20))
                }
            } finally { $source.Dispose() }
        }
        $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
    } finally { $light.Dispose();$dark.Dispose();$ink.Dispose();$white.Dispose();$font.Dispose() }
} finally { $g.Dispose();$canvas.Dispose() }
Write-Output $out
