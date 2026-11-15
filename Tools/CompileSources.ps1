$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$editorData = $unity.Data
$monoRoot = Join-Path $editorData 'MonoBleedingEdge'
$managed = Join-Path $editorData 'Managed\UnityEngine'
$artifactDirectory = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$frameworkReferences = @(Get-ChildItem -LiteralPath (Join-Path $editorData 'NetStandard\ref\2.1.0') -Filter '*.dll' | ForEach-Object { '/reference:' + $_.FullName })
$engineNames = @('UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll', 'UnityEngine.AudioModule.dll', 'UnityEngine.JSONSerializeModule.dll', 'UnityEngine.TextRenderingModule.dll', 'UnityEngine.SharedInternalsModule.dll', 'UnityEngine.InputLegacyModule.dll', 'UnityEngine.ImageConversionModule.dll', 'UnityEngine.ScreenCaptureModule.dll')
$references = @($engineNames | ForEach-Object { $assemblyPath = Join-Path $managed $_; if (Test-Path -LiteralPath $assemblyPath) { '/reference:' + $assemblyPath } })
$sources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ThreadZero\Assets\Scripts') -Filter '*.cs' | ForEach-Object { $_.FullName })
$runtimeDll = Join-Path $artifactDirectory 'RuntimeCompile.dll'
$log = Join-Path $artifactDirectory 'SourceCompile.log'
& (Join-Path $monoRoot 'bin\mono.exe') (Join-Path $monoRoot 'lib\mono\4.5\csc.exe') /nologo /noconfig /nostdlib+ /target:library /out:$runtimeDll @frameworkReferences @references @sources 2>&1 | Tee-Object -FilePath $log
if ($LASTEXITCODE -ne 0) { throw 'Runtime source compilation failed' }
$editorReferences = @((Get-ChildItem -LiteralPath $managed -Filter 'UnityEditor*.dll' | ForEach-Object { '/reference:' + $_.FullName }))
$editorSources = @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'ThreadZero\Assets\Editor') -Filter '*.cs' | ForEach-Object { $_.FullName })
$editorDll = Join-Path $artifactDirectory 'EditorCompile.dll'
& (Join-Path $monoRoot 'bin\mono.exe') (Join-Path $monoRoot 'lib\mono\4.5\csc.exe') /nologo /noconfig /nostdlib+ /target:library /out:$editorDll @frameworkReferences @references @editorReferences /reference:$runtimeDll @editorSources 2>&1 | Tee-Object -FilePath $log -Append
if ($LASTEXITCODE -ne 0) { throw 'Editor source compilation failed' }
'Runtime and editor source compilation: PASS' | Tee-Object -FilePath $log -Append
