# Complete Database Schema Update

## What Changed in `car_rental_schema.sql`:

### 1. Customers Table - Added Role Support (lines 63-80)
```sql
role VARCHAR(50) DEFAULT 'customer', -- customer, worker, admin, mainadmin
is_active BOOLEAN DEFAULT true,
last_login TIMESTAMP,
company_id UUID REFERENCES rental_companies(company_id) ON DELETE SET NULL,
CONSTRAINT valid_customer_role CHECK (role IN ('customer', 'worker', 'admin', 'mainadmin'))
```

### 2. Indexes for Customer Role Fields (lines 252-254)
```sql
CREATE INDEX IF NOT EXISTS idx_customers_role ON customers(role);
CREATE INDEX IF NOT EXISTS idx_customers_is_active ON customers(is_active);
CREATE INDEX IF NOT EXISTS idx_customers_company_id ON customers(company_id);
```

## Role Types:
- **customer**: Regular customer, cannot edit vehicles
- **worker**: Company employee, cannot edit vehicles  
- **admin**: Company administrator, can edit vehicles from their own company
- **mainadmin**: Super administrator, can edit vehicles from all companies

## Next Steps:

### Option 1: Fresh Database
Run the entire `car_rental_schema.sql` to create a new database with all role fields.

### Option 2: Existing Database  
Run the migration script `COMPLETE_MIGRATION.sql` to add role fields to existing database.

### Option 3: Update Existing User
After either option, update your user:
```sql
UPDATE customers 
SET role = 'mainadmin', 
    is_active = true 
WHERE email = 'orlovus@gmail.com';
```

## Authorization Flow:
1. User logs in → Backend generates JWT with role claims
2. User requests to edit vehicle → Backend checks role
3. **mainadmin**: ✅ Can edit ALL vehicles
4. **admin**: ✅ Can edit vehicles from their company only
5. **customer/worker**: ❌ Cannot edit vehicles

## Files Updated:
- ✅ `car_rental_schema.sql` - Complete schema with role support
- ✅ `COMPLETE_MIGRATION.sql` - Migration script for existing databases
- ✅ `VehiclesController.cs` - Role-based authorization logic
- ✅ `CustomerDto.cs` - Added Role, CompanyId, CompanyName fields
- ✅ `AuthController.cs` - Returns role in profile endpoint
- ✅ `Customer.cs` - Model has all role properties
- ✅ `AuthContext.js` - Frontend recognizes admin roles
- ✅ `VehicleDetail.js` - Shows edit button for admins

Everything is ready! Just run the migration and restart your backend.
