param([string]$Root = (Split-Path $PSScriptRoot -Parent),[int]$Style=1,[string]$GunId='w0',[string]$SwordId='w1')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$base=[System.Drawing.Image]::FromFile((Join-Path $Root "ThreadZero/Assets/Resources/Art/V08/AtlasesClean/Player_c$Style.png"))
$head=[System.Drawing.Image]::FromFile((Join-Path $Root 'ThreadZero/Assets/Resources/Art/V08/HeadOverlay/h0.png'))
$gun=[System.Drawing.Image]::FromFile((Join-Path $Root "ThreadZero/Assets/Resources/Art/V08/Weapon/$GunId.png"))
$sword=[System.Drawing.Image]::FromFile((Join-Path $Root "ThreadZero/Assets/Resources/Art/V08/Weapon/$SwordId.png"))
try {
  $canvas=[System.Drawing.Bitmap]::new(1536,1024,[System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $g=[System.Drawing.Graphics]::FromImage($canvas)
  try {
    $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#F3F3EF'))
    $dx=@(0,12,12,0,18,42); $dy=@(12,28,28,28,24,45)
    $gx=@(214,347,347,282,185,350);$gy=@(343,195,195,275,120,285)
    $size=@(220,310,310,390,380,450);$angle=@(88,0,0,-15,-35,15)
    for($frame=0;$frame -lt 6;$frame++) {
      $x=($frame%3)*512; $y=[int][Math]::Floor($frame/3)*512
      $src=[System.Drawing.Rectangle]::new($x,$y,512,512)
      $g.DrawImage($base,[System.Drawing.Rectangle]::new($x,$y,512,512),$src,[System.Drawing.GraphicsUnit]::Pixel)
      $g.DrawImage($head,[System.Drawing.Rectangle]::new([int]($x+$dx[$frame]),[int]($y+$dy[$frame]),512,512))
      if($frame -ge 0) {
        $old=$g.Transform.Clone()
        try {
          $g.TranslateTransform($x+$gx[$frame],$y+$gy[$frame])
          $g.RotateTransform($angle[$frame])
          $variant= $(if($frame -lt 3){if($GunId -eq 'w2'){1.04}elseif($GunId -eq 'w3'){.96}else{1}}else{if($SwordId -eq 'w4'){.94}else{1}})
          $s=[int]($size[$frame]*$variant)
          $g.DrawImage($(if($frame -lt 3){$gun}else{$sword}),[System.Drawing.Rectangle]::new([int](-$s/2),[int](-$s/2),$s,$s))
        } finally {$g.Transform=$old;$old.Dispose()}
      }
    }
    $out=Join-Path $Root "Deliverables/ArtPack_v08/WeaponSixPreview_c$Style`_$GunId`_$SwordId.png"
    $canvas.Save($out,[System.Drawing.Imaging.ImageFormat]::Png)
    Write-Output $out
  } finally {$g.Dispose();$canvas.Dispose()}
} finally {$base.Dispose();$head.Dispose();$gun.Dispose();$sword.Dispose()}
