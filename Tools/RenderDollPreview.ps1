$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
& python (Join-Path $PSScriptRoot 'MakeDollPreview.py')
if ($LASTEXITCODE -ne 0) { throw 'Preview source generation failed' }
$artifactDirectory = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$outputImage = Join-Path $artifactDirectory 'DollPreview.png'
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$monoRoot = $unity.MonoRoot
$previewExe = Join-Path $artifactDirectory 'DollPreviewRenderer.exe'
& (Join-Path $monoRoot 'bin\mono.exe') (Join-Path $monoRoot 'lib\mono\4.5\csc.exe') /nologo /target:exe /reference:System.Drawing.dll /out:$previewExe (Join-Path $projectRoot 'ThreadZero\Assets\Scripts\PaperDollLayout.cs') (Join-Path $PSScriptRoot 'DollPreviewRenderer.cs')
if ($LASTEXITCODE -ne 0) { throw 'Software preview renderer compilation failed' }
& (Join-Path $monoRoot 'bin\mono.exe') $previewExe (Join-Path $projectRoot 'ThreadZero\Assets\Resources\Art\RainRunnerAtlas.png') $outputImage
if ($LASTEXITCODE -ne 0) { throw 'Software preview rendering failed' }
Write-Output ('Preview: '+$outputImage)
