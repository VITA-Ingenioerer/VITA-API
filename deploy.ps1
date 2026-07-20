[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('dev', 'prod')]
    [string]$Environment
)

$ErrorActionPreference = 'Stop'

$resourceGroup = 'rg-vita-planning'
$appName = "vita-planning-api-$Environment"
$publishDir = "./publish/$appName"
$zipPath = "./publish/$appName.zip"

# Deploys must come from a commit that's already on GitHub, so the repo is always
# the source of truth for what's actually running in each environment.
$dirty = git status --porcelain
if ($dirty) {
    Write-Error "Working tree has uncommitted changes. Commit and push before deploying:`n$dirty"
}

$branch = git rev-parse --abbrev-ref HEAD
$upstream = git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>$null
if (-not $upstream) {
    Write-Error "Branch '$branch' has no upstream. Push it to GitHub before deploying."
}

$local = git rev-parse HEAD
$remote = git rev-parse $upstream
if ($local -ne $remote) {
    Write-Error "Local HEAD ($($local.Substring(0,7))) differs from $upstream ($($remote.Substring(0,7))). Push before deploying."
}

$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Error "Not logged in to Azure CLI. Run 'az login' first."
}
Write-Host "Deploying commit $($local.Substring(0,7)) on '$branch' to $appName (subscription: $($account.name))" -ForegroundColor Cyan

if (Test-Path $publishDir) { Remove-Item -Recurse -Force $publishDir }
dotnet publish Vita.Planning.Api -c Release -o $publishDir

if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$publishDir/*" -DestinationPath $zipPath -Force

az webapp deploy `
    --resource-group $resourceGroup `
    --name $appName `
    --src-path $zipPath `
    --type zip

Write-Host "Deployed to https://$appName.azurewebsites.net" -ForegroundColor Green
