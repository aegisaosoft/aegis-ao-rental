-- Add is_mandatory column to company_services table
-- This allows companies to override the mandatory setting for services at the company level

ALTER TABLE company_services
ADD COLUMN IF NOT EXISTS is_mandatory bool NULL;

-- Update existing records: if is_mandatory is NULL, use the value from additional_services table
UPDATE company_services cs
SET is_mandatory = (
    SELECT is_mandatory 
    FROM additional_services as2 
    WHERE as2.id = cs.additional_service_id
)
WHERE cs.is_mandatory IS NULL;

-- Add comment
COMMENT ON COLUMN company_services.is_mandatory IS 'Whether this service is mandatory for this company (if NULL, uses is_mandatory from additional_services table)';

