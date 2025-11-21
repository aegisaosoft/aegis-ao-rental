--
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
-- 
-- THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
-- Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
-- SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
-- WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
-- Alexander Orlov.
-- 
--  Author: Alexander Orlov
-- 
--
-- Migration: Add Google authentication fields to customers table
-- This script adds google_id, google_picture, and auth_provider columns

-- Step 1: Add google_id column to customers table
ALTER TABLE customers 
ADD COLUMN IF NOT EXISTS google_id VARCHAR(255);

-- Step 2: Add google_picture column to customers table
ALTER TABLE customers 
ADD COLUMN IF NOT EXISTS google_picture VARCHAR(500);

-- Step 3: Add auth_provider column to customers table
ALTER TABLE customers 
ADD COLUMN IF NOT EXISTS auth_provider VARCHAR(50);

-- Step 4: Add comments to document the columns
COMMENT ON COLUMN customers.google_id IS 'Google user ID from OAuth authentication';
COMMENT ON COLUMN customers.google_picture IS 'Google profile picture URL';
COMMENT ON COLUMN customers.auth_provider IS 'Authentication provider used (e.g., "google", "email")';

-- Step 5: Create index on google_id for faster lookups
CREATE INDEX IF NOT EXISTS idx_customers_google_id ON customers(google_id) WHERE google_id IS NOT NULL;

-- Step 6: Create index on auth_provider for filtering
CREATE INDEX IF NOT EXISTS idx_customers_auth_provider ON customers(auth_provider) WHERE auth_provider IS NOT NULL;

-- Step 7: Verify the changes
SELECT 
    column_name,
    data_type,
    character_maximum_length,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'customers' 
AND column_name IN ('google_id', 'google_picture', 'auth_provider')
ORDER BY column_name;

-- Step 8: Show index information
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'customers' 
AND indexname IN ('idx_customers_google_id', 'idx_customers_auth_provider');

