$ErrorActionPreference = "SilentlyContinue"

Get-Process -Name "PettyCash.Api" | Stop-Process -Force

Get-NetTCPConnection -LocalPort 5280 -ErrorAction SilentlyContinue |
    ForEach-Object { Stop-Process -Id $_.OwningProcess -Force -ErrorAction SilentlyContinue }

Write-Host "Stopped PettyCash.Api (and anything bound to port 5280)."
