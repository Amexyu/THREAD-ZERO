$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity=Get-ThreadZeroUnity -ProjectRoot $root
$sources=@('Balance.cs','GameModel.cs','CombatSession.cs','EncounterMovement.cs','DaggerSkill.cs','DollAnimation.cs') | ForEach-Object {Join-Path $root ('ThreadZero\Assets\Scripts\'+$_)}
$exe=Join-Path $root 'Artifacts\MovementReview.exe'
& (Join-Path $unity.MonoRoot 'bin\mono.exe') (Join-Path $unity.MonoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /reference:System.Drawing.dll /out:$exe @sources (Join-Path $root 'Tools\RenderMovementReview.cs')
if($LASTEXITCODE -ne 0){throw 'Movement render compile failed'}
& (Join-Path $unity.MonoRoot 'bin\mono.exe') $exe $root
if($LASTEXITCODE -ne 0){throw 'Movement render failed'}
python (Join-Path $root 'Tools\AssembleAnimationGif.py') --movement
