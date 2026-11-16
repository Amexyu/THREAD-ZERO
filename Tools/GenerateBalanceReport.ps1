$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$monoRoot = $unity.MonoRoot
$artifactDirectory = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$reportExe = Join-Path $artifactDirectory 'BalanceReport.exe'
& (Join-Path $monoRoot 'bin\mono.exe') (Join-Path $monoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /out:$reportExe (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\Balance.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\GameModel.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\CombatSession.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\EncounterMovement.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\DaggerSkill.cs') (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\DollAnimation.cs') (Join-Path $projectRoot 'Tools\BalanceReport.cs')
if ($LASTEXITCODE -ne 0) { throw 'Balance report compilation failed' }
& (Join-Path $monoRoot 'bin\mono.exe') $reportExe (Join-Path $projectRoot 'Docs\BalanceReport.md')
if ($LASTEXITCODE -ne 0) { throw 'Balance report simulation failed' }
