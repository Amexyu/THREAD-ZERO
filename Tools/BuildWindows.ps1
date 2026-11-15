$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$lockFile = Join-Path $projectRoot 'ThreadZero\Temp\UnityLockfile'
if (Test-Path -LiteralPath $lockFile) {
    throw 'Project is open in Unity. Build using Thread Zero > Build Windows in the existing editor, or close that project before background building.'
}
$artifactDirectory = Join-Path $projectRoot 'Artifacts'
New-Item -ItemType Directory -Path $artifactDirectory -Force | Out-Null
$unityArguments = @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + (Join-Path $projectRoot 'ThreadZero') + '"'), '-executeMethod', 'ThreadZero.Editor.BuildProject.Windows', '-logFile', ('"' + (Join-Path $artifactDirectory 'UnityBuild.log') + '"'))
$buildProcess = Start-Process -FilePath $unity.Editor -ArgumentList $unityArguments -WindowStyle Hidden -PassThru
Write-Output ('Background build process: ' + $buildProcess.Id)
Write-Output ('Log: ' + (Join-Path $artifactDirectory 'UnityBuild.log'))
