param(
    [Parameter(Mandatory = $true)]
    [string]$PostgresPassword
)

$ErrorActionPreference = "Stop"
$psql = "C:\Program Files\PostgreSQL\18\bin\psql.exe"
if (-not (Test-Path $psql)) {
    throw "psql not found at $psql. Adjust the path in this script for your PostgreSQL install."
}

$env:PGPASSWORD = $PostgresPassword

& $psql -U postgres -h localhost -p 5432 -d postgres -v ON_ERROR_STOP=0 -c "CREATE USER pettycash WITH PASSWORD 'pettycash_dev';"
& $psql -U postgres -h localhost -p 5432 -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE pettycash OWNER pettycash;"
& $psql -U postgres -h localhost -p 5432 -d postgres -v ON_ERROR_STOP=0 -c "GRANT ALL PRIVILEGES ON DATABASE pettycash TO pettycash;"

Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
Write-Host "Done. Use Port=5432 in appsettings.Development.json DefaultConnection."
