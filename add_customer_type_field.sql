-- Add customer_type field to customers table
-- This script adds the customer_type column with enum values Individual and Corporate

-- Step 1: Add customer_type column with default value
ALTER TABLE customers 
ADD COLUMN IF NOT EXISTS customer_type VARCHAR(50) DEFAULT 'Individual';

-- Step 2: Update existing records to have Individual as default
UPDATE customers 
SET customer_type = 'Individual'
WHERE customer_type IS NULL;

-- Step 3: Set NOT NULL constraint after setting defaults
ALTER TABLE customers 
ALTER COLUMN customer_type SET NOT NULL;

-- Step 4: Add CHECK constraint for valid customer_type values
ALTER TABLE customers 
ADD CONSTRAINT chk_customer_type 
CHECK (customer_type IN ('Individual', 'Corporate'));

-- Step 5: Create index on customer_type for better query performance
CREATE INDEX IF NOT EXISTS idx_customers_customer_type ON customers(customer_type);

-- Step 6: Verify the changes
SELECT 
    customer_type,
    COUNT(*) as count
FROM customers 
GROUP BY customer_type
ORDER BY customer_type;

-- Show the constraint
SELECT 
    conname as constraint_name,
    pg_get_constraintdef(oid) as constraint_definition
FROM pg_constraint 
WHERE conrelid = 'customers'::regclass 
AND conname = 'chk_customer_type';

-- Verify column exists
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'customers' 
AND column_name = 'customer_type';
