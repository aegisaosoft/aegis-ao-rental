# Visual Studio Deployment Guide - Fix EF Core Model Errors

## Problem
The error `Token 0x0600003f is not a valid Type token` indicates corrupted or mismatched Entity Framework assemblies in Azure. This requires a clean rebuild and deployment.

## Solution: Clean Build & Deploy from Visual Studio

### Step 1: Clean the Solution

1. In Visual Studio, go to **Build** → **Clean Solution**
   - This removes all build artifacts and intermediate files
   - Ensures no corrupted assemblies remain

2. Manually delete build folders (optional but recommended):
   - Close Visual Studio
   - Navigate to: `C:\aegis-ao\rental\aegis-ao-rental\CarRental.Api\`
   - Delete these folders if they exist:
     - `bin\`
     - `obj\`
   - Reopen Visual Studio

### Step 2: Rebuild the Solution

1. In Visual Studio, go to **Build** → **Rebuild Solution**
   - This performs a clean build from scratch
   - Ensures all dependencies are properly resolved

2. Verify the build succeeds:
   - Check the **Output** window for any errors
   - Ensure all projects compile successfully

### Step 3: Configure Publish Settings

1. Right-click on **CarRental.Api** project → **Publish**

2. Select your publish profile: **aegis-ao-rental - Zip Deploy**

3. Click **Edit** (pencil icon) to review settings

4. Ensure these settings:
   - **Configuration**: `Release`
   - **Target Framework**: `net9.0`
   - **Deployment Mode**: `Self-Contained` (recommended) OR `Framework-Dependent`
   - **Target Runtime**: `linux-x64` (as per your publish profile)

### Step 4: Publish Settings - Advanced

1. Click **Show all settings** or **Settings** tab

2. Under **File Publish Options**, ensure:
   - ✅ **Remove additional files at destination** - **IMPORTANT!**
     - This deletes old/corrupted files in Azure
   - ✅ **Precompile during publishing** - Recommended
   - ✅ **Exclude files from the App_Data folder** - Optional

3. Under **Database**:
   - Leave as-is (database migrations are handled separately)

### Step 5: Deploy

1. Click **Publish** button

2. Wait for deployment to complete:
   - Monitor the **Output** window
   - Check for any errors during deployment

3. After deployment completes:
   - Visual Studio will automatically open the site URL
   - Or manually navigate to: `https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net`

### Step 6: Verify Deployment

1. Check Azure Portal:
   - Go to your App Service: `aegis-ao-rental`
   - **Deployment Center** → Check deployment status
   - **Log stream** → Monitor for errors

2. Test the API:
   - Try accessing: `/api/companies/config`
   - Check if the EF Core errors are resolved

## Alternative: Quick Fix via Azure Portal

If the above doesn't work, try this:

1. **Stop the App Service**:
   - Azure Portal → Your App Service → **Stop**

2. **Delete old files** (via SSH or Kudu):
   - Azure Portal → **Development Tools** → **SSH**
   - Or use Kudu: `https://aegis-ao-rental-h4hda5gmengyhyc9.scm.canadacentral-01.azurewebsites.net`
   - Navigate to `site/wwwroot/bin/`
   - Delete all DLL files

3. **Redeploy from Visual Studio**:
   - Follow Steps 1-5 above

4. **Start the App Service**:
   - Azure Portal → Your App Service → **Start**

## Troubleshooting

### If errors persist:

1. **Check NuGet Package Versions**:
   - Ensure all EF Core packages are version `9.0.0`:
     - `Microsoft.EntityFrameworkCore`
     - `Microsoft.EntityFrameworkCore.Design`
     - `Microsoft.EntityFrameworkCore.Tools`
     - `Npgsql.EntityFrameworkCore.PostgreSQL`

2. **Clear NuGet Cache**:
   - Visual Studio → **Tools** → **NuGet Package Manager** → **Package Manager Console**
   - Run: `dotnet nuget locals all --clear`

3. **Restore Packages**:
   - Right-click Solution → **Restore NuGet Packages**

4. **Check .NET SDK Version**:
   - Ensure you have .NET 9.0 SDK installed
   - Visual Studio → **Help** → **About Microsoft Visual Studio**
   - Check installed .NET SDKs

## Prevention

To avoid this issue in the future:

1. **Always use "Remove additional files at destination"** when publishing
2. **Clean Solution** before rebuilding for production
3. **Use Release configuration** for all deployments
4. **Monitor deployment logs** after each publish

