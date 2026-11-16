# Store Office 365 SMTP settings in user secrets (not committed).
# Example: .\scripts\configure-email-smtp.ps1 -SmtpPassword "your-app-password"
param(
    [string]$Project = "$PSScriptRoot\..\src\PettyCash.Api\PettyCash.Api.csproj",
    [string]$SmtpUser = "thabang@foodbev.co.za",
    [Parameter(Mandatory = $true)]
    [string]$SmtpPassword
)

dotnet user-secrets init --project $Project | Out-Null
dotnet user-secrets set "Email:Enabled" "true" --project $Project
dotnet user-secrets set "Email:SmtpHost" "smtp.office365.com" --project $Project
dotnet user-secrets set "Email:SmtpPort" "587" --project $Project
dotnet user-secrets set "Email:UseSsl" "true" --project $Project
dotnet user-secrets set "Email:Username" $SmtpUser --project $Project
dotnet user-secrets set "Email:Password" $SmtpPassword --project $Project
dotnet user-secrets set "Email:FromAddress" $SmtpUser --project $Project
dotnet user-secrets remove "Email:RedirectAllTo" --project $Project 2>$null

Write-Host "SMTP user secrets saved. Each person receives their own email. Restart the API, then submit a request to test."
