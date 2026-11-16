$ErrorActionPreference = 'Stop'
$repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$manifest = Join-Path $repo 'Deliverables\ArtPack_v13\Character\starter\parts.json'
$out = Join-Path $repo 'Deliverables\ArtPack_v13\Review\S1'
$localDeps = Join-Path (Split-Path $repo -Parent) 'runtime_deps'
$previousPath = $env:PYTHONPATH
$previousEncoding = $env:PYTHONIOENCODING
try {
    if (Test-Path -LiteralPath $localDeps) {
        $env:PYTHONPATH = $localDeps + [IO.Path]::PathSeparator + $previousPath
    }
    $env:PYTHONIOENCODING = 'utf-8'
    New-Item -ItemType Directory -Path $out -Force | Out-Null
    & python (Join-Path $PSScriptRoot 'validate_parts.py') $manifest 2>&1 |
        Tee-Object -FilePath (Join-Path $out 'validate_parts.log')
    if ($LASTEXITCODE -ne 0) { throw 'Part validation failed.' }
    & python (Join-Path $PSScriptRoot 'review_starter.py') $manifest $out 2>&1 |
        Tee-Object -FilePath (Join-Path $out 'rest.log')
    if ($LASTEXITCODE -ne 0) { throw 'Rest-pose measurements failed.' }
    & python (Join-Path $PSScriptRoot 'rig.py') $out $manifest 2>&1 |
        Tee-Object -FilePath (Join-Path $out 'rig.log')
    if ($LASTEXITCODE -ne 0) { throw 'Rig rendering failed.' }
} finally {
    $env:PYTHONPATH = $previousPath
    $env:PYTHONIOENCODING = $previousEncoding
}
