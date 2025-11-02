--
-- MIGRATION SCRIPT: Restructure vehicle-model architecture
-- This script:
-- 1. Creates vehicles_backup table
-- 2. Deletes all records from vehicles and vehicle_model
-- 3. Removes make/model/year columns from vehicles
-- 4. Restores vehicles and vehicle_model from backup
--
-- IMPORTANT: This is a DESTRUCTIVE migration!
-- Make sure to backup your database before running!
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- ============================================
-- STEP 1: Create backup table with current data
-- ============================================
CREATE TABLE IF NOT EXISTS vehicles_backup AS SELECT * FROM vehicles;

-- Verify backup was created
SELECT 
    'Backup created' AS status,
    COUNT(*) AS vehicle_count
FROM vehicles_backup;

-- ============================================
-- STEP 2: Delete all records from junction tables first
-- ============================================
-- Delete from vehicle_model first (to avoid FK constraint issues)
DELETE FROM vehicle_model;

-- Verify deletion
SELECT 
    'vehicle_model after deletion' AS status,
    COUNT(*) AS record_count
FROM vehicle_model;

-- ============================================
-- STEP 3: Delete all vehicles
-- ============================================
DELETE FROM vehicles;

-- Verify deletion
SELECT 
    'vehicles after deletion' AS status,
    COUNT(*) AS vehicle_count
FROM vehicles;

-- ============================================
-- STEP 4: Drop dependent views before removing columns
-- ============================================
-- Drop views that depend on make/model/year columns
DROP VIEW IF EXISTS reservation_details CASCADE;
DROP VIEW IF EXISTS company_reservations CASCADE;

-- ============================================
-- STEP 5: Remove make, model, year, and daily_rate columns from vehicles
-- ============================================
-- Drop the columns
ALTER TABLE vehicles DROP COLUMN IF EXISTS make CASCADE;
ALTER TABLE vehicles DROP COLUMN IF EXISTS model CASCADE;
ALTER TABLE vehicles DROP COLUMN IF EXISTS year CASCADE;
ALTER TABLE vehicles DROP COLUMN IF EXISTS daily_rate CASCADE;

-- Verify columns are gone
SELECT 
    column_name, 
    data_type 
FROM information_schema.columns 
WHERE table_name = 'vehicles'
ORDER BY ordinal_position;

-- ============================================
-- STEP 6: Restore vehicles from backup (without make/model/year)
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
    created_at,
    updated_at
)
SELECT 
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
    created_at,
    updated_at
FROM vehicles_backup;

-- Verify vehicles restored
SELECT 
    'Vehicles restored from backup' AS status,
    COUNT(*) AS vehicle_count
FROM vehicles;

-- ============================================
-- STEP 7: Populate vehicle_model from backup data
-- ============================================
-- First, ensure all models exist in models table
-- This will create models that don't exist yet
INSERT INTO models (make, model, year, category_id)
SELECT DISTINCT
    UPPER(TRIM(b.make)) AS make,
    UPPER(TRIM(b.model)) AS model,
    b.year AS year,
    CAST(NULL AS UUID) AS category_id
FROM vehicles_backup b
WHERE NOT EXISTS (
    SELECT 1 FROM models m 
    WHERE UPPER(TRIM(m.make)) = UPPER(TRIM(b.make))
    AND UPPER(TRIM(m.model)) = UPPER(TRIM(b.model))
    AND m.year = b.year
);

-- Now populate vehicle_model by linking vehicles to models
INSERT INTO vehicle_model (vehicle_id, model_id, daily_rate, created_at)
SELECT 
    b.id AS vehicle_id,
    m.id AS model_id,
    COALESCE(b.daily_rate, 0) AS daily_rate,  -- Use daily_rate from vehicles if available
    b.created_at AS created_at
FROM vehicles_backup b
INNER JOIN models m 
    ON UPPER(TRIM(b.make)) = UPPER(TRIM(m.make))
    AND UPPER(TRIM(b.model)) = UPPER(TRIM(m.model))
    AND b.year = m.year;

-- Verify vehicle_model populated
SELECT 
    'vehicle_model records created' AS status,
    COUNT(*) AS record_count
FROM vehicle_model;

-- ============================================
-- STEP 8: Verification - Check for orphaned vehicles
-- ============================================
-- Check if any vehicles don't have a vehicle_model record
SELECT 
    'Vehicles without vehicle_model link' AS status,
    COUNT(*) AS orphaned_count
FROM vehicles v
LEFT JOIN vehicle_model vm ON v.id = vm.vehicle_id
WHERE vm.vehicle_id IS NULL;

-- If there are orphaned vehicles, show them
SELECT 
    'Orphaned vehicles (need manual linking)' AS status,
    v.id,
    v.license_plate,
    v.created_at
FROM vehicles v
LEFT JOIN vehicle_model vm ON v.id = vm.vehicle_id
WHERE vm.vehicle_id IS NULL;

-- ============================================
-- STEP 9: Verification - Show sample data
-- ============================================
-- Show a sample of vehicle-model relationships
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    m.make,
    m.model,
    m.year,
    vm.daily_rate
FROM vehicles v
INNER JOIN vehicle_model vm ON v.id = vm.vehicle_id
INNER JOIN models m ON vm.model_id = m.id
LIMIT 10;

-- ============================================
-- STEP 10: Cleanup (optional - keep backup for safety)
-- ============================================
-- Uncomment to drop the backup table after verification
-- DROP TABLE IF EXISTS vehicles_backup;

-- Verify final state
SELECT 
    'Final state - vehicles count' AS check_type,
    COUNT(*) AS count
FROM vehicles
UNION ALL
SELECT 
    'Final state - vehicle_model count' AS check_type,
    COUNT(*) AS count
FROM vehicle_model
UNION ALL
SELECT 
    'Final state - vehicles without model' AS check_type,
    COUNT(*) AS count
FROM vehicles v
LEFT JOIN vehicle_model vm ON v.id = vm.vehicle_id
WHERE vm.vehicle_id IS NULL;

-- Show summary
SELECT 
    'MIGRATION COMPLETE' AS status,
    'Check results above for any issues' AS next_steps;

