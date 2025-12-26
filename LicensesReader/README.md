# Driver License Scanner - Complete System

## 📦 Package Overview

This is a complete driver license scanning system for rental platforms with:
- **React Native mobile app** with PDF417 barcode scanner
- **.NET API backend** for processing and storing license data
- **PostgreSQL database** optimized for existing customers table
- **Multi-tenant architecture** with row-level security

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────┐
│          MOBILE APP (React Native)              │
│  ┌──────────────────────────────────────┐      │
│  │  DriverLicenseScanner Component      │      │
│  │  - Camera + PDF417 Barcode Scanner   │      │
│  │  - AAMVA Standard Parser             │      │
│  └──────────────────────────────────────┘      │
└──────────────────┬──────────────────────────────┘
                   │ HTTPS
                   ▼
┌─────────────────────────────────────────────────┐
│          .NET API BACKEND                       │
│  ┌──────────────────────────────────────┐      │
│  │  LicenseController                    │      │
│  │  └─ POST /api/license/scan            │      │
│  │  └─ GET  /api/license/customer/{id}   │      │
│  └──────────────────────────────────────┘      │
│  ┌──────────────────────────────────────┐      │
│  │  LicenseService                       │      │
│  │  - Validation                         │      │
│  │  - Data processing                    │      │
│  │  - Database operations                │      │
│  └──────────────────────────────────────┘      │
└──────────────────┬──────────────────────────────┘
                   │ Npgsql
                   ▼
┌─────────────────────────────────────────────────┐
│          POSTGRESQL DATABASE                    │
│                                                  │
│  companies (existing)                           │
│      ↓                                           │
│  customers (existing)                           │
│      ↓                                           │
│  customer_licenses (new)                        │
│    - license_number                             │
│    - expiration_date                            │
│    - license_address (separate!)                │
│      ↓                                           │
│  license_scans (audit log)                      │
│                                                  │
└─────────────────────────────────────────────────┘
```

## 📂 Package Contents

### Backend (.NET)
```
backend/
├── LicenseService.cs        - Business logic
└── LicenseController.cs     - API endpoints
```

### Database (PostgreSQL)
```
database/
└── 001_license_scanning.sql - Complete migration
```

### Mobile (React Native)
```
mobile/
├── DriverLicenseScanner.tsx - Scanner component
├── App.tsx                  - Example integration
├── package.json             - Dependencies
├── android/                 - Android config
└── ios/                     - iOS config
```

### Documentation
```
docs/
├── README.md               - This file
├── SETUP.md                - Installation guide
├── API.md                  - API documentation
└── ADDRESS_GUIDE.md        - Address handling
```

## 🚀 Quick Start

### 1. Database Setup

```bash
psql -h your-db.postgres.database.azure.com \
  -U your-username \
  -d your-database \
  -f database/001_license_scanning.sql
```

### 2. Backend Setup

Add to your `.csproj`:
```xml
<PackageReference Include="Npgsql" Version="8.0.0" />
```

Add to `Program.cs`:
```csharp
builder.Services.AddScoped<ILicenseService, LicenseService>();
```

Copy files:
- `backend/LicenseService.cs` → `Services/`
- `backend/LicenseController.cs` → `Controllers/`

### 3. Mobile App Setup

```bash
cd mobile
npm install
cd ios && pod install && cd ..

# Run
npx react-native run-ios
# or
npx react-native run-android
```

## 🔑 Key Features

### Address Separation
**IMPORTANT:** License address is stored separately from customer address!

```
customers.address          → Current billing/mailing address
customer_licenses.license_address → Address on physical license (often outdated)
```

**Why?** People move but don't update their licenses. The license address is for verification only.

### Data Sync
When scanning a license, you can optionally sync customer data:

```csharp
POST /api/license/scan
{
  "syncCustomerData": true  // Only syncs name and DOB, NOT address
}
```

**What syncs:** `first_name`, `last_name`, `date_of_birth` (only if empty)  
**What DOESN'T sync:** `address`, `city`, `state`, `postal_code`

### Multi-Tenant Support
- Each company has isolated data (Row Level Security)
- Same license can exist in different companies
- License uniqueness enforced per company

### Validation
Automatic validation for:
- ✅ Age requirement (21+ years)
- ✅ License expiration
- ✅ Duplicate detection
- ✅ Required fields

## 📱 API Endpoints

### Scan License
```http
POST /api/license/scan
Content-Type: application/json

