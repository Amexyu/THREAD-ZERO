$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
. (Join-Path $PSScriptRoot 'GetUnity.ps1')
$unity = Get-ThreadZeroUnity -ProjectRoot $projectRoot
$testExe = Join-Path $projectRoot 'Artifacts/InventoryQueryTests.exe'
$files = @('ThreadZero/Assets/Scripts/Balance.cs', 'ThreadZero/Assets/Scripts/GameModel.cs', 'ThreadZero/Assets/Scripts/InventoryQuery.cs', 'Tests/InventoryQueryTests.cs') | ForEach-Object { Join-Path $projectRoot $_ }
& (Join-Path $unity.MonoRoot 'bin/mono.exe') (Join-Path $unity.MonoRoot 'lib/mono/4.5/csc.exe') /nologo /target:exe /out:$testExe @files
if ($LASTEXITCODE -ne 0) { throw 'Inventory test compilation failed' }
& $testExe | Tee-Object -FilePath (Join-Path $projectRoot 'Artifacts/InventoryQueryTests.log')
if ($LASTEXITCODE -ne 0) { throw 'Inventory tests failed' }
