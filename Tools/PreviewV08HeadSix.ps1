param([string]$Root = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$base=[System.Drawing.Image]::FromFile((Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/AtlasesClean/Player_c1.png'))
$head=[System.Drawing.Image]::FromFile((Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/HeadOverlay/h0.png'))
try {
  $canvas=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($canvas)
  try {
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F3F3EF'))
    $dx=@(0,12,12,0,18,42); $dy=@(0,28,28,28,24,45)
    for($frame=0;$frame -lt 6;$frame++) {
      $x=($frame%3)*512; $y=[int][Math]::Floor($frame/3)*512
      $src=[System.Drawing.Rectangle]::new($x,$y,512,512)
      $g.DrawImage($base,[System.Drawing.Rectangle]::new($x,$y,512,512),$src,[System.Drawing.GraphicsUnit]::Pixel)
      $g.DrawImage($head,[System.Drawing.Rectangle]::new([int]($x+$dx[$frame]),[int]($y+$dy[$frame]),512,512))
    }
    $out=Join-Path $Root 'Deliverables/ArtPack_v08/HeadSixPreview.png'
    $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output $out
  } finally {$g.Dispose();$canvas.Dispose()}
} finally {$base.Dispose();$head.Dispose()}
