-- Rename tests field to texts in rental_companies table
-- Created: 2025-10-28

-- Rename column from tests to texts
ALTER TABLE rental_companies RENAME COLUMN tests TO texts;

-- Update column comment
COMMENT ON COLUMN rental_companies.texts IS 'JSONB field for flexible text content storage';

-- Verify the column was renamed
SELECT column_name, data_type, udt_name
FROM information_schema.columns
WHERE table_name = 'rental_companies'
AND column_name = 'texts';

-- Success message
DO $$
BEGIN
    RAISE NOTICE 'Successfully renamed tests column to texts in rental_companies table';
END $$;

