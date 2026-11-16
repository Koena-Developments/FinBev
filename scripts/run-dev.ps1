$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\init-dev-postgres.ps1"

Push-Location $repoRoot
try {
    dotnet ef database update --project src/PettyCash.Infrastructure --startup-project src/PettyCash.Api
    Write-Host "Starting API at http://localhost:5280 ..."
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$repoRoot'; dotnet run --project src/PettyCash.Api"
    Write-Host "Starting frontend at http://localhost:3000 (Node 22/24 LTS required; see frontend/.nvmrc) ..."
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$repoRoot\frontend'; `$env:Path = 'C:\Program Files\nodejs;' + `$env:Path; `$env:NUXT_PUBLIC_API_BASE='http://localhost:5280'; npm run dev"
}
finally {
    Pop-Location
}
