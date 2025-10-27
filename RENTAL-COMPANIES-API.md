# Rental Companies Management API

This document describes the comprehensive API for managing rental companies in the Car Rental system.

## Overview

The Rental Companies API provides full CRUD operations for managing rental companies, including:
- Company registration and management
- Stripe Connect integration for payment processing
- Revenue reporting and analytics
- Vehicle and reservation management
- Company statistics and performance metrics

## Base URL

```
/api/rentalcompanies
```

## Authentication

All endpoints require JWT authentication. Include the Bearer token in the Authorization header:

```
Authorization: Bearer <your-jwt-token>
```

## Endpoints

### 1. Get All Rental Companies

**GET** `/api/rentalcompanies`

Retrieve all rental companies with optional filtering and pagination.

#### Query Parameters

| Parameter | Type | Description | Default |
|-----------|------|-------------|---------|
| `search` | string | Search by company name, email, or city | - |
| `state` | string | Filter by state | - |
| `country` | string | Filter by country | - |
| `isActive` | boolean | Filter by active status | - |
| `page` | integer | Page number | 1 |
| `pageSize` | integer | Items per page | 20 |

#### Response

```json
[
  {
    "companyId": "uuid",
    "companyName": "Premium Rentals Inc",
    "email": "info@premiumrentals.com",
    "phone": "+1-555-0100",
    "address": "123 Main St",
    "city": "New York",
    "state": "NY",
    "country": "USA",
    "postalCode": "10001",
    "stripeAccountId": "acct_1234567890",
    "taxId": "12-3456789",
    "isActive": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T00:00:00Z"
  }
]
```

### 2. Get Rental Company by ID

**GET** `/api/rentalcompanies/{id}`

Retrieve a specific rental company by its ID.

#### Response

