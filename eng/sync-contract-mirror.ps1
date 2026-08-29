# Re-vendor the architecture source's published surface, or report drift against it.
#
# This needs a LumioGameEngineArchitecture checkout and therefore CANNOT be a CI
# gate — CI has no sibling repo. The gate is verify-contract-mirror.ps1, which is
# self-contained. Keep the two questions apart:
#   "were the vendored bytes hand-edited?"  -> verify-contract-mirror.ps1 (hard)
#   "has upstream moved past our pin?"      -> this script, -Check (report)
#
# Only committed objects are read (git archive <commit>); the source working
# tree is never consulted, so an unrelated dirty checkout cannot leak in.
[CmdletBinding()]
param(
    # Deliberately not read from the environment: a gate that silently passes
    # when the variable is unset is not a gate.
    [Parameter(Mandatory = $true)][string]$Source,
    [string]$Commit = "",
    [switch]$Check
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

$MirrorDir = "contract-mirror/upstream"
$Lock = "contract-mirror/contract-mirror.sha256"
$PinFile = "contract-mirror/MIRROR.md"

# The mirror scope is a rule, not a hand-kept file list: these upstream
# directories are vendored whole, with no exceptions. An exception list is a
# second thing to keep correct, and packages/index.json — the registry that
# records every artifact's compilerHash and baselineId — sits at the packages
# root, so any sub-selection would have had to carve around it.
$MirrorScope = @("schemas", "fixtures", "ids", "packages")

if (-not (Test-Path (Join-Path $Source ".git"))) {
    throw "contract-mirror: not a git checkout: $Source"
}

if ([string]::IsNullOrWhiteSpace($Commit) -and (Test-Path $PinFile -PathType Leaf)) {
    $match = [regex]::Match((Get-Content $PinFile -Raw), '(?m)^- upstream commit: `([0-9a-f]{40})`')
    if ($match.Success) { $Commit = $match.Groups[1].Value }
}

if ([string]::IsNullOrWhiteSpace($Commit)) {
    throw "contract-mirror: no -Commit given and no pin found in $PinFile"
}

git -C $Source cat-file -e "$Commit^{commit}" 2>$null
if ($LASTEXITCODE -ne 0) { throw "contract-mirror: commit not found in ${Source}: $Commit" }

$stage = Join-Path ([System.IO.Path]::GetTempPath()) ([System.IO.Path]::GetRandomFileName())
New-Item -ItemType Directory -Path $stage -Force | Out-Null
try {
    $tarball = Join-Path $stage "upstream.tar"
    git -C $Source archive --format=tar -o $tarball $Commit -- @MirrorScope
    if ($LASTEXITCODE -ne 0) { throw "contract-mirror: git archive failed" }

    $extract = Join-Path $stage "tree"
    New-Item -ItemType Directory -Path $extract -Force | Out-Null
    tar -x -f $tarball -C $extract
    if ($LASTEXITCODE -ne 0) { throw "contract-mirror: tar extract failed" }

    $extractPrefix = (Resolve-Path $extract).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if ($Check) {
        $drift = 0
        foreach ($file in Get-ChildItem -Path $extract -Recurse -File) {
            $rel = $file.FullName.Substring($extractPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
            $here = Join-Path $MirrorDir $rel
            if (-not (Test-Path $here -PathType Leaf)) {
                Write-Output "drift: ABSENT-HERE   $rel"
                $drift++
            }
            elseif ((Get-FileHash -Algorithm SHA256 -LiteralPath $here).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $file.FullName).Hash) {
                Write-Output "drift: BYTES-DIFFER  $rel"
                $drift++
            }
        }

        if (Test-Path $MirrorDir -PathType Container) {
            $mirrorPrefix = (Resolve-Path $MirrorDir).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
            foreach ($file in Get-ChildItem -Path $MirrorDir -Recurse -File) {
                $rel = $file.FullName.Substring($mirrorPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/')
                if (-not (Test-Path (Join-Path $extract $rel) -PathType Leaf)) {
                    Write-Output "drift: GONE-UPSTREAM $rel"
                    $drift++
                }
            }
        }

        if ($drift -eq 0) {
            Write-Output "contract-mirror: in sync with $Source at $Commit"
        }
        else {
            Write-Output "contract-mirror: $drift file(s) drift from $Source at $Commit — re-run without -Check to re-vendor"
        }
        # Report-only by contract: drift is news, not a build break.
        exit 0
    }

    if (Test-Path $MirrorDir) {
        Get-ChildItem -Path $MirrorDir -Recurse -File | ForEach-Object { $_.IsReadOnly = $false }
        Remove-Item -Recurse -Force $MirrorDir
    }
    New-Item -ItemType Directory -Path $MirrorDir -Force | Out-Null
    Copy-Item -Path (Join-Path $extract "*") -Destination $MirrorDir -Recurse -Force

    $rootPrefix = (Resolve-Path $Root).Path.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $lines = Get-ChildItem -Path $MirrorDir -Recurse -File |
        ForEach-Object { $_.FullName.Substring($rootPrefix.Length).Replace([System.IO.Path]::DirectorySeparatorChar, '/') } |
        Sort-Object -CaseSensitive |
        ForEach-Object { "$((Get-FileHash -Algorithm SHA256 -LiteralPath $_).Hash.ToLowerInvariant())  $_" }

    [System.IO.File]::WriteAllText((Join-Path $Root $Lock), ($lines -join "`n") + "`n")
    Get-ChildItem -Path $MirrorDir -Recurse -File | ForEach-Object { $_.IsReadOnly = $true }

    Write-Output "contract-mirror: vendored $($lines.Count) file(s) from $Source at $Commit"
    Write-Output "contract-mirror: remember to update the pin in $PinFile to $Commit"
}
finally {
    if (Test-Path $stage) { Remove-Item -Recurse -Force $stage }
}
