--
-- MIGRATION SCRIPT: Restructure vehicle_model as catalog template
-- This script:
-- 1. Deletes all records from vehicles and vehicle_model
-- 2. Removes vehicle_id from vehicle_model table
-- 3. Adds vehicle_model_id to vehicles table
-- 4. Restores vehicles with links to vehicle_model catalog
--
-- This is a DESTRUCTIVE migration - make sure backup exists!
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- ============================================
-- STEP 1: Verify backup exists
-- ============================================
SELECT 
    'Checking vehicles_backup exists' AS status,
    COUNT(*) AS backup_count
FROM vehicles_backup;

-- ============================================
-- STEP 2: Drop dependent views
-- ============================================
DROP VIEW IF EXISTS reservation_details CASCADE;
DROP VIEW IF EXISTS company_reservations CASCADE;

-- ============================================
-- STEP 3: Delete all records from junction tables first
-- ============================================
DELETE FROM vehicle_model;

-- Verify deletion
SELECT 
    'vehicle_model after deletion' AS status,
    COUNT(*) AS record_count
FROM vehicle_model;

-- ============================================
-- STEP 4: Delete all vehicles
-- ============================================
DELETE FROM vehicles;

-- Verify deletion
SELECT 
    'vehicles after deletion' AS status,
    COUNT(*) AS vehicle_count
FROM vehicles;

-- ============================================
-- STEP 5: Remove vehicle_id column from vehicle_model
-- ============================================
ALTER TABLE vehicle_model DROP COLUMN IF EXISTS vehicle_id CASCADE;

-- Add new primary key on model_id (unique per model)
ALTER TABLE vehicle_model 
DROP CONSTRAINT IF EXISTS pk_vehicle_model;

ALTER TABLE vehicle_model
ADD CONSTRAINT pk_vehicle_model PRIMARY KEY (model_id);

-- Verify structure
SELECT 
    'vehicle_model columns after restructuring' AS status,
    column_name, data_type
FROM information_schema.columns
WHERE table_name = 'vehicle_model'
ORDER BY ordinal_position;

-- ============================================
-- STEP 6: Add vehicle_model_id to vehicles
-- ============================================
ALTER TABLE vehicles 
ADD COLUMN IF NOT EXISTS vehicle_model_id UUID;

-- Add foreign key
ALTER TABLE vehicles
DROP CONSTRAINT IF EXISTS fk_vehicles_vehicle_model_id;

ALTER TABLE vehicles
ADD CONSTRAINT fk_vehicles_vehicle_model_id 
FOREIGN KEY (vehicle_model_id) REFERENCES vehicle_model(model_id) ON DELETE SET NULL;

-- Create index
CREATE INDEX IF NOT EXISTS idx_vehicles_vehicle_model_id ON vehicles(vehicle_model_id);

-- Verify structure
SELECT 
    'vehicles columns after adding vehicle_model_id' AS status,
    column_name, data_type
FROM information_schema.columns
WHERE table_name = 'vehicles' AND column_name = 'vehicle_model_id';

-- ============================================
-- STEP 7: Populate vehicle_model catalog from backup data
-- ============================================
-- Create unique vehicle_model entries (one per unique make/model/year/daily_rate)
INSERT INTO vehicle_model (model_id, daily_rate, created_at)
SELECT DISTINCT
    m.id AS model_id,
    AVG(b.daily_rate) AS daily_rate,  -- Average rate for this model
    MIN(b.created_at) AS created_at
FROM vehicles_backup b
INNER JOIN models m 
    ON UPPER(TRIM(b.make)) = UPPER(TRIM(m.make))
    AND UPPER(TRIM(b.model)) = UPPER(TRIM(m.model))
    AND b.year = m.year
GROUP BY m.id
ON CONFLICT (model_id) DO NOTHING;

-- Verify vehicle_model populated
SELECT 
    'vehicle_model catalog records created' AS status,
    COUNT(*) AS record_count
FROM vehicle_model;

-- ============================================
-- STEP 8: Restore vehicles and link to vehicle_model catalog
-- ============================================
INSERT INTO vehicles (
    id,
    company_id,
    color,
    license_plate,
    vin,
    mileage,
    transmission,
    seats,
    status,
    state,
    location,
    location_id,
    current_location_id,
    image_url,
    features,
    vehicle_model_id,
    created_at,
    updated_at
)
SELECT 
    b.id,
    b.company_id,
    b.color,
    b.license_plate,
    b.vin,
    b.mileage,
    b.transmission,
    b.seats,
    b.status,
    b.state,
    b.location,
    b.location_id,
    b.current_location_id,
    b.image_url,
    b.features,
    vm.model_id AS vehicle_model_id,  -- Link to catalog
    b.created_at,
    b.updated_at
FROM vehicles_backup b
INNER JOIN models m 
    ON UPPER(TRIM(b.make)) = UPPER(TRIM(m.make))
    AND UPPER(TRIM(b.model)) = UPPER(TRIM(m.model))
    AND b.year = m.year
INNER JOIN vehicle_model vm ON m.id = vm.model_id;

-- Verify vehicles restored
SELECT 
    'Vehicles restored with vehicle_model links' AS status,
    COUNT(*) AS vehicle_count
FROM vehicles;

-- ============================================
-- STEP 9: Verification queries
-- ============================================
-- Check for orphaned vehicles (no vehicle_model link)
SELECT 
    'Vehicles without vehicle_model link' AS status,
    COUNT(*) AS orphaned_count
FROM vehicles
WHERE vehicle_model_id IS NULL;

-- Show sample of relationships
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    vm.model_id,
    m.make || ' ' || m.model || ' ' || m.year::text AS model_name,
    vm.daily_rate,
    COUNT(*) OVER (PARTITION BY vm.model_id) AS vehicles_sharing_this_model
FROM vehicles v
INNER JOIN vehicle_model vm ON v.vehicle_model_id = vm.model_id
INNER JOIN models m ON vm.model_id = m.id
LIMIT 10;

-- Check how many vehicles share each model
SELECT 
    m.make,
    m.model,
    m.year,
    COUNT(v.id) AS vehicle_count
FROM models m
INNER JOIN vehicle_model vm ON m.id = vm.model_id
LEFT JOIN vehicles v ON v.vehicle_model_id = vm.model_id
GROUP BY m.make, m.model, m.year
ORDER BY vehicle_count DESC
LIMIT 20;

-- Final summary
SELECT 
    'Final state - vehicles count' AS check_type,
    COUNT(*) AS count
FROM vehicles
UNION ALL
SELECT 
    'Final state - vehicle_model catalog count' AS check_type,
    COUNT(*) AS count
FROM vehicle_model
UNION ALL
SELECT 
    'Final state - models count' AS check_type,
    COUNT(*) AS count
FROM models
UNION ALL
SELECT 
    'Final state - vehicles without catalog' AS check_type,
    COUNT(*) AS count
FROM vehicles
WHERE vehicle_model_id IS NULL;

-- Show summary
SELECT 
    'MIGRATION COMPLETE' AS status,
    'Check results above for any issues' AS next_steps;

