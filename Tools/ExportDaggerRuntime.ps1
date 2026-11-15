$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent $PSScriptRoot
$output=Join-Path $root 'Deliverables\ArtPack_v12\Runtime\StarterDagger'
$runtime=Join-Path $root 'ThreadZero\Assets\Resources\Art\V12\StarterDagger'
New-Item -ItemType Directory -Path $output,$runtime -Force | Out-Null
$src=[System.Drawing.Bitmap]::FromFile((Join-Path $root 'Deliverables\ArtPack_v12\Review\Dagger_Combo18_Alpha.png'))
$atlas=New-Object System.Drawing.Bitmap 1536,768
$body=New-Object System.Drawing.Bitmap 1536,768
$weapon=New-Object System.Drawing.Bitmap 1536,768
# Exposed blade start/tip in each 296px source cell. The fist occludes the handle.
$starts=@(@(169,155),@(166,130),@(126,103),@(190,117),@(214,132),@(207,162),@(186,149),@(197,126),@(207,99),@(160,149),@(151,161),@(146,161),@(165,147),@(202,131),@(235,131),@(165,143),@(172,154),@(170,156))
$tips=@(@(214,162),@(211,136),@(164,108),@(232,121),@(257,138),@(249,167),@(227,157),@(239,115),@(243,80),@(203,136),@(195,162),@(189,163),@(205,148),@(244,136),@(276,138),@(204,146),@(214,159),@(213,162))
$alphaMin=255; $alphaMax=0; $transparent=0; $mismatch=0
try {
 $ga=[System.Drawing.Graphics]::FromImage($atlas)
 $ga.CompositingMode=[System.Drawing.Drawing2D.CompositingMode]::SourceCopy
 for($i=0;$i -lt 18;$i++) {
  $col=$i%6; $row=[int][Math]::Floor($i/6)
  $sx=[int][Math]::Round($col*$src.Width/6.0); $sy=[int][Math]::Round($row*$src.Height/3.0)
  $sw=[int][Math]::Round(($col+1)*$src.Width/6.0)-$sx; $sh=[int][Math]::Round(($row+1)*$src.Height/3.0)-$sy
  $dest=New-Object System.Drawing.Rectangle ($col*256),($row*256),256,256
  $ga.DrawImage($src,$dest,$sx,$sy,$sw,$sh,[System.Drawing.GraphicsUnit]::Pixel)
 }
 $ga.Dispose()
 # Exact pixel partition; no fabricated body pixels, cloned hands, or rotated weapon sprites.
 for($i=0;$i -lt 18;$i++) {
  $cx=($i%6)*256; $cy=[int][Math]::Floor($i/6)*256
  $ax=$starts[$i][0]*256.0/296; $ay=$starts[$i][1]*256.0/296
  $bx=$tips[$i][0]*256.0/296; $by=$tips[$i][1]*256.0/296
  $dx=$bx-$ax; $dy=$by-$ay; $len=[Math]::Sqrt($dx*$dx+$dy*$dy)
  for($y=0;$y -lt 256;$y++) {for($x=0;$x -lt 256;$x++) {
   $p=$atlas.GetPixel($cx+$x,$cy+$y)
   if($p.A -lt $alphaMin){$alphaMin=$p.A}; if($p.A -gt $alphaMax){$alphaMax=$p.A}; if($p.A -eq 0){$transparent++}
   $along=(($x-$ax)*$dx+($y-$ay)*$dy)/$len
   $perp=[Math]::Abs((($x-$ax)*$dy-($y-$ay)*$dx)/$len)
   $isBlade=$along -ge 0 -and $along -le ($len+3) -and $perp -le 7
   if($isBlade){$weapon.SetPixel($cx+$x,$cy+$y,$p)} else {$body.SetPixel($cx+$x,$cy+$y,$p)}
  }}
 }
 if($alphaMin -ne 0 -or $alphaMax -ne 255 -or $transparent -lt 500000){throw 'Source alpha validation failed'}
 $atlas.Save((Join-Path $output 'Composite.png'),[System.Drawing.Imaging.ImageFormat]::Png)
 $body.Save((Join-Path $output 'Body.png'),[System.Drawing.Imaging.ImageFormat]::Png)
 $weapon.Save((Join-Path $output 'Weapon.png'),[System.Drawing.Imaging.ImageFormat]::Png)
 # Two-background QA of actual runtime layers, covering cuts and the final thrust.
 $qa=New-Object System.Drawing.Bitmap 1536,512
 $g=[System.Drawing.Graphics]::FromImage($qa)
 for($r=0;$r -lt 2;$r++) {
  $bg=if($r -eq 0){[System.Drawing.Color]::FromArgb(243,243,239)}else{[System.Drawing.Color]::FromArgb(28,31,34)}
  $brush=New-Object System.Drawing.SolidBrush $bg; $g.FillRectangle($brush,0,($r*256),1536,256); $brush.Dispose()
  $indices=@(0,4,5,8,14,17)
  for($n=0;$n -lt 6;$n++) {
   $i=$indices[$n]; $rect=New-Object System.Drawing.Rectangle ($n*256),($r*256),256,256
   foreach($layer in @($body,$weapon)) {$g.DrawImage($layer,$rect,(($i%6)*256),([int][Math]::Floor($i/6)*256),256,256,[System.Drawing.GraphicsUnit]::Pixel)}
  }
 }
 $qa.Save((Join-Path $root 'Deliverables\ArtPack_v12\Review\RuntimeLayers_QA.png')); $g.Dispose(); $qa.Dispose()
 foreach($name in @('Body.png','Weapon.png')) {Copy-Item -LiteralPath (Join-Path $output $name) -Destination (Join-Path $runtime $name) -Force}
 Write-Output "Runtime atlas: 1536x768, 18 cells, alpha=$alphaMin..$alphaMax, transparent=$transparent"
} finally {$src.Dispose();$atlas.Dispose();$body.Dispose();$weapon.Dispose()}
