# Contract mirror

A byte-exact, read-only copy of the published surface of `LumioGameEngineArchitecture`.

The architecture source publishes nothing to a NuGet feed — no `PackageId`, no
`nuget push`, no `GeneratePackageOnBuild` anywhere in that repo. Its CI step named
"Publish generated artifacts" runs `generate --out /tmp` and checks `outputHash`
stability; it uploads no package. The public consumption model is therefore a
vendored byte-level mirror plus a sha256 lock, and this directory is that mirror.

## Pin

- upstream repository: `LumioGameEngineArchitecture`
- upstream commit: `a206e2ca29be81a80f143d0251f4d525beadbf44`
- BaselineId: `LGE-V1.4-2026-08-27`
- pinned at: 2026-08-29T11:12:00Z
- mirror root: `contract-mirror/upstream/`
- lock file: `contract-mirror/contract-mirror.sha256`

The pin is a commit sha, never a branch name. `origin/main` moved five times while
this card was being measured (`e354611` → `11f6bfc` → `3287bba` → `a914244` →
`a206e2c`), so a branch-relative pin would not be reproducible.

Only the last of those moves is visible in the mirrored bytes: over
`3287bba..a206e2c` the scope below is byte-identical, because every commit in that
range touched `docs/` only. Chasing the branch head further would change the pin
string and nothing else.

## Scope is a rule, not a list

These upstream directories are vendored **whole, with no exceptions**:

- `schemas/`
- `fixtures/`
- `ids/`
- `packages/`

Nothing here records how many files that is. Upstream grows additively by design,
so a count would rot on exactly the changes the contract encourages — the same
reason the room's standing ruling replaced every count assertion with existence
plus identity.

There is deliberately no exclusion list. An earlier draft vendored only
`packages/csharp/` and `packages/canonical/` on the reasoning that a C# client
cannot reference a Rust crate — but that reasoning confuses *referencing* with
*mirroring*. Nothing here is referenced; it is contract truth read as bytes. The
sub-selection also carved around `packages/index.json`, the registry that records
every artifact's `compilerHash`, `outputHash` and `baselineId` and is the correct
place to read those values from. One rule with no exceptions is both smaller to
state and harder to get wrong.

## The mirror is read-only

Files under `upstream/` are copies of published bytes. Editing one is always a
defect, never a fix. If upstream is wrong, the fix belongs upstream — this repo
does not own public schemas, fixtures, ids or generated artifacts, and ADR-023 is
explicit that consumers must not vendor a rewritten copy.

`sync-contract-mirror.sh` sets the vendored files to mode `0444` locally. Git only
tracks the executable bit, so a fresh clone gets ordinary `0644` files; the mode is
a local guardrail, not the enforcement. Enforcement is the lock plus the two checks
below.

## Two checks, deliberately separate

They answer different questions and must not be collapsed into one command.

| Question | Command | Kind |
|---|---|---|
| Were the vendored bytes hand-edited? | `bash eng/verify-contract-mirror.sh` | **hard gate**, exit `33` on tamper |
| Has upstream moved past our pin? | `bash eng/sync-contract-mirror.sh --source <path> --check` | **report only**, always exit `0` |

The hard gate is self-contained: it reads only this repository, so it is a real
gate in CI where no sibling checkout exists. It runs in `repository-policy.yml`.

The drift report needs a `LumioGameEngineArchitecture` checkout and therefore
cannot be a CI gate. Its source path is a required argument and is deliberately
**not** read from the environment — a check that silently passes when a variable is
unset is not a check, which is the exact failure mode this card was written to
avoid.

PowerShell equivalents: `eng/verify-contract-mirror.ps1`, `eng/sync-contract-mirror.ps1`.

## Re-vendoring

```bash
bash eng/sync-contract-mirror.sh --source /path/to/LumioGameEngineArchitecture --commit <sha>
```

Then update the pin block above to the same sha and commit both together. The sync
script reads committed objects only (`git archive <commit>`); the source working
tree is never consulted, so an unrelated dirty checkout upstream cannot leak in.

## Line endings

`contract-mirror/.gitattributes` marks the whole mirror `-text`. The repository
root sets `* text=auto`, which would let git renormalize line endings on checkout
and silently break byte equality with upstream on a platform whose defaults differ.
No file in the pinned scope contains CRLF today, so the marker changes nothing now;
it exists so that a future upstream file carrying CRLF survives the round trip.

## A trap worth knowing about

The repository root `.gitignore` excludes `[Bb]in/` and `[Oo]bj/` **anywhere** in the
tree, and git does not descend into an excluded directory, so a `!` re-include on its
contents would not rescue it. No file in the current pinned scope sits under such a
path — `git check-ignore` over the whole mirror returns nothing — but if upstream ever
publishes one, this clone would drop it.

That failure is loud, not silent: the path stays in the lock, so
`verify-contract-mirror.sh` reports `MISSING` and exits `33` on the next fresh clone.
Fix it then by narrowing the root ignore patterns, not by editing the lock.

## Exit condition

This mirror is deleted when the architecture source publishes a consumable package
this repo can reference directly. Until then, `eng/upstream-api-map.md` records what
the published surface does and does not cover.

## Baseline observation

The mirrored artifact descriptors all carry `baselineId = LGE-V1.4-2026-08-27`,
while `eng/upstream-api-map.md` still declares `architectureBaseline =
LGE-V1.2-2026-08-27` and every module README is pinned to `LGE-V1.2-2026-08-27` by
`repository-policy.yml`. That gap predates this mirror and is not resolved here —
moving the repo's declared baseline is a cross-cutting decision, not a side effect
of vendoring.
