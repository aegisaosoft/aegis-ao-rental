-- Add background_link, about, and booking_integrated fields to rental_companies table
-- Created: 2025-10-28

-- Add background_link column (VARCHAR 255)
ALTER TABLE rental_companies
ADD COLUMN IF NOT EXISTS background_link VARCHAR(255);

-- Add about column (TEXT for long text)
ALTER TABLE rental_companies
ADD COLUMN IF NOT EXISTS about TEXT;

-- Add booking_integrated column (TEXT)
ALTER TABLE rental_companies
ADD COLUMN IF NOT EXISTS booking_integrated TEXT;

-- Add comments for documentation
COMMENT ON COLUMN rental_companies.background_link IS 'URL link to background image for the company';
COMMENT ON COLUMN rental_companies.about IS 'Long text description about the company';
COMMENT ON COLUMN rental_companies.booking_integrated IS 'Booking integration information or code';

-- Verify the columns were added
SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns
WHERE table_name = 'rental_companies'
AND column_name IN ('background_link', 'about', 'booking_integrated');

-- Success message
DO $$
BEGIN
    RAISE NOTICE 'Successfully added background_link, about, and booking_integrated fields to rental_companies table';
END $$;

