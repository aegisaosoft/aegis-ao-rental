# Deployment script for Car Rental API to Azure
# Prerequisites: Azure CLI must be installed and you must be logged in

param(
    [string]$ResourceGroup = "aegis_rentals",
    [string]$WebAppName = "aegis-ao-rental"
)

Write-Host "Starting deployment to Azure..." -ForegroundColor Green

# Check if Azure CLI is installed
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "Azure CLI is not installed. Please install it from: https://aka.ms/InstallAzureCLI" -ForegroundColor Red
    exit 1
}

# Check if logged in to Azure
$account = az account show 2>$null
if (-not $account) {
    Write-Host "Not logged in to Azure. Logging in..." -ForegroundColor Yellow
    az login
}

# Build the project
Write-Host "Building project in Release configuration..." -ForegroundColor Yellow
dotnet build --configuration Release

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Publish the project
Write-Host "Publishing project..." -ForegroundColor Yellow
dotnet publish --configuration Release --output ./publish

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# Create zip file for deployment
Write-Host "Creating deployment package..." -ForegroundColor Yellow
$zipPath = ".\deploy-package.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath
}
Compress-Archive -Path .\publish\* -DestinationPath $zipPath -Force

# Deploy to Azure
Write-Host "Deploying to Azure Web App: $WebAppName..." -ForegroundColor Yellow
az webapp deploy `
    --resource-group $ResourceGroup `
    --name $WebAppName `
    --src-path $zipPath `
    --type zip

if ($LASTEXITCODE -eq 0) {
    Write-Host "Deployment successful!" -ForegroundColor Green
    Write-Host "Application URL: https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net" -ForegroundColor Cyan
} else {
    Write-Host "Deployment failed!" -ForegroundColor Red
    exit 1
}

# Cleanup
Write-Host "Cleaning up..." -ForegroundColor Yellow
Remove-Item $zipPath -ErrorAction SilentlyContinue

Write-Host "Done!" -ForegroundColor Green

