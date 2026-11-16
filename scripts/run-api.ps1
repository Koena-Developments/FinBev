$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

Push-Location $repoRoot
try {
    & (Join-Path $repoRoot "scripts\init-dev-postgres.ps1")
    dotnet run --project src/PettyCash.Api --launch-profile dev @args
}
finally {
    Pop-Location
}
