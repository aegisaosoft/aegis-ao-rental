# Deployment Instructions

## API Deployment (Azure App Service)

### Option 1: Using PowerShell Script (Recommended)

```powershell
cd aegis-ao-rental
.\deploy-api.ps1
```

### Option 2: Using Azure CLI Manually

1. **Login to Azure:**
   ```powershell
   az login
   ```

2. **Build and Publish:**
   ```powershell
   cd CarRental.Api
   dotnet publish --configuration Release --output ./publish
   ```

3. **Create ZIP package:**
   ```powershell
   Compress-Archive -Path .\publish\* -DestinationPath deploy-package.zip -Force
   ```

4. **Deploy to Azure:**
   ```powershell
   az webapp deployment source config-zip `
     --resource-group aegis_rentals `
     --name aegis-ao-rental `
     --src deploy-package.zip
   ```

### Option 3: Using Visual Studio

1. Right-click on `CarRental.Api` project
2. Select **Publish**
3. Choose the publish profile: **aegis-ao-rental - Zip Deploy**
4. Click **Publish**

## Frontend Deployment

The frontend uses GitHub Actions for automatic deployment. Push to the `main` branch:

```bash
cd aegis-ao-rental_web
git push origin main
```

The workflow will automatically:
- Build the React client
- Install dependencies
- Deploy to Azure Web App

## Verification

After deployment, verify the API is running:

1. **Check API Health:**
   ```
   https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/swagger
   ```

2. **Check API Status:**
   ```powershell
   az webapp show --name aegis-ao-rental --resource-group aegis_rentals --query state
   ```

## Troubleshooting

If deployment fails:

1. **Check Azure login:**
   ```powershell
   az account show
   ```

2. **Check Web App exists:**
   ```powershell
   az webapp list --resource-group aegis_rentals
   ```

3. **View deployment logs:**
   ```powershell
   az webapp log tail --name aegis-ao-rental --resource-group aegis_rentals
   ```

