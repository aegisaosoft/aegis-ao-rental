-- Add terms_of_use column to companies table
-- This column stores formatted text content as JSONB

ALTER TABLE companies
ADD COLUMN IF NOT EXISTS terms_of_use JSONB;

-- Add comment to the column
COMMENT ON COLUMN companies.terms_of_use IS 'Terms of use content stored as JSONB (formatted HTML text)';

