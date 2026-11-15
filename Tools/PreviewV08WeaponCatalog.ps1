param([string]$Root=(Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$canvas=[System.Drawing.Bitmap]::new(1500,600,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g=[System.Drawing.Graphics]::FromImage($canvas)
try {
  $g.InterpolationMode=[System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
  $light=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#F3F3EF'))
  $dark=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml('#1C1F22'))
  $font=[System.Drawing.Font]::new('Arial',20)
  try {
    for($row=0;$row -lt 2;$row++) {
      $g.FillRectangle($(if($row -eq 0){$light}else{$dark}),0,$row*300,1500,300)
      for($i=0;$i -lt 5;$i++) {
        $id="w$i";$source=[System.Drawing.Image]::FromFile((Join-Path $Root "Deliverables/ArtPack_v08/Source/Weapon_$id.png"))
        try {
          $scale=[Math]::Min(276/$source.Width,210/$source.Height)
          $w=[int]($source.Width*$scale);$h=[int]($source.Height*$scale)
          $g.DrawImage($source,[System.Drawing.Rectangle]::new(($i*300+[int]((300-$w)/2)),($row*300+38+[int]((210-$h)/2)),$w,$h))
          $brush=$(if($row -eq 0){[System.Drawing.Brushes]::Black}else{[System.Drawing.Brushes]::White})
          $g.DrawString($id,$font,$brush,($i*300+18),($row*300+8))
        } finally {$source.Dispose()}
      }
    }
    $out=Join-Path $Root 'Deliverables/ArtPack_v08/WeaponCatalogPreview.png'
    $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output $out
  } finally {$light.Dispose();$dark.Dispose();$font.Dispose()}
} finally {$g.Dispose();$canvas.Dispose()}
