$ErrorActionPreference='Stop'
$root=Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity=Get-ThreadZeroUnity -ProjectRoot $root
if(Get-Process Unity -ErrorAction SilentlyContinue) {
 Write-Host 'In the open Unity editor choose: Thread Zero > Review Starter Dagger (Unsaved).'
 Read-Host 'Press Enter to close';exit
}
$reviewArgs=@('-projectPath',('"'+(Join-Path $root 'ThreadZero')+'"'),'-executeMethod','ThreadZero.Editor.DaggerReviewMenu.Review')
Start-Process -FilePath $unity.Editor -ArgumentList $reviewArgs
