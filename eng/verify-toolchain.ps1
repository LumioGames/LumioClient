$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root
dotnet --info
if ((Get-Content "./global.json" -Raw) -notmatch '"version":\s*"10.0.400"') { throw "SDK pin missing" }
dotnet restore --locked-mode
dotnet format --verify-no-changes --no-restore
dotnet build -c Release --no-restore
dotnet test -c Release --no-build --filter ToolchainPolicy
