-- Migration: Add media links and promotional text fields to rental_companies table
-- Date: 2025-01-28
-- Description: Adds media link fields and promotional text fields for rental companies

-- Add video_link column to rental_companies table
ALTER TABLE rental_companies 
ADD COLUMN video_link VARCHAR(500);

-- Add banner_link column to rental_companies table
ALTER TABLE rental_companies 
ADD COLUMN banner_link VARCHAR(500);

-- Add logo_link column to rental_companies table
ALTER TABLE rental_companies 
ADD COLUMN logo_link VARCHAR(500);

-- Add motto column to rental_companies table with default value
ALTER TABLE rental_companies 
ADD COLUMN motto VARCHAR(255) DEFAULT 'Meet our newest fleet yet';

-- Add motto_description column to rental_companies table with default value
ALTER TABLE rental_companies 
ADD COLUMN motto_description VARCHAR(500) DEFAULT 'New rental cars. No lines. Let''s go!';

-- Add invitation column to rental_companies table with default value
ALTER TABLE rental_companies 
ADD COLUMN invitation TEXT DEFAULT 'Find & Book a Great Deal Today';

-- Add comments to the columns
COMMENT ON COLUMN rental_companies.video_link IS 'URL link to company promotional or informational video (YouTube, Vimeo, etc.)';
COMMENT ON COLUMN rental_companies.banner_link IS 'URL link to company banner image for homepage or promotional display';
COMMENT ON COLUMN rental_companies.logo_link IS 'URL link to company logo image';
COMMENT ON COLUMN rental_companies.motto IS 'Company motto or tagline (e.g., "Drive Your Dreams")';
COMMENT ON COLUMN rental_companies.motto_description IS 'Description or subtext for the motto';
COMMENT ON COLUMN rental_companies.invitation IS 'Company invitation or welcome message for customers';

-- Optional: Add check constraints to ensure valid URL formats (basic check)
-- Uncomment if you want to enforce URL format
-- ALTER TABLE rental_companies 
-- ADD CONSTRAINT chk_video_link_format 
-- CHECK (video_link IS NULL OR video_link ~* '^https?://');

-- ALTER TABLE rental_companies 
-- ADD CONSTRAINT chk_banner_link_format 
-- CHECK (banner_link IS NULL OR banner_link ~* '^https?://');

-- ALTER TABLE rental_companies 
-- ADD CONSTRAINT chk_logo_link_format 
-- CHECK (logo_link IS NULL OR logo_link ~* '^https?://');

-- Update existing companies to have NULL or default values
UPDATE rental_companies SET video_link = NULL WHERE video_link IS NULL;
UPDATE rental_companies SET banner_link = NULL WHERE banner_link IS NULL;
UPDATE rental_companies SET logo_link = NULL WHERE logo_link IS NULL;
-- Set default motto for existing companies that don't have one
UPDATE rental_companies SET motto = 'Meet our newest fleet yet' WHERE motto IS NULL;
-- Set default motto_description for existing companies that don't have one
UPDATE rental_companies SET motto_description = 'New rental cars. No lines. Let''s go!' WHERE motto_description IS NULL;
-- Set default invitation for existing companies that don't have one
UPDATE rental_companies SET invitation = 'Find & Book a Great Deal Today' WHERE invitation IS NULL;

-- Display updated schema
SELECT 
    column_name, 
    data_type, 
    character_maximum_length, 
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'rental_companies'
ORDER BY ordinal_position;

