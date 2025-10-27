-- Complete migration script for customer roles and company association
-- Run this entire script in your PostgreSQL client

-- Add all new columns to customers table
ALTER TABLE customers ADD COLUMN role VARCHAR(50) DEFAULT 'customer';
ALTER TABLE customers ADD COLUMN is_active BOOLEAN DEFAULT true;
ALTER TABLE customers ADD COLUMN last_login TIMESTAMP;
ALTER TABLE customers ADD COLUMN company_id UUID REFERENCES rental_companies(company_id) ON DELETE SET NULL;

-- Add constraint for valid roles
ALTER TABLE customers ADD CONSTRAINT valid_customer_role 
CHECK (role IN ('customer', 'worker', 'admin', 'mainadmin'));

-- Create indexes for better performance
CREATE INDEX idx_customers_role ON customers(role);
CREATE INDEX idx_customers_is_active ON customers(is_active);
CREATE INDEX idx_customers_company_id ON customers(company_id);

-- Update existing customers to have default values
UPDATE customers SET role = 'customer' WHERE role IS NULL;
UPDATE customers SET is_active = true WHERE is_active IS NULL;

-- Update your existing customer to be an admin
UPDATE customers 
SET role = 'admin', 
    is_active = true 
WHERE email = 'orlovus@gmail.com';

-- Verify the changes
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
