-- Migration: Add auto-publish settings to company_meta_credentials
-- Date: 2025-12-25
-- Description: Adds fields for automatic publishing of new vehicle models to social media

-- Add auto-publish columns to company_meta_credentials
ALTER TABLE company_meta_credentials 
ADD COLUMN IF NOT EXISTS auto_publish_facebook BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS auto_publish_instagram BOOLEAN DEFAULT FALSE,
ADD COLUMN IF NOT EXISTS auto_publish_include_price BOOLEAN DEFAULT TRUE,
ADD COLUMN IF NOT EXISTS auto_publish_hashtags TEXT DEFAULT NULL;

-- Add comments for documentation
COMMENT ON COLUMN company_meta_credentials.auto_publish_facebook IS 'Auto-publish new vehicle models to Facebook';
COMMENT ON COLUMN company_meta_credentials.auto_publish_instagram IS 'Auto-publish new vehicle models to Instagram';
COMMENT ON COLUMN company_meta_credentials.auto_publish_include_price IS 'Include daily rate in auto-published posts';
COMMENT ON COLUMN company_meta_credentials.auto_publish_hashtags IS 'JSON array of custom hashtags for auto-published posts';

-- Add vehicle_model_id to vehicle_social_posts for model-level posts
ALTER TABLE vehicle_social_posts
ALTER COLUMN vehicle_id DROP NOT NULL;

ALTER TABLE vehicle_social_posts
ADD COLUMN IF NOT EXISTS vehicle_model_id UUID REFERENCES vehicle_model(id) ON DELETE CASCADE;

-- Add index for vehicle_model_id
CREATE INDEX IF NOT EXISTS idx_vehicle_social_posts_vehicle_model_id 
ON vehicle_social_posts(vehicle_model_id) WHERE is_active = true;

CREATE INDEX IF NOT EXISTS idx_vehicle_social_posts_company_model 
ON vehicle_social_posts(company_id, vehicle_model_id);

COMMENT ON COLUMN vehicle_social_posts.vehicle_model_id IS 'Reference to vehicle model for model-level posts (Make/Model/Year)';

