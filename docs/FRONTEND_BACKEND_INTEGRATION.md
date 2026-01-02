# Frontend-Backend API Integration

## Overview

This document describes the API contract between:
- **Frontend**: `aegis-ao-rental_web` (Node.js/Express + React)
- **Backend**: `aegis-ao-rental` (ASP.NET Core)

## Route Mapping

### Meta Integration Routes

| Frontend Route | Backend Route | Status |
|----------------|---------------|--------|
| `GET /api/companies/{companyId}/meta/status` | `GET /api/companies/{companyId}/meta/status` | ✅ NEW |
| `GET /api/companies/{companyId}/meta/pages` | `GET /api/companies/{companyId}/meta/pages` | ✅ NEW |
| `POST /api/companies/{companyId}/meta/disconnect` | `POST /api/companies/{companyId}/meta/disconnect` | ✅ NEW |
| `POST /api/companies/{companyId}/meta/select-page` | `POST /api/companies/{companyId}/meta/select-page` | ✅ NEW |
| `POST /api/companies/{companyId}/meta/refresh-instagram` | `POST /api/companies/{companyId}/meta/refresh-instagram` | ✅ NEW |
| `GET /api/meta/oauth/connect/{companyId}` | `GET /api/meta/oauth/connect/{companyId}` | ✅ EXISTS |
| `GET /api/meta/oauth/callback` | `GET /api/meta/oauth/callback` | ✅ EXISTS |

### Previously Mismatched Routes (Fixed)

Before this update, frontend was calling:
- `GET /api/companies/{companyId}/meta/status`

But backend only had:
- `GET /api/meta/oauth/status/{companyId}`

**Solution**: Added new `CompanyMetaController` with RESTful routes at `/api/companies/{companyId}/meta/*`

### Vehicles Routes

| Frontend Route | Backend Route | Status |
|----------------|---------------|--------|
| `GET /api/vehicles` | `GET /api/vehicles` | ✅ |
| `GET /api/vehicles/{id}` | `GET /api/vehicles/{id}` | ✅ |
| `PUT /api/vehicles/{id}` | `PUT /api/vehicles/{id}` | ✅ |

### Booking Routes

| Frontend Route | Backend Route | Status |
|----------------|---------------|--------|
| `GET /api/booking/bookings` | `GET /api/booking/bookings` | ✅ |
| `GET /api/booking/bookings/{id}` | `GET /api/booking/bookings/{id}` | ✅ |
| `GET /api/booking/companies/{companyId}/bookings` | `GET /api/booking/companies/{companyId}/bookings` | ✅ |
| `POST /api/booking/bookings` | `POST /api/booking/bookings` | ✅ |
| `PUT /api/booking/bookings/{id}` | `PUT /api/booking/bookings/{id}` | ✅ |
| `POST /api/booking/bookings/{id}/cancel` | `POST /api/booking/bookings/{id}/cancel` | ✅ |

### Auth Routes

| Frontend Route | Backend Route | Status |
|----------------|---------------|--------|
| `POST /api/auth/login` | `POST /api/auth/login` | ✅ |
| `POST /api/auth/register` | `POST /api/auth/register` | ✅ |
| `GET /api/auth/profile` | `GET /api/auth/profile` | ✅ |
| `PUT /api/auth/profile` | `PUT /api/auth/profile` | ✅ |
| `POST /api/auth/forgot-password` | `POST /api/auth/forgot-password` | ✅ |
| `POST /api/auth/reset-password` | `POST /api/auth/reset-password` | ✅ |

### Customers Routes

| Frontend Route | Backend Route | Status |
|----------------|---------------|--------|
| `GET /api/customers` | `GET /api/customers` | ✅ |
| `GET /api/customers/{id}` | `GET /api/customers/{id}` | ✅ |
| `POST /api/customers` | `POST /api/customers` | ✅ |
| `PUT /api/customers/{id}` | `PUT /api/customers/{id}` | ✅ |

### Companies Routes

| Frontend Route | Backend Route | Status |
|----------------|---------------|--------|
| `GET /api/RentalCompanies` | `GET /api/RentalCompanies` | ✅ |
| `GET /api/RentalCompanies/{id}` | `GET /api/RentalCompanies/{id}` | ✅ |

## New Files Added

### Backend (aegis-ao-rental)

1. **`CarRental.Api/Controllers/CompanyMetaController.cs`**
   - New RESTful controller for Meta integration
   - Routes: `/api/companies/{companyId}/meta/*`
   - Endpoints:
     - `GET /status` - Get Meta connection status
     - `GET /pages` - Get available Facebook pages
     - `POST /disconnect` - Disconnect from Meta
     - `POST /select-page` - Select a Facebook page
     - `POST /refresh-instagram` - Refresh Instagram connection
     - `GET /auto-publish` - Get auto-publish settings
     - `POST /auto-publish` - Update auto-publish settings
     - `GET /deep-links` - Get deep link settings
     - `POST /deep-links` - Update deep link settings

2. **`CarRental.Tests/Integration/FrontendBackendIntegrationTests.cs`**
   - Integration tests verifying API contract
   - Tests Meta integration endpoints
   - Tests data structure matches frontend expectations

## Testing

Run integration tests:
```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

Run Meta-specific tests:
```bash
dotnet test --filter "FullyQualifiedName~Meta"
```

## Response Format

### Meta Status Response

Frontend expects:
```json
{
  "isConnected": true,
  "status": "Active",
  "pageId": "123456789",
  "pageName": "My Business Page",
  "instagramAccountId": "987654321",
  "instagramUsername": "mybusiness",
  "catalogId": null,
  "pixelId": null,
  "tokenExpiresAt": "2025-03-01T00:00:00Z",
  "tokenStatus": "valid",
  "lastTokenRefresh": "2025-01-01T00:00:00Z"
}
```

### Meta Pages Response

Frontend expects array of pages:
```json
[
  {
    "id": "123456789",
    "name": "My Business Page",
    "accessToken": "...",
    "instagramBusinessAccountId": "987654321",
    "instagramUsername": "mybusiness"
  }
]
```

## Environment Configuration

### Frontend (aegis-ao-rental_web)

Set `API_BASE_URL` in Azure App Service Configuration:
```
API_BASE_URL=https://aegis-ao-rental-h4hda5gmengyhyc9.canadacentral-01.azurewebsites.net
```

### Backend (aegis-ao-rental)

No changes needed - endpoints are available at the existing base URL.
