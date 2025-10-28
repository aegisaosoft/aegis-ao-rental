-- Add new fields to rental_companies table
-- Created: 2025-10-28

-- Content and Integration fields
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS background_link VARCHAR(255);
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS about TEXT;
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS booking_integrated TEXT;
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS company_path TEXT;

-- Branding fields
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS subdomain VARCHAR(100);
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS primary_color VARCHAR(7);
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS secondary_color VARCHAR(7);
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS logo_url VARCHAR(500);
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS favicon_url VARCHAR(500);
ALTER TABLE rental_companies ADD COLUMN IF NOT EXISTS custom_css TEXT;

-- Add comments for documentation
COMMENT ON COLUMN rental_companies.background_link IS 'URL link to background image for the company';
COMMENT ON COLUMN rental_companies.about IS 'Long text description about the company';
COMMENT ON COLUMN rental_companies.booking_integrated IS 'Booking integration information or code';
COMMENT ON COLUMN rental_companies.company_path IS 'Company path or URL slug';
COMMENT ON COLUMN rental_companies.subdomain IS 'Unique subdomain for the company';
COMMENT ON COLUMN rental_companies.primary_color IS 'Primary brand color in hex format (e.g., #FF5733)';
COMMENT ON COLUMN rental_companies.secondary_color IS 'Secondary brand color in hex format (e.g., #33C1FF)';
COMMENT ON COLUMN rental_companies.logo_url IS 'URL to company logo image';
COMMENT ON COLUMN rental_companies.favicon_url IS 'URL to company favicon';
COMMENT ON COLUMN rental_companies.custom_css IS 'Custom CSS styles for the company';

-- Add index on subdomain for faster lookups
CREATE INDEX IF NOT EXISTS idx_rental_companies_subdomain ON rental_companies(subdomain);

-- Verify the columns were added
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns
WHERE table_name = 'rental_companies'
AND column_name IN ('background_link', 'about', 'booking_integrated', 'company_path', 
                     'subdomain', 'primary_color', 'secondary_color', 'logo_url', 'favicon_url', 'custom_css')
ORDER BY ordinal_position;

-- Success message
DO $$
BEGIN
    RAISE NOTICE 'Successfully added all new fields to rental_companies table';
    RAISE NOTICE 'Content fields: background_link, about, booking_integrated, company_path';
    RAISE NOTICE 'Branding fields: subdomain, primary_color, secondary_color, logo_url, favicon_url, custom_css';
END $$;

