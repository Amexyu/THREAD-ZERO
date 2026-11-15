$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$u=Get-ThreadZeroUnity -ProjectRoot $projectRoot
& "$($u.MonoRoot)/bin/mono.exe" "$($u.MonoRoot)/lib/mono/4.5/csc.exe" /nologo /r:System.Drawing.dll /out:Artifacts/ReviewV5.exe Tools/ReviewV5.cs ThreadZero/Assets/Scripts/PuppetGeometry.cs ThreadZero/Assets/Scripts/DollAnimation.cs ThreadZero/Assets/Scripts/SpriteAtlasLayout.cs
if($LASTEXITCODE -ne 0){throw 'Preview compilation failed'}
& ./Artifacts/ReviewV5.exe ThreadZero/Assets/Resources/Art Artifacts
if($LASTEXITCODE -ne 0){throw 'Preview rendering failed'}
python Tools/AssembleAnimationGif.py
if($LASTEXITCODE -ne 0){throw 'Animation assembly failed'}
