$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $repoRoot ".postgres\data"
$logFile = Join-Path $repoRoot ".postgres\server.log"
$pwFile = Join-Path $repoRoot ".postgres\.pwfile"
$bin = "C:\Program Files\PostgreSQL\18\bin"

if (-not (Test-Path "$bin\initdb.exe")) {
    throw "PostgreSQL 18 binaries not found at $bin. Install PostgreSQL or edit this script."
}

New-Item -ItemType Directory -Force -Path (Split-Path $dataDir) | Out-Null

if (-not (Test-Path "$dataDir\PG_VERSION")) {
    "pettycash_dev" | Set-Content -Path $pwFile -NoNewline -Encoding ascii
    & "$bin\initdb.exe" -D $dataDir -U pettycash -A scram-sha-256 --pwfile=$pwFile --encoding=UTF8 --locale=C
    Remove-Item $pwFile -Force -ErrorAction SilentlyContinue

    $conf = Join-Path $dataDir "postgresql.conf"
    (Get-Content $conf) `
        -replace "#port = 5432", "port = 5433" `
        -replace "#listen_addresses = 'localhost'", "listen_addresses = 'localhost'" |
        Set-Content $conf
}

$running = & "$bin\pg_ctl.exe" -D $dataDir status 2>&1
if ($LASTEXITCODE -ne 0) {
    & "$bin\pg_ctl.exe" -D $dataDir -l $logFile start -w
}

function Test-DevPostgresReady {
    $env:PGPASSWORD = "pettycash_dev"
    & "$bin\psql.exe" -U pettycash -h localhost -p 5433 -d postgres -v ON_ERROR_STOP=1 -c "SELECT 1;" 2>$null | Out-Null
    $ok = ($LASTEXITCODE -eq 0)
    Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    return $ok
}

$ready = $false
for ($i = 0; $i -lt 30; $i++) {
    if (Test-DevPostgresReady) {
        $ready = $true
        break
    }
    Start-Sleep -Seconds 1
}
if (-not $ready) {
    throw "Dev PostgreSQL did not become ready on localhost:5433. See $logFile"
}

$env:PGPASSWORD = "pettycash_dev"
$dbExists = & "$bin\psql.exe" -U pettycash -h localhost -p 5433 -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = 'pettycash';" 2>$null
if (-not ($dbExists -match '1')) {
    & "$bin\psql.exe" -U pettycash -h localhost -p 5433 -d postgres -v ON_ERROR_STOP=1 -c "CREATE DATABASE pettycash;" 2>$null | Out-Null
}
Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue

Write-Host "Dev PostgreSQL ready on localhost:5433 (user pettycash, database pettycash)."
