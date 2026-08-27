# Dependency baseline

All direct packages must appear here with license and lock strategy. Versions live in `Directory.Packages.props`. Restore uses lock files (`RestorePackagesWithLockFile=true`).

| Package | Version | License | Lock strategy | AOT / isolation |
| --- | --- | --- | --- | --- |
| Microsoft.CodeAnalysis.BannedApiAnalyzers | 5.6.0 | MIT | Central Package Management + packages.lock.json | Analyzer only; not shipped |
| System.Threading.Channels | 10.0.0 | MIT | Central Package Management + packages.lock.json | Internal queues; type does not cross public ports |
| xunit.v3 | 3.2.2 | Apache-2.0 | Central Package Management + packages.lock.json | Test-only |
| xunit.runner.visualstudio | 3.1.5 | MIT | Central Package Management + packages.lock.json | Test-only |
| Microsoft.NET.Test.Sdk | 18.8.1 | MIT | Central Package Management + packages.lock.json | Test-only |
| TngTech.ArchUnitNET.xUnitV3 | 0.13.3 | Apache-2.0 | Central Package Management + packages.lock.json | ArchitectureTests only; design alias ArchUnitNET.xUnitV3 |
| FsCheck | 3.3.4 | BSD-3-Clause | Central Package Management + packages.lock.json | Property tests only; not FsCheck.Xunit (that package pulls xunit v2 and CS0433-collides with xunit.v3) |
