-- Add price column to company_services table
-- This allows companies to set custom prices for services that differ from the base additional_service price

ALTER TABLE company_services
ADD COLUMN IF NOT EXISTS price numeric(10, 2) NULL;

-- Update existing records: if price is NULL, use the price from additional_services table
UPDATE company_services cs
SET price = (
    SELECT price 
    FROM additional_services as2 
    WHERE as2.id = cs.additional_service_id
)
WHERE cs.price IS NULL;

-- Add comment
COMMENT ON COLUMN company_services.price IS 'Custom price for this service at this company (if NULL, uses price from additional_services table)';

-- Optionally, you can make it NOT NULL with a default after the update:
-- ALTER TABLE company_services ALTER COLUMN price SET NOT NULL;
-- ALTER TABLE company_services ALTER COLUMN price SET DEFAULT 0.00;

