[CmdletBinding()]
param([switch]$Apply)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# This script never creates a Unity project, resolves packages or launches Unity.
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$gameRoot = Join-Path $repoRoot 'Game'
$templateRoot = Join-Path $PSScriptRoot 'Template'
$requiredVersion = '6000.3.28f1'

function Assert-NoReparsePoint([string]$PathToCheck) {
    $cursor = [IO.Path]::GetFullPath($PathToCheck)
    while ($cursor.StartsWith($repoRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $cursor -eq $repoRoot) {
        if (Test-Path -LiteralPath $cursor) {
            $item = Get-Item -LiteralPath $cursor -Force
            if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Refusing linked path: $cursor"
            }
        }
        if ($cursor -eq $repoRoot) { break }
        $cursor = Split-Path -Parent $cursor
    }
}

Assert-NoReparsePoint $PSScriptRoot
Assert-NoReparsePoint $gameRoot
if (-not (Test-Path -LiteralPath $gameRoot -PathType Container)) {
    throw 'Game/ is absent. Create the 2D project in Unity Hub first; see .agent/S1_01_SETUP.md.'
}
foreach ($required in @('Assets', 'Packages', 'ProjectSettings')) {
    if (-not (Test-Path -LiteralPath (Join-Path $gameRoot $required) -PathType Container)) {
        throw "Incomplete Unity project: missing $required"
    }
}
$versionPath = Join-Path $gameRoot 'ProjectSettings\ProjectVersion.txt'
Assert-NoReparsePoint $versionPath
$versionText = Get-Content -LiteralPath $versionPath -Raw
if ($versionText -notmatch ('(?m)^m_EditorVersion:\s*' + [regex]::Escape($requiredVersion) + '\s*$')) {
    throw "Expected candidate $requiredVersion. Inspect the actual version; do not edit ProjectVersion.txt to bypass this check."
}
if (Test-Path -LiteralPath (Join-Path $gameRoot 'Temp\UnityLockfile')) {
    throw 'UnityLockfile exists. Close this project in Unity and retry; do not delete the lock automatically.'
}

$manifestPath = Join-Path $gameRoot 'Packages\manifest.json'
$lockPath = Join-Path $gameRoot 'Packages\packages-lock.json'
Assert-NoReparsePoint $manifestPath
Assert-NoReparsePoint $lockPath
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$packageLock = Get-Content -LiteralPath $lockPath -Raw | ConvertFrom-Json
foreach ($packageId in @('com.unity.inputsystem', 'com.unity.ugui', 'com.unity.test-framework')) {
    $declared = $manifest.dependencies.PSObject.Properties[$packageId]
    $resolved = $packageLock.dependencies.PSObject.Properties[$packageId]
    if ($null -eq $declared -or $null -eq $resolved) {
        throw "Missing declared/resolved package $packageId. Install it in Package Manager and close Unity first."
    }
    Write-Output ("RESOLVED {0} {1}" -f $packageId, $resolved.Value.version)
}

Assert-NoReparsePoint $templateRoot
# Reject linked template directories before recursing through them.
$pendingDirs = [Collections.Generic.Queue[string]]::new()
$pendingDirs.Enqueue($templateRoot)
$sources = @()
while ($pendingDirs.Count -gt 0) {
    $directory = $pendingDirs.Dequeue()
    foreach ($entry in Get-ChildItem -LiteralPath $directory -Force) {
        Assert-NoReparsePoint $entry.FullName
        if ($entry.PSIsContainer) { $pendingDirs.Enqueue($entry.FullName) }
        else { $sources += $entry }
    }
}
if ($sources.Count -eq 0) { throw 'Template is empty.' }

# Complete preflight before the first destination directory/file is created.
$toCopy = @()
foreach ($source in $sources) {
    $relativePath = $source.FullName.Substring($templateRoot.Length + 1)
    $target = [IO.Path]::GetFullPath((Join-Path $gameRoot $relativePath))
    if (-not $target.StartsWith($gameRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Destination outside Game/: $relativePath"
    }
    Assert-NoReparsePoint $target
    if (Test-Path -LiteralPath $target) {
        if (-not (Test-Path -LiteralPath $target -PathType Leaf)) { throw "Not a file: $relativePath" }
        if ((Get-FileHash -LiteralPath $source.FullName).Hash -ne (Get-FileHash -LiteralPath $target).Hash) {
            throw "Existing different file, preserving it: $relativePath"
        }
        Write-Output "UNCHANGED $relativePath"
        continue
    }
    if (Test-Path -LiteralPath ($target + '.meta')) {
        throw "Orphan .meta found, preserving it: $relativePath.meta"
    }
    $toCopy += [pscustomobject]@{ Source = $source.FullName; Target = $target; Relative = $relativePath }
}

foreach ($copy in $toCopy) {
    if (-not $Apply) {
        Write-Output ("WOULD_CREATE " + $copy.Relative)
        continue
    }
    Assert-NoReparsePoint $copy.Target
    [void][IO.Directory]::CreateDirectory((Split-Path -Parent $copy.Target))
    $bytes = [IO.File]::ReadAllBytes($copy.Source)
    $stream = [IO.File]::Open($copy.Target, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length) }
    finally { $stream.Dispose() }
    Write-Output ("CREATED " + $copy.Relative)
}
if ($Apply) {
    Write-Output 'SOURCE_FILES_INSTALLED. Unity must generate .meta and compile. This is not S1-01 acceptance.'
} else {
    Write-Output 'PREFLIGHT_ONLY. No files written. Use -Apply only after reviewing the output.'
}
