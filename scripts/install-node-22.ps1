# Installs Node.js 22 LTS on Windows (no nvm required).
$ErrorActionPreference = "Stop"

Write-Host "Installing Node.js 22 LTS via winget..."
winget install OpenJS.NodeJS.22 --accept-package-agreements --accept-source-agreements

function Remove-OldNodeFromPath([string]$scope) {
    $name = if ($scope -eq 'User') { 'User' } else { 'Machine' }
    $current = [Environment]::GetEnvironmentVariable('Path', $scope)
    if ([string]::IsNullOrWhiteSpace($current)) { return }

    $parts = $current -split ';' | Where-Object {
        $_ -and ($_ -notmatch 'node-v25\.1\.0-win-x64')
    }
    $newPath = ($parts -join ';').Trim(';')
    if ($newPath -ne $current) {
        [Environment]::SetEnvironmentVariable('Path', $newPath, $scope)
        Write-Host "Removed Node v25 folder from $name PATH."
    }
}

Remove-OldNodeFromPath 'User'
try {
    Remove-OldNodeFromPath 'Machine'
} catch {
    Write-Host "Could not update Machine PATH (run as Administrator to remove old Node 25 from system Path)."
}

Write-Host ""
Write-Host "Close ALL terminals, open a NEW one, then run:"
Write-Host "  node -v    (should show v22.x)"
Write-Host ""
Write-Host "If you still see v25, open Environment Variables and delete this entry from Path:"
Write-Host "  C:\Program Files\node-v25.1.0-win-x64"
Write-Host ""
Write-Host "Then: cd frontend && npm run dev"
