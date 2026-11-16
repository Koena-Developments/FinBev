$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\stop-dev.ps1" | Out-Null
& "$PSScriptRoot\init-dev-postgres.ps1" | Out-Null

Push-Location $repoRoot
try {
    dotnet build src/PettyCash.Api/PettyCash.Api.csproj
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    dotnet run --project src/PettyCash.Api --no-build -- --export-staff-passwords
    $out = (Resolve-Path (Join-Path $repoRoot "secrets\staff-temporary-passwords.xlsx")).Path
    Write-Host "Workbook written to: $out"
}
finally {
    Pop-Location
}
