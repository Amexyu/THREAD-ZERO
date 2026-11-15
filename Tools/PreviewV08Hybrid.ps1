param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$base=[System.Drawing.Image]::FromFile((Join-Path $Root 'ThreadZero/Assets/Resources/Art/V07/Atlases/Player_c0.png'))
$head=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Runtime/Head/h0.png'))
$weapon=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Runtime/Weapon/w0.png'))
$back=[System.Drawing.Image]::FromFile((Join-Path $Root 'Deliverables/ArtPack_v08/Runtime/Back/b0.png'))
try {
  $canvas=[System.Drawing.Bitmap]::new(1024,512,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($canvas)
  try {
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F3F3EF'))
    for($frame=0;$frame -lt 2;$frame++) {
      $x=$frame*512
      $g.DrawImage($back,[System.Drawing.Rectangle]::new($x,0,512,512))
      $src=[System.Drawing.Rectangle]::new($frame*512,0,512,512)
      $g.DrawImage($base,[System.Drawing.Rectangle]::new($x,0,512,512),$src,[System.Drawing.GraphicsUnit]::Pixel)
      $headOffset= $(if($frame -eq 0){0}else{12})
      $headY= $(if($frame -eq 0){0}else{28})
      $g.DrawImage($head,[System.Drawing.Rectangle]::new([int]($x-166+$headOffset),$headY,845,845))
      if($frame -eq 1){$g.DrawImage($weapon,[System.Drawing.Rectangle]::new($x+185,65,310,310))}
    }
    $out=Join-Path $Root 'Deliverables/ArtPack_v08/HybridPreview.png'
    $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output $out
  } finally {$g.Dispose();$canvas.Dispose()}
} finally {$base.Dispose();$head.Dispose();$weapon.Dispose();$back.Dispose()}
