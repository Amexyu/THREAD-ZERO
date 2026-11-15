$ErrorActionPreference='Stop'
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$projectRoot=Split-Path -Parent $PSScriptRoot
$u=Get-ThreadZeroUnity -ProjectRoot $projectRoot
& "$($u.MonoRoot)/bin/mono.exe" "$($u.MonoRoot)/lib/mono/4.5/csc.exe" /nologo /r:System.Drawing.dll /out:Artifacts/PrepareConnectedArt.exe Tools/PrepareConnectedArt.cs ThreadZero/Assets/Scripts/SpriteAtlasLayout.cs
if($LASTEXITCODE -ne 0){throw 'Asset exporter compile failed'}
& ./Artifacts/PrepareConnectedArt.exe Deliverables/ArtPack_v07
if($LASTEXITCODE -ne 0){throw 'Asset exporter failed'}
