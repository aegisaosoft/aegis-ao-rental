-- Migration: Add auto-publish settings to company_meta_credentials
-- Date: 2025-12-25
-- Description: Adds fields for automatic publishing of new vehicles to social media

-- Add auto-publish columns
ALTER TABLE company_meta_credentials 
ADD COLUMN IF NOT EXISTS auto_publish_facebook BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS auto_publish_instagram BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS auto_publish_include_price BOOLEAN DEFAULT TRUE,
ADD COLUMN IF NOT EXISTS auto_publish_hashtags TEXT DEFAULT NULL;

-- Add comments for documentation
COMMENT ON COLUMN company_meta_credentials.auto_publish_facebook IS 'Auto-publish new vehicles to Facebook';
COMMENT ON COLUMN company_meta_credentials.auto_publish_instagram IS 'Auto-publish new vehicles to Instagram';
COMMENT ON COLUMN company_meta_credentials.auto_publish_include_price IS 'Include daily rate in auto-published posts';
COMMENT ON COLUMN company_meta_credentials.auto_publish_hashtags IS 'JSON array of custom hashtags for auto-published posts';
