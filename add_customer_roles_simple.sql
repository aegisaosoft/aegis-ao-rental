-- Simple approach: Add role column step by step
-- Run these commands one by one

-- Step 1: Add role column
ALTER TABLE customers ADD COLUMN role VARCHAR(50) DEFAULT 'customer';

-- Step 2: Add is_active column  
ALTER TABLE customers ADD COLUMN is_active BOOLEAN DEFAULT true;

-- Step 3: Add last_login column
ALTER TABLE customers ADD COLUMN last_login TIMESTAMP;

-- Step 4: Add company_id column
ALTER TABLE customers ADD COLUMN company_id UUID REFERENCES rental_companies(company_id) ON DELETE SET NULL;

-- Step 5: Add constraint for valid roles
ALTER TABLE customers ADD CONSTRAINT valid_customer_role 
CHECK (role IN ('customer', 'worker', 'admin', 'mainadmin'));

-- Step 6: Create indexes
CREATE INDEX idx_customers_role ON customers(role);
CREATE INDEX idx_customers_is_active ON customers(is_active);
CREATE INDEX idx_customers_company_id ON customers(company_id);

-- Step 7: Update existing customers
UPDATE customers SET role = 'customer' WHERE role IS NULL;
UPDATE customers SET is_active = true WHERE is_active IS NULL;
