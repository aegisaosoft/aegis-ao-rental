-- Add role, is_active, last_login, and company_id fields to customers table
-- This migration adds role-based authentication to the customer table
-- Workers are associated with specific companies

-- Add new columns to customers table (PostgreSQL compatible)
DO $$ 
BEGIN
    -- Add role column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'customers' AND column_name = 'role') THEN
        ALTER TABLE customers ADD COLUMN role VARCHAR(50) DEFAULT 'customer';
    END IF;
    
    -- Add is_active column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'customers' AND column_name = 'is_active') THEN
        ALTER TABLE customers ADD COLUMN is_active BOOLEAN DEFAULT true;
    END IF;
    
    -- Add last_login column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'customers' AND column_name = 'last_login') THEN
        ALTER TABLE customers ADD COLUMN last_login TIMESTAMP;
    END IF;
    
    -- Add company_id column
    IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                   WHERE table_name = 'customers' AND column_name = 'company_id') THEN
        ALTER TABLE customers ADD COLUMN company_id UUID REFERENCES rental_companies(company_id) ON DELETE SET NULL;
    END IF;
END $$;

-- Add constraint for valid roles (if it doesn't exist)
DO $$ 
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.table_constraints 
                   WHERE table_name = 'customers' AND constraint_name = 'valid_customer_role') THEN
        ALTER TABLE customers ADD CONSTRAINT valid_customer_role 
        CHECK (role IN ('customer', 'worker', 'admin', 'mainadmin'));
    END IF;
END $$;

-- Create indexes for faster lookups (if they don't exist)
CREATE INDEX IF NOT EXISTS idx_customers_role ON customers(role);
CREATE INDEX IF NOT EXISTS idx_customers_is_active ON customers(is_active);
CREATE INDEX IF NOT EXISTS idx_customers_company_id ON customers(company_id);

-- Update existing customers to have default role
UPDATE customers SET role = 'customer' WHERE role IS NULL;
UPDATE customers SET is_active = true WHERE is_active IS NULL;
