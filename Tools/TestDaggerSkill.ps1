$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity=Get-ThreadZeroUnity -ProjectRoot $root
$sources=@('Balance.cs','GameModel.cs','CombatSession.cs','DaggerSkill.cs','DollAnimation.cs') | ForEach-Object {Join-Path $root ('ThreadZero\Assets\Scripts\'+$_)}
$exe=Join-Path $root 'Artifacts\DaggerSkillTests.exe'
& (Join-Path $unity.MonoRoot 'bin\mono.exe') (Join-Path $unity.MonoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /out:$exe @sources (Join-Path $root 'Tests\DaggerSkillTests.cs')
if($LASTEXITCODE -ne 0){throw 'Dagger test compile failed'}
& (Join-Path $unity.MonoRoot 'bin\mono.exe') $exe | Tee-Object -FilePath (Join-Path $root 'Artifacts\DaggerSkillTests.log')
if($LASTEXITCODE -ne 0){throw 'Dagger tests failed'}
