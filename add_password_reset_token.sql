-- Migration: Add password reset token field to customers table
-- This script adds a token column to store password reset tokens

-- Add token column if it doesn't exist
ALTER TABLE customers ADD COLUMN IF NOT EXISTS token VARCHAR(255);
COMMENT ON COLUMN customers.token IS 'Password reset token - cleared after password reset';

-- Create index for faster token lookups
CREATE INDEX IF NOT EXISTS idx_customers_token ON customers(token) WHERE token IS NOT NULL;

-- Verify the changes
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'customers' 
AND column_name = 'token';

SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'customers'
AND indexname = 'idx_customers_token';

