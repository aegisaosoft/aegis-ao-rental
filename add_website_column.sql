/*
 * Migration: Add website column to rental_companies table
 * Date: 2025-01-28
 * Description: Adds website field to store company website URLs
 */

-- Add website column to rental_companies table
ALTER TABLE rental_companies 
ADD COLUMN IF NOT EXISTS website VARCHAR(255);

-- Add comment for documentation
COMMENT ON COLUMN rental_companies.website IS 'Company website URL';

-- Verify the column was added
SELECT column_name, data_type, character_maximum_length, is_nullable
FROM information_schema.columns
WHERE table_name = 'rental_companies' AND column_name = 'website';

