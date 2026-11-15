$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$monoRoot = $unity.MonoRoot
$artifactDirectory = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$renderer = Join-Path $artifactDirectory 'UiReviewRenderer.exe'
& (Join-Path $monoRoot 'bin\mono.exe') (Join-Path $monoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /main:UiReviewRenderer /reference:System.Drawing.dll /out:$renderer (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\PaperDollLayout.cs') (Join-Path $PSScriptRoot 'DollPreviewRenderer.cs') (Join-Path $PSScriptRoot 'UiReviewRenderer.cs')
if ($LASTEXITCODE -ne 0) { throw 'UI layout review compilation failed' }
& (Join-Path $monoRoot 'bin\mono.exe') $renderer (Join-Path $projectRoot 'ThreadZero\Assets\Resources\Art\RainRunnerAtlas.png') (Join-Path $artifactDirectory 'ActionUiReview.png')
if ($LASTEXITCODE -ne 0) { throw 'UI layout review rendering failed' }