```json
{
  "companyId": "uuid",
  "companyName": "Premium Rentals Inc",
  "email": "info@premiumrentals.com",
  "phone": "+1-555-0100",
  "address": "123 Main St",
  "city": "New York",
  "state": "NY",
  "country": "USA",
  "postalCode": "10001",
  "stripeAccountId": "acct_1234567890",
  "taxId": "12-3456789",
  "isActive": true,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

### 3. Get Rental Company by Email

**GET** `/api/rentalcompanies/email/{email}`

Retrieve a rental company by its email address.

### 4. Create Rental Company

**POST** `/api/rentalcompanies`

Create a new rental company with automatic Stripe Connect account setup.

#### Request Body

```json
{
  "companyName": "Premium Rentals Inc",
  "email": "info@premiumrentals.com",
  "phone": "+1-555-0100",
  "address": "123 Main St",
  "city": "New York",
  "state": "NY",
  "country": "USA",
  "postalCode": "10001",
  "taxId": "12-3456789"
}
```

#### Response

```json
{
  "companyId": "uuid",
  "companyName": "Premium Rentals Inc",
  "email": "info@premiumrentals.com",
  "phone": "+1-555-0100",
  "address": "123 Main St",
  "city": "New York",
  "state": "NY",
  "country": "USA",
  "postalCode": "10001",
  "stripeAccountId": "acct_1234567890",
  "taxId": "12-3456789",
  "isActive": true,
  "createdAt": "2025-01-01T00:00:00Z",
  "updatedAt": "2025-01-01T00:00:00Z"
}
```

### 5. Update Rental Company

**PUT** `/api/rentalcompanies/{id}`

Update an existing rental company.

#### Request Body

```json
{
  "companyName": "Updated Company Name",
  "email": "newemail@company.com",
  "phone": "+1-555-0200",
  "address": "456 New St",
  "city": "Los Angeles",
  "state": "CA",
  "country": "USA",
  "postalCode": "90001",
  "taxId": "98-7654321",
  "isActive": true
}
```

### 6. Delete Rental Company

**DELETE** `/api/rentalcompanies/{id}`

Delete a rental company. Only allowed if the company has no active vehicles, reservations, or rentals.

### 7. Toggle Company Status

**POST** `/api/rentalcompanies/{id}/toggle-status`

Activate or deactivate a rental company.

#### Response

```json
{
  "isActive": true
}
```

### 8. Get Company Statistics

**GET** `/api/rentalcompanies/{id}/stats`

Get comprehensive statistics for a rental company.

#### Response

```json
{
  "totalVehicles": 50,
  "activeVehicles": 45,
  "totalReservations": 1200,
  "activeReservations": 25,
  "totalRentals": 1000,
  "activeRentals": 20,
  "totalRevenue": 150000.00,
  "averageRating": 4.5,
  "lastActivity": "2025-01-15T10:30:00Z"
}
```

### 9. Get Company Vehicles

**GET** `/api/rentalcompanies/{id}/vehicles`

Get all vehicles belonging to a rental company.

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `status` | string | Filter by vehicle status |
| `categoryId` | uuid | Filter by vehicle category |
| `page` | integer | Page number |
| `pageSize` | integer | Items per page |

#### Response

```json
[
  {
    "vehicleId": "uuid",
    "companyId": "uuid",
    "categoryId": "uuid",
    "make": "Toyota",
    "model": "Camry",
    "year": 2023,
    "color": "Silver",
    "licensePlate": "ABC123",
    "vin": "1HGBH41JXMN109186",
    "mileage": 15000,
    "fuelType": "Gasoline",
    "transmission": "Automatic",
    "seats": 5,
    "dailyRate": 75.00,
    "status": "available",
    "location": "Downtown Branch",
    "imageUrl": "https://example.com/camry.jpg",
    "features": ["GPS", "Bluetooth", "Backup Camera"],
    "isActive": true,
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T00:00:00Z",
    "categoryName": "Mid-Size"
  }
]
]
```

### 10. Get Company Reservations

**GET** `/api/rentalcompanies/{id}/reservations`

Get all reservations for a rental company.

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `status` | string | Filter by reservation status |
| `fromDate` | datetime | Filter from date |
| `toDate` | datetime | Filter to date |
| `page` | integer | Page number |
| `pageSize` | integer | Items per page |

#### Response

```json
[
  {
    "reservationId": "uuid",
    "customerId": "uuid",
    "vehicleId": "uuid",
    "companyId": "uuid",
    "reservationNumber": "RES-20250101-ABC12345",
    "pickupDate": "2025-01-15T10:00:00Z",
    "returnDate": "2025-01-17T10:00:00Z",
    "pickupLocation": "Downtown Branch",
    "returnLocation": "Downtown Branch",
    "dailyRate": 75.00,
    "totalDays": 2,
    "subtotal": 150.00,
    "taxAmount": 12.00,
    "insuranceAmount": 25.00,
    "additionalFees": 0.00,
    "totalAmount": 187.00,
    "status": "confirmed",
    "notes": "Customer requested GPS",
    "createdAt": "2025-01-01T00:00:00Z",
    "updatedAt": "2025-01-01T00:00:00Z",
    "customerName": "John Doe",
    "customerEmail": "john@example.com",
    "vehicleName": "Toyota Camry (2023)",
    "licensePlate": "ABC123",
    "companyName": "Premium Rentals Inc"
  }
]
```

### 11. Get Company Revenue Report

**GET** `/api/rentalcompanies/{id}/revenue`

Get detailed revenue report for a rental company.

#### Query Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `fromDate` | datetime | Start date for report |
| `toDate` | datetime | End date for report |

#### Response

```json
{
  "companyName": "Premium Rentals Inc",
  "period": {
    "from": "2025-01-01T00:00:00Z",
    "to": "2025-01-31T23:59:59Z"
  },
  "summary": {
    "totalRevenue": 50000.00,
    "totalTransactions": 250,
    "averageTransaction": 200.00
  },
  "dailyRevenue": [
    {
      "date": "2025-01-01T00:00:00Z",
      "totalAmount": 1500.00,
      "transactionCount": 8
    },
    {
      "date": "2025-01-02T00:00:00Z",
      "totalAmount": 2200.00,
      "transactionCount": 12
    }
  ]
}
```

### 12. Setup Stripe Connect Account

**POST** `/api/rentalcompanies/{id}/stripe/setup`

Setup Stripe Connect account for a rental company.

#### Response

```json
{
  "stripeAccountId": "acct_1234567890",
  "status": "pending",
  "requiresAction": true
}
```

### 13. Get Stripe Account Status

**GET** `/api/rentalcompanies/{id}/stripe/status`

Get the status of a rental company's Stripe Connect account.

#### Response

```json
{
  "stripeAccountId": "acct_1234567890",
  "status": "completed",
  "chargesEnabled": true,
  "payoutsEnabled": true,
  "requiresAction": false,
  "country": "US",
  "created": 1640995200
}
```

## Error Responses

### 400 Bad Request
```json
{
  "error": "Invalid request data",
  "message": "Company with this email already exists"
}
```

### 404 Not Found
```json
{
  "error": "Company not found",
  "message": "The specified rental company does not exist"
}
```

### 409 Conflict
```json
{
  "error": "Conflict",
  "message": "Cannot delete company with active vehicles, reservations, or rentals"
}
```

### 500 Internal Server Error
```json
{
  "error": "Internal server error",
  "message": "An unexpected error occurred"
}
```

## Business Rules

1. **Company Creation**: Automatically creates a Stripe Connect account
2. **Company Deletion**: Only allowed if no active vehicles, reservations, or rentals
3. **Email Uniqueness**: Company email addresses must be unique
4. **Stripe Integration**: All companies must have Stripe Connect accounts for payment processing
5. **Status Management**: Companies can be activated/deactivated without deletion

## Rate Limiting

- **Standard endpoints**: 100 requests per minute
- **Revenue reports**: 10 requests per minute
- **Stripe operations**: 5 requests per minute

## Security

- All endpoints require JWT authentication
- Company data is isolated by company ID
- Stripe operations are logged for audit purposes
- Sensitive data is encrypted in transit and at rest

## Examples

### Create a New Rental Company

```bash
curl -X POST "https://api.carrental.com/api/rentalcompanies" \
  -H "Authorization: Bearer <jwt-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "companyName": "City Car Rentals",
    "email": "info@citycarrentals.com",
    "phone": "+1-555-0300",
    "address": "789 Business Ave",
    "city": "Chicago",
    "state": "IL",
    "country": "USA",
    "postalCode": "60601",
    "taxId": "36-1234567"
  }'
```

### Get Company Revenue Report

```bash
curl -X GET "https://api.carrental.com/api/rentalcompanies/{company-id}/revenue?fromDate=2025-01-01&toDate=2025-01-31" \
  -H "Authorization: Bearer <jwt-token>"
```

### Setup Stripe Connect Account

```bash
curl -X POST "https://api.carrental.com/api/rentalcompanies/{company-id}/stripe/setup" \
  -H "Authorization: Bearer <jwt-token>"
```
