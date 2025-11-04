# Multi-Tenancy Test Links

## API Base URLs
- **API (Direct)**: `https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net`
- **Frontend (if deployed)**: Check your frontend deployment URL

---

## 🔧 API Endpoints (Direct Access)

### 1. Company Configuration (Public - Domain-Based)
**Purpose**: Get company config based on domain/subdomain  
**Method**: GET  
**Auth**: None (public)

```
https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/config
```

**Expected Response**: 
- ✅ 200 OK with company config if accessed with `X-Company-Id` header or subdomain
- ⚠️ 400 Bad Request if no company ID found

---

### 2. Domain Mapping (Public - For Node.js Proxy)
**Purpose**: Get all active companies' domain mappings  
**Method**: GET  
**Auth**: None (public)

```
https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/domain-mapping
```

**Expected Response**: 
- ✅ 200 OK with dictionary: `{ "subdomain.aegis-rental.com": "company-guid", ... }`

---

### 3. Swagger UI
**Purpose**: Test API endpoints interactively  
**Method**: GET

```
https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/swagger
```

---

## 🌐 Subdomain-Based Testing

### Test Subdomain Access Patterns

Based on your database, test with actual company subdomains. Replace `{subdomain}` with actual values:

#### Example 1: Copacabana Rental
```
https://copacabanarental.aegis-rental.com/
https://copacabanarental.aegis-rental.com/api/companies/config
```

#### Example 2: Miami Life Cars
```
https://miamilifecars.aegis-rental.com/
https://miamilifecars.aegis-rental.com/api/companies/config
```

#### Example 3: Generic Company (if exists)
```
https://{subdomain}.aegis-rental.com/
https://{subdomain}.aegis-rental.com/api/companies/config
```

**Note**: These subdomains need to be configured in:
1. DNS (CNAME records pointing to your Azure App Service)
2. Azure App Service (custom domains)
3. Database (`companies` table with `subdomain` column)

---

## 🧪 Testing Scenarios

### Scenario 1: Direct API Access (No Subdomain)
**Test**: Access API directly without subdomain

```
GET https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/config
```

**Expected**: 
- ⚠️ 400 Bad Request (no company ID found)
- ✅ Should work if `X-Company-Id` header is provided

---

### Scenario 2: API with X-Company-Id Header
**Test**: Access API with company ID header (simulates Node.js proxy)

```bash
# Using curl
curl -H "X-Company-Id: {your-company-guid}" \
  https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/config
```

**Expected**: 
- ✅ 200 OK with company config
- ✅ Response includes company-specific branding

---

### Scenario 3: API with Query Parameter
**Test**: Access API with companyId query parameter

```
GET https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/config?companyId={your-company-guid}
```

**Expected**: 
- ✅ 200 OK with company config

---

### Scenario 4: Node.js Proxy → API
**Test**: Access through Node.js proxy with subdomain

```
GET https://{subdomain}.aegis-rental.com/api/companies/config
```

**Expected Flow**:
1. Node.js proxy detects subdomain
2. Proxy calls `/api/companies/domain-mapping` to get company ID
3. Proxy adds `X-Company-Id` header
4. Proxy forwards to .NET API
5. .NET API returns company config
6. ✅ 200 OK with company-specific data

---

## 📋 Frontend Testing Links

### If Frontend is Deployed:

#### Home Page (Different Subdomains)
```
https://copacabanarental.aegis-rental.com/
https://miamilifecars.aegis-rental.com/
https://{subdomain}.aegis-rental.com/
```

**Expected**:
- ✅ Shows company-specific branding (logo, colors, name)
- ✅ Navbar shows company name (not dropdown)
- ✅ Footer shows company name
- ✅ Custom CSS applied (if configured)

---

#### Other Pages (with Subdomain)
```
https://{subdomain}.aegis-rental.com/models
https://{subdomain}.aegis-rental.com/locations
https://{subdomain}.aegis-rental.com/bookings
```

**Expected**:
- ✅ All pages show company-specific branding
- ✅ Company context is maintained throughout navigation

---

## 🔍 Database Verification Queries

Before testing, verify your companies have subdomains configured:

```sql
-- Check all companies with subdomains
SELECT 
    id,
    company_name,
    subdomain,
    CASE 
        WHEN subdomain IS NOT NULL AND subdomain != '' 
        THEN subdomain || '.aegis-rental.com'
        ELSE NULL
    END as full_domain,
    is_active
FROM companies
WHERE is_active = true
ORDER BY company_name;
```

---

## 🧰 Testing Tools

### 1. Browser DevTools
- Open Network tab
- Check requests to `/api/companies/config`
- Verify `X-Company-Id` header is present
- Check response status and data

### 2. Postman/Insomnia
Create a collection with:
- **Request 1**: GET `/api/companies/config` (no headers) → Should fail
- **Request 2**: GET `/api/companies/config` (with `X-Company-Id` header) → Should succeed
- **Request 3**: GET `/api/companies/domain-mapping` → Should return mapping

### 3. curl Commands

```bash
# Test domain mapping
curl https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/domain-mapping

# Test company config (with header)
curl -H "X-Company-Id: YOUR-COMPANY-GUID" \
  https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/config

# Test company config (with query parameter)
curl "https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net/api/companies/config?companyId=YOUR-COMPANY-GUID"
```

---

## ✅ Success Criteria

### API Tests Pass When:
- ✅ `/api/companies/config` returns 200 with valid company data when accessed with subdomain/header
- ✅ `/api/companies/config` returns 400 when no company context is available
- ✅ `/api/companies/domain-mapping` returns all active companies' domains
- ✅ No EF Core "Token not valid" errors in logs
- ✅ Company middleware logs show successful company resolution

### Frontend Tests Pass When:
- ✅ Different subdomains show different company branding
- ✅ Navbar shows company name (not dropdown) when accessed via subdomain
- ✅ Company-specific colors, logos, and CSS are applied
- ✅ All API calls include correct company context

---

## 🐛 Troubleshooting

### If Company Config Returns 400:
1. Check Azure Log Stream for company resolution errors
2. Verify subdomain exists in database
3. Check `X-Company-Id` header is being sent
4. Verify domain mapping endpoint works

### If Subdomain Not Working:
1. Check DNS CNAME records point to Azure App Service
2. Verify custom domain is configured in Azure Portal
3. Check SSL certificate is valid
4. Verify subdomain is lowercase and matches database

### If EF Core Errors Persist:
1. Verify deployment includes the `UseModel(null)` fix
2. Check Azure Log Stream for detailed errors
3. Ensure clean rebuild and deployment

---

## 📝 Quick Reference

**API Base**: `https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net`  
**Domain Pattern**: `{subdomain}.aegis-rental.com`  
**Key Endpoints**:
- `/api/companies/config` - Get current company config
- `/api/companies/domain-mapping` - Get all domain mappings

**Test Subdomains** (verify in database first):
- `copacabanarental`
- `miamilifecars`
- (add more as needed)

