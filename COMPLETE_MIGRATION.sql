-- Complete Customer Migration Script
-- This script adds role support to the customers table
-- Run this in your PostgreSQL database

-- Step 1: Add new columns to customers table
ALTER TABLE customers ADD COLUMN IF NOT EXISTS role VARCHAR(50) DEFAULT 'customer';
ALTER TABLE customers ADD COLUMN IF NOT EXISTS is_active BOOLEAN DEFAULT true;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS last_login TIMESTAMP;
ALTER TABLE customers ADD COLUMN IF NOT EXISTS company_id UUID REFERENCES rental_companies(company_id) ON DELETE SET NULL;

-- Step 2: Add constraint for valid roles
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint WHERE conname = 'valid_customer_role'
    ) THEN
        ALTER TABLE customers ADD CONSTRAINT valid_customer_role 
        CHECK (role IN ('customer', 'worker', 'admin', 'mainadmin'));
    END IF;
END $$;

-- Step 3: Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_customers_role ON customers(role);
CREATE INDEX IF NOT EXISTS idx_customers_is_active ON customers(is_active);
CREATE INDEX IF NOT EXISTS idx_customers_company_id ON customers(company_id);

-- Step 4: Update existing customers to have default values
UPDATE customers SET role = 'customer' WHERE role IS NULL;
UPDATE customers SET is_active = true WHERE is_active IS NULL;

-- Step 5: Update your existing customer to be mainadmin
UPDATE customers 
SET role = 'mainadmin', 
    is_active = true 
WHERE email = 'orlovus@gmail.com';

-- Step 6: Verify the changes
SELECT 
    customer_id,
    email, 
    first_name,
    last_name,
    role, 
    is_active, 
    company_id,
    created_at
FROM customers 
WHERE email = 'orlovus@gmail.com';
