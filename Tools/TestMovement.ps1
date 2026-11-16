$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity=Get-ThreadZeroUnity -ProjectRoot $root
New-Item -ItemType Directory -Path (Join-Path $root 'Artifacts') -Force | Out-Null
$sources=@('Balance.cs','GameModel.cs','CombatSession.cs','EncounterMovement.cs','DaggerSkill.cs','DollAnimation.cs') | ForEach-Object {Join-Path $root ('ThreadZero\Assets\Scripts\'+$_)}
$exe=Join-Path $root 'Artifacts\MovementTests.exe'
& (Join-Path $unity.MonoRoot 'bin\mono.exe') (Join-Path $unity.MonoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /out:$exe @sources (Join-Path $root 'Tests\MovementTests.cs')
if($LASTEXITCODE -ne 0){throw 'Movement test compile failed'}
& (Join-Path $unity.MonoRoot 'bin\mono.exe') $exe | Tee-Object -FilePath (Join-Path $root 'Artifacts\MovementTests.log')
if($LASTEXITCODE -ne 0){throw 'Movement tests failed'}
