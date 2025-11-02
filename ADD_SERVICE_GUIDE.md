# How to Add Additional Services to Database

## Overview
The system already has full CRUD functionality for Additional Services. You can add services through the Admin Dashboard UI or directly via API.

## Method 1: Through Admin Dashboard (Recommended)

1. **Login as Admin**
   - Navigate to `/admin` page
   - Make sure you're logged in as an admin user

2. **Select Company**
   - Choose the company you want to add services for from the company selector

3. **Navigate to Booking Settings**
   - Click on "Booking Settings" in the left sidebar navigation

4. **Add New Service**
   - Click the "+ Add Service" button
   - Fill in the form:
     - **Name**: Service name (e.g., "Full Coverage Insurance")
     - **Description**: Optional description
     - **Price**: Price per unit/day (decimal)
     - **Service Type**: Select from dropdown:
       - Insurance
       - GPS
       - ChildSeat
       - AdditionalDriver
       - FuelPrepay
       - Cleaning
       - Delivery
       - Other
     - **Max Quantity**: Maximum quantity allowed (default: 1)
     - **Is Mandatory**: Check if this service is mandatory for all bookings
     - **Is Active**: Check if service is currently available

5. **Save**
   - Click "Save" button
   - The service will be created and added to the database

## Method 2: Direct API Call

### API Endpoint
```
POST /api/AdditionalServices
```

### Request Body
```json
{
  "companyId": "guid-here",
  "name": "Full Coverage Insurance",
  "description": "Comprehensive insurance coverage",
  "price": 25.99,
  "serviceType": "Insurance",
  "isMandatory": false,
  "maxQuantity": 1,
  "isActive": true
}
```

### Example using cURL
```bash
curl -X POST https://your-api-url/api/AdditionalServices \
  -H "Content-Type: application/json" \
  -d '{
    "companyId": "your-company-guid",
    "name": "GPS Navigation",
    "description": "GPS device rental",
    "price": 10.00,
    "serviceType": "GPS",
    "isMandatory": false,
    "maxQuantity": 1,
    "isActive": true
  }'
```

## Available Service Types
- `Insurance` - Insurance coverage
- `GPS` - GPS navigation device
- `ChildSeat` - Child seat rental
- `AdditionalDriver` - Additional driver option
- `FuelPrepay` - Fuel prepayment option
- `Cleaning` - Vehicle cleaning service
- `Delivery` - Vehicle delivery service
- `Other` - Other services

## Database Schema
The `additional_services` table has the following fields:
- `id` (UUID, Primary Key)
- `company_id` (UUID, Foreign Key to companies)
- `name` (varchar(255), Required)
- `description` (text, Optional)
- `price` (decimal(10,2), Required)
- `service_type` (varchar(50), Required)
- `is_mandatory` (bool, Default: false)
- `max_quantity` (int, Default: 1)
- `is_active` (bool, Default: true)
- `created_at` (timestamp)
- `updated_at` (timestamp)

## Validation Rules
- `name`: Required, max 255 characters
- `price`: Required, must be >= 0
- `serviceType`: Required, must be one of the valid types
- `maxQuantity`: Required, must be >= 1
- `companyId`: Required, company must exist in database

## Notes
- Each service is linked to a specific company
- Services can be marked as mandatory (required for all bookings)
- Services can be activated/deactivated without deletion
- Maximum quantity limits how many units of a service can be added per booking


