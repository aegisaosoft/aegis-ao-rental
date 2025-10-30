# 🚀 Deployment Ready!

## ✅ Status
- **API Build:** ✅ Success (Release configuration)
- **Deployment Package:** ✅ Created (`deploy-package.zip` - 16.9 MB)
- **Next Step:** Deploy to Azure

## 📦 Deployment Package Location
```
aegis-ao-rental\CarRental.Api\deploy-package.zip
```

## 🔐 Step 1: Login to Azure

```powershell
az login
```

This will open your browser for authentication.

## 🚀 Step 2: Deploy API

### Option A: Use the PowerShell Script

```powershell
cd aegis-ao-rental
.\deploy-api.ps1
```

### Option B: Manual Deployment (Recommended - New Command)

```powershell
cd aegis-ao-rental\CarRental.Api

az webapp deploy `
  --resource-group aegis_rentals `
  --name aegis-ao-rental `
  --src-path .\deploy-package.zip `
  --type zip
```

### Option C: Visual Studio (If Azure SDK is installed)

1. Right-click `CarRental.Api` project
2. **Publish** → Select **aegis-ao-rental - Zip Deploy**
3. Click **Publish**

## ✅ Verify Deployment

After deployment completes:

1. **Check Swagger:** https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/swagger
2. **View Logs:** 
   ```powershell
   az webapp log tail --name aegis-ao-rental --resource-group aegis_rentals
   ```

## 🌐 Frontend Deployment

The frontend deploys automatically via GitHub Actions. Just push to main:

```powershell
cd ..\aegis-ao-rental_web
git add .
git commit -m "Deploy frontend updates"
git push origin main
```

## 📝 Important Notes

- Ensure database migrations are applied before/after deployment
- Check environment variables in Azure Portal if deployment fails
- The API URL is: `https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net`

