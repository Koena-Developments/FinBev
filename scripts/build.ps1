$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

& "$PSScriptRoot\stop-dev.ps1"

Push-Location $repoRoot
try {
    dotnet build @args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
