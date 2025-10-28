-- Add tests JSONB field to rental_companies table
-- This script adds a flexible JSONB column for storing test data or configuration

-- Add tests column to rental_companies table
ALTER TABLE rental_companies 
ADD COLUMN tests JSONB;

-- Add comment to the column
COMMENT ON COLUMN rental_companies.tests IS 'Flexible JSONB field for storing test data or configuration';

-- Create index on tests column for better query performance
CREATE INDEX idx_rental_companies_tests ON rental_companies USING gin (tests);

-- Display updated schema
SELECT 
    column_name, 
    data_type, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'rental_companies'
    AND column_name = 'tests';

-- Example usage:
-- UPDATE rental_companies 
-- SET tests = '{"test1": "value1", "test2": {"nested": "value2"}}'::jsonb 
-- WHERE company_id = 'some-uuid';

-- Query examples:
-- Get specific key from tests:
-- SELECT tests->>'test1' as test1_value FROM rental_companies;

-- Query where tests contains specific key:
-- SELECT * FROM rental_companies WHERE tests ? 'test1';

-- Query where tests contains specific value:
-- SELECT * FROM rental_companies WHERE tests @> '{"test1": "value1"}'::jsonb;

