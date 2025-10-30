# Quick Deployment Guide

## ✅ API Build Complete

The API has been built successfully in Release configuration. Ready to deploy!

## 🚀 Deploy API to Azure

### Method 1: PowerShell Script (Easiest)

```powershell
cd aegis-ao-rental
.\deploy-api.ps1
```

### Method 2: Azure CLI

If you have Azure CLI installed and are logged in:

```powershell
# From CarRental.Api directory
cd aegis-ao-rental\CarRental.Api

# Create ZIP package
Compress-Archive -Path .\publish\* -DestinationPath deploy-package.zip -Force

# Deploy to Azure
az webapp deployment source config-zip `
  --resource-group aegis_rentals `
  --name aegis-ao-rental `
  --src deploy-package.zip
```

### Method 3: Visual Studio

1. Right-click `CarRental.Api` project
2. Select **Publish**
3. Choose **aegis-ao-rental - Zip Deploy**
4. Click **Publish**

## 🌐 Frontend Deployment

The frontend will deploy automatically via GitHub Actions when you push to main:

```bash
cd aegis-ao-rental_web
git add .
git commit -m "Update frontend"
git push origin main
```

## 📋 Pre-Deployment Checklist

- [x] API code compiles (Release build successful)
- [ ] Database migrations applied (run SQL scripts if needed)
- [ ] Environment variables configured in Azure
- [ ] Frontend API URL points to correct backend

## 🔍 Verify Deployment

After deployment:

1. **API Swagger:** https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/swagger
2. **Health Check:** https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/health
3. **View Logs:** `az webapp log tail --name aegis-ao-rental --resource-group aegis_rentals`

