# Hard gate: the vendored mirror still holds the bytes the lock recorded.
# Self-contained on purpose — it never reads LumioGameEngineArchitecture, so it
# is a real gate where no sibling checkout exists. Upstream drift is a separate,
# report-only question answered by sync-contract-mirror.ps1 -Check.
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$MirrorDir = "contract-mirror/upstream"
$Lock = "contract-mirror/contract-mirror.sha256"

$ExitTampered = 33
$ExitUsage = 2

if (-not (Test-Path $Lock -PathType Leaf)) {
    Write-Error "contract-mirror: lock file missing: $Lock" -ErrorAction Continue
    exit $ExitUsage
}

if (-not (Test-Path $MirrorDir -PathType Container)) {
    Write-Error "contract-mirror: mirror directory missing: $MirrorDir" -ErrorAction Continue
    exit $ExitUsage
}

$failures = 0
$lockedPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

# 1. every locked path must exist and still hash to the locked value
foreach ($line in Get-Content $Lock) {
    if ([string]::IsNullOrWhiteSpace($line)) { continue }

    $split = $line.IndexOf("  ", [System.StringComparison]::Ordinal)
    if ($split -lt 1) {
        Write-Error "contract-mirror: malformed lock line: $line" -ErrorAction Continue
        exit $ExitUsage
    }

    $expected = $line.Substring(0, $split)
    $path = $line.Substring($split + 2)
    [void]$lockedPaths.Add($path)

    if (-not (Test-Path $path -PathType Leaf)) {
        Write-Error "contract-mirror: MISSING $path" -ErrorAction Continue
        $failures++
        continue
    }

    $actual = (Get-FileHash -Algorithm SHA256 -LiteralPath $path).Hash.ToLowerInvariant()
    if ($actual -ne $expected) {
        Write-Error "contract-mirror: MODIFIED $path" -ErrorAction Continue
        Write-Error "  expected $expected" -ErrorAction Continue
        Write-Error "  actual   $actual" -ErrorAction Continue
        $failures++
    }
}

# 2. no unlocked file may hide inside the mirror
$rootPrefix = (Resolve-Path $Root).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
foreach ($file in Get-ChildItem -Path $MirrorDir -Recurse -File) {
    $relative = $file.FullName.Substring($rootPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
    if (-not $lockedPaths.Contains($relative)) {
        Write-Error "contract-mirror: UNLOCKED $relative" -ErrorAction Continue
        $failures++
    }
}

if ($failures -ne 0) {
    Write-Error "contract-mirror: FAILED — $failures file(s) diverge from $Lock" -ErrorAction Continue
    Write-Error "contract-mirror: the mirror is read-only; restore it with eng/sync-contract-mirror.ps1" -ErrorAction Continue
    exit $ExitTampered
}

Write-Output "contract-mirror: OK — every mirrored file matches $Lock"
