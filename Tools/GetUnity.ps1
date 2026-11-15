function Get-ThreadZeroUnity {
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    $versionFile = Join-Path $ProjectRoot 'ThreadZero\ProjectSettings\ProjectVersion.txt'
    $versionText = [IO.File]::ReadAllText($versionFile)
    $versionMatch = [regex]::Match($versionText, '(?m)^m_EditorVersion:\s*(\S+)')
    if (-not $versionMatch.Success) { throw 'ProjectVersion.txt does not contain an editor version' }
    $version = $versionMatch.Groups[1].Value
    if ($version -notmatch '^\d{4}\.\d+\.\d+[abfp]\d+$') { throw 'Unexpected Unity version format' }
    $candidates = @(
        (Join-Path 'G:\Unity' ($version + '\Editor\Unity.exe')),
        (Join-Path $env:ProgramFiles ('Unity\Hub\Editor\' + $version + '\Editor\Unity.exe'))
    )
    $editor = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $editor) { throw ('Cannot find installed Unity ' + $version + '. Expected: ' + ($candidates -join ', ')) }
    $data = Join-Path (Split-Path -Parent $editor) 'Data'
    [pscustomobject]@{ Version = $version; Editor = $editor; Data = $data; MonoRoot = (Join-Path $data 'MonoBleedingEdge') }
}
