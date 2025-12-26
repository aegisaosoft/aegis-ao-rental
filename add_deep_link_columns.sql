-- ============================================================================
-- Add Deep Link columns to company_meta_credentials table
-- ============================================================================

-- Add deep link columns if they don't exist
ALTER TABLE company_meta_credentials 
ADD COLUMN IF NOT EXISTS deep_link_base_url VARCHAR(500),
ADD COLUMN IF NOT EXISTS deep_link_vehicle_pattern VARCHAR(500),
ADD COLUMN IF NOT EXISTS deep_link_booking_pattern VARCHAR(500);

-- Add comments
COMMENT ON COLUMN company_meta_credentials.deep_link_base_url IS 'Base URL for social media deep links (e.g., https://mycompany.aegis-rental.com)';
COMMENT ON COLUMN company_meta_credentials.deep_link_vehicle_pattern IS 'URL pattern for vehicle pages. Placeholders: {modelId}, {vehicleId}, {make}, {model}, {companyId}, {category}';
COMMENT ON COLUMN company_meta_credentials.deep_link_booking_pattern IS 'URL pattern for booking pages. Placeholders: {bookingId}, {companyId}';

-- ============================================================================
-- Example: Set deep link settings for a company
-- ============================================================================

-- UPDATE company_meta_credentials 
-- SET 
--     deep_link_base_url = 'https://mycompany.aegis-rental.com',
--     deep_link_vehicle_pattern = '/book?modelId={modelId}',
--     deep_link_booking_pattern = '/booking/{bookingId}'
-- WHERE company_id = 'YOUR_COMPANY_ID';

-- ============================================================================
-- Verify columns were added
-- ============================================================================

SELECT column_name, data_type, character_maximum_length
FROM information_schema.columns 
WHERE table_name = 'company_meta_credentials' 
AND column_name LIKE 'deep_link%';
