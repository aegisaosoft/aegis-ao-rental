--
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
-- 
-- THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
-- Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
-- SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
-- WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
-- Alexander Orlov.
-- 
--  Author: Alexander Orlov
-- 
--

-- =====================================================
-- CREATE LOCATIONS TABLE
-- =====================================================

CREATE TABLE IF NOT EXISTS locations (
    location_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    location_name VARCHAR(255) NOT NULL,
    address TEXT,
    city VARCHAR(100),
    state VARCHAR(100),
    country VARCHAR(100) DEFAULT 'USA',
    postal_code VARCHAR(20),
    phone VARCHAR(50),
    email VARCHAR(255),
    latitude DECIMAL(10, 8),  -- For GPS coordinates
    longitude DECIMAL(11, 8), -- For GPS coordinates
    is_active BOOLEAN DEFAULT true,
    is_pickup_location BOOLEAN DEFAULT true,
    is_return_location BOOLEAN DEFAULT true,
    opening_hours TEXT, -- JSON or text format: "Mon-Fri: 9AM-6PM"
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- ADD LOCATION_ID TO VEHICLES TABLE
-- =====================================================

-- Add location_id column to vehicles table
ALTER TABLE vehicles ADD COLUMN IF NOT EXISTS location_id UUID REFERENCES locations(location_id) ON DELETE SET NULL;

-- =====================================================
-- MIGRATE EXISTING DATA
-- =====================================================

-- Create locations from existing vehicle locations
-- This will create unique locations per company
INSERT INTO locations (company_id, location_name, is_active, is_pickup_location, is_return_location)
SELECT DISTINCT 
    v.company_id,
    v.location,
    true,
    true,
    true
FROM vehicles v
WHERE v.location IS NOT NULL 
  AND v.location != ''
  AND NOT EXISTS (
      SELECT 1 FROM locations l 
      WHERE l.company_id = v.company_id 
      AND l.location_name = v.location
  );

-- Update vehicles to link to the new locations table
UPDATE vehicles v
SET location_id = (
    SELECT l.location_id 
    FROM locations l 
    WHERE l.company_id = v.company_id 
    AND l.location_name = v.location
    LIMIT 1
)
WHERE v.location IS NOT NULL 
  AND v.location != '';

-- =====================================================
-- CREATE INDEXES
-- =====================================================

CREATE INDEX IF NOT EXISTS idx_locations_company ON locations(company_id);
CREATE INDEX IF NOT EXISTS idx_locations_state ON locations(state);
CREATE INDEX IF NOT EXISTS idx_locations_is_active ON locations(is_active);
CREATE INDEX IF NOT EXISTS idx_locations_pickup ON locations(is_pickup_location);
CREATE INDEX IF NOT EXISTS idx_locations_return ON locations(is_return_location);
CREATE INDEX IF NOT EXISTS idx_vehicles_location ON vehicles(location_id);

-- =====================================================
-- CREATE TRIGGER FOR UPDATED_AT
-- =====================================================

DROP TRIGGER IF EXISTS update_locations_updated_at ON locations;
CREATE TRIGGER update_locations_updated_at BEFORE UPDATE ON locations
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- ADD COMMENTS
-- =====================================================

COMMENT ON TABLE locations IS 'Physical locations/branches for rental companies';
COMMENT ON COLUMN locations.latitude IS 'GPS latitude coordinate for mapping';
COMMENT ON COLUMN locations.longitude IS 'GPS longitude coordinate for mapping';
COMMENT ON COLUMN locations.is_pickup_location IS 'Whether customers can pick up vehicles from this location';
COMMENT ON COLUMN locations.is_return_location IS 'Whether customers can return vehicles to this location';
COMMENT ON COLUMN vehicles.location_id IS 'Reference to the locations table (replaces location VARCHAR)';

-- Note: The old location VARCHAR(255) column is kept for backward compatibility
-- You can drop it later after verifying the migration: ALTER TABLE vehicles DROP COLUMN location;

