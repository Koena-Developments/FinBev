# Wire Microsoft sign-in after Entra app registration (SPA + PKCE).
# This cannot create the Entra app without an admin session in the FoodBev tenant.
param(
    [Parameter(Mandatory = $true)]
    [string]$TenantId,
    [Parameter(Mandatory = $true)]
    [string]$ClientId,
    [string]$PortalUrl = 'https://finbev.vercel.app',
    [switch]$LocalOnly
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$apiDir = Join-Path $repoRoot 'src\PettyCash.Api'
$frontendDir = Join-Path $repoRoot 'frontend'

Write-Host "Tenant: $TenantId"
Write-Host "Client: $ClientId"
Write-Host "Portal: $PortalUrl"

& dotnet user-secrets set "Auth:Microsoft:TenantId" $TenantId --project $apiDir | Out-Null
& dotnet user-secrets set "Auth:Microsoft:ClientId" $ClientId --project $apiDir | Out-Null
Write-Host "API user secrets updated."

$envLocal = @"
NUXT_MICROSOFT_TENANT_ID=$TenantId
NUXT_MICROSOFT_CLIENT_ID=$ClientId
"@
Set-Content -Path (Join-Path $frontendDir '.env.local') -Value $envLocal -Encoding utf8
Write-Host "Wrote frontend/.env.local (gitignored)."

if ($LocalOnly) {
    Write-Host "Local only. Restart Nuxt and run the API with user secrets to test Sign in with Microsoft."
    exit 0
}

function Invoke-VercelEnv {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
  try {
        & vercel @Arguments 2>&1 | ForEach-Object { if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { $_ } }
        if ($LASTEXITCODE -ne 0 -and $LASTEXITCODE -ne $null) {
            throw "vercel $($Arguments -join ' ') failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        $ErrorActionPreference = $previous
    }
}

Push-Location $frontendDir
foreach ($target in @('production', 'preview', 'development')) {
    Invoke-VercelEnv @('env', 'rm', 'NUXT_MICROSOFT_TENANT_ID', $target, '--yes') | Out-Null
    Invoke-VercelEnv @('env', 'rm', 'NUXT_MICROSOFT_CLIENT_ID', $target, '--yes') | Out-Null
    $TenantId | Invoke-VercelEnv @('env', 'add', 'NUXT_MICROSOFT_TENANT_ID', $target, '--force')
    $ClientId | Invoke-VercelEnv @('env', 'add', 'NUXT_MICROSOFT_CLIENT_ID', $target, '--force')
}
Pop-Location
Write-Host "Vercel (finbev) env updated for Production, Preview, and Development."

Write-Host ""
Write-Host "Render: set these on service pettycash-api (or your blueprint name), then redeploy:"
Write-Host "  Auth__Microsoft__TenantId = $TenantId"
Write-Host "  Auth__Microsoft__ClientId  = $ClientId"
Write-Host "Dashboard: https://dashboard.render.com"
Write-Host ""
Write-Host "Entra redirect URIs must include:"
Write-Host "  $PortalUrl/auth/microsoft/callback"
Write-Host "  http://localhost:3000/auth/microsoft/callback"