{
  "companyId": "guid",
  "customerId": "guid",
  "licenseData": {
    "firstName": "John",
    "lastName": "Doe",
    "licenseNumber": "D1234567",
    "state": "CA",
    "dateOfBirth": "01/15/1990",
    "expirationDate": "01/15/2026",
    ...
  },
  "syncCustomerData": true
}

Response:
{
  "success": true,
  "customerId": "guid",
  "licenseId": "guid",
  "age": 35,
  "licenseStatus": "valid",
  "customerDataUpdated": true,
  "fieldsUpdated": ["first_name", "last_name"]
}
```

### Get Customer License
```http
GET /api/license/customer/{customerId}

Response:
{
  "customer": {
    "customerId": "guid",
    "email": "john@email.com",
    "firstName": "John",
    "lastName": "Doe",
    "address": "456 Broadway, NY",  ← Current address
    "licenseNumber": "D1234567",
    "licenseState": "CA",
    "licenseAddress": "123 Main St, LA"  ← Old address on license
  },
  "hasLicense": true,
  "age": 35,
  "status": "valid"
}
```

### Get Expired Licenses
```http
GET /api/license/company/{companyId}/expired

Response:
{
  "count": 5,
  "expiredLicenses": [...]
}
```

## 🛡️ Security

### Row Level Security (RLS)
Database has RLS enabled to ensure companies only see their own data:

```sql
-- Set company context
SET app.current_company_id = 'your-company-guid';

-- All queries automatically filtered by company
```

### Authentication
Add authentication to your API:

```csharp
[Authorize]
[HttpPost("scan")]
public async Task<ActionResult> ScanLicense(...)
{
    var companyId = Guid.Parse(User.FindFirst("CompanyId")?.Value);
    // ...
}
```

## 📊 Database Views

Pre-built views for common queries:

```sql
-- Complete customer profiles with licenses
SELECT * FROM v_customers_complete;

-- Customers with expired licenses
SELECT * FROM v_expired_licenses WHERE company_id = 'xxx';

-- Customers without licenses
SELECT * FROM v_customers_without_license WHERE company_id = 'xxx';

-- Recent scans audit log
SELECT * FROM v_recent_scans WHERE company_id = 'xxx';
```

## 🔧 Configuration

### Connection String
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=your-db.postgres.database.azure.com;Database=your-db;Username=your-user;Password=your-pass;SSL Mode=Require"
  }
}
```

### Mobile App API URL
Update in `App.tsx`:
```typescript
const API_URL = 'https://your-api.azurewebsites.net/api/license';
```

## 🧪 Testing

### Test License Scan
```bash
curl -X POST https://your-api.azurewebsites.net/api/license/scan \
  -H "Content-Type: application/json" \
  -d '{
    "companyId": "your-company-guid",
    "customerId": "customer-guid",
    "licenseData": {
      "firstName": "John",
      "lastName": "Doe",
      "licenseNumber": "D1234567",
      "state": "CA",
      "dateOfBirth": "01/15/1990",
      "expirationDate": "01/15/2026"
    }
  }'
```

## 📖 Additional Documentation

- **SETUP.md** - Detailed installation instructions
- **API.md** - Complete API reference
- **ADDRESS_GUIDE.md** - Understanding address separation
- **MIGRATION_GUIDE.md** - Database migration help

## ⚠️ Common Issues

### "Index already exists" error
Run rollback script first:
```bash
psql -f database/rollback_license_scanning.sql
psql -f database/001_license_scanning.sql
```

### Camera permission denied
Check iOS Info.plist and Android Manifest have camera permissions.

### License not parsing
Ensure you're scanning the PDF417 barcode on the BACK of the license.

## 🆘 Support

For issues or questions:
1. Check the docs/ folder
2. Review API.md for endpoint details
3. Check ADDRESS_GUIDE.md for address handling

## 📝 License

Created for aegis-ao-rental and huur-rentals platforms.

---

**Version:** 1.0.0  
**Date:** October 29, 2025  
**Compatible with:** PostgreSQL 13+, .NET 8+, React Native 0.73+
