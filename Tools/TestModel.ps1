$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$monoRoot = $unity.MonoRoot
$artifactDirectory = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$testExe = Join-Path $artifactDirectory 'ModelTests.exe'
& (Join-Path $monoRoot 'bin\mono.exe') (Join-Path $monoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /reference:System.Runtime.Serialization.dll /out:$testExe (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\Balance.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\GameModel.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\CombatSession.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\SaveService.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\DollAnimation.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\DaggerSkill.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\PaperDollLayout.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\SpriteAtlasLayout.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\PuppetGeometry.cs') (Join-Path $projectRoot 'Tests\ModelTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Model test compilation failed' }
& (Join-Path $monoRoot 'bin\mono.exe') $testExe | Tee-Object -FilePath (Join-Path $artifactDirectory 'ModelTests.log')
if ($LASTEXITCODE -ne 0) { throw 'Model tests failed' }
