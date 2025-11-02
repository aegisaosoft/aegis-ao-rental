--
-- FULL MIGRATION: Remove make, model, and year columns from vehicles table
-- This migration removes redundant columns from vehicles table since all
-- vehicle-model relationships are now managed through the vehicle_model junction table.
--
-- IMPORTANT ARCHITECTURE:
-- - Vehicles link to Models ONLY through vehicle_model table (junction table)
-- - To get make/model/year of a vehicle, you must JOIN through vehicle_model
-- - Each vehicle MUST have exactly ONE model in vehicle_model table
--
-- This is a DESTRUCTIVE migration - make sure to backup before running!
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- ============================================
-- STEP 1: Review data before migration
-- ============================================
-- Run this first to see how many vehicles have make/model/year data
SELECT 
    COUNT(*) AS total_vehicles,
    COUNT(DISTINCT make) AS unique_makes,
    COUNT(DISTINCT model) AS unique_models,
    COUNT(DISTINCT year) AS unique_years,
    MIN(year) AS oldest_year,
    MAX(year) AS newest_year
FROM vehicles
WHERE make IS NOT NULL AND model IS NOT NULL AND year IS NOT NULL;

-- ============================================
-- STEP 2: Verify vehicle_model relationship exists
-- ============================================
-- Check if all vehicles have corresponding vehicle_model records
SELECT 
    'Vehicles in vehicles table' AS source,
    COUNT(*) AS count
FROM vehicles
UNION ALL
SELECT 
    'Vehicle_model records' AS source,
    COUNT(*) AS count
FROM vehicle_model
UNION ALL
SELECT 
    'Vehicles without vehicle_model' AS source,
    COUNT(*) AS count
FROM vehicles v
LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
WHERE vm.vehicle_id IS NULL;

-- CRITICAL: Check for vehicles with multiple models
SELECT 
    v.id,
    v.make,
    v.model,
    v.year,
    COUNT(vm.model_id) AS model_count,
    STRING_AGG(m.make || ' ' || m.model_name || ' ' || m.year::text, ', ') AS models
FROM vehicles v
INNER JOIN vehicle_model vm ON v.id = vm.vehicle_id
INNER JOIN models m ON vm.model_id = m.id
GROUP BY v.id, v.make, v.model, v.year
HAVING COUNT(vm.model_id) > 1;

-- ============================================
-- STEP 3: Ensure each vehicle has only ONE model in vehicle_model
-- ============================================
-- If there are vehicles with multiple models, keep only the first one
-- This is critical! Vehicles must have exactly one model

-- First, delete duplicate model links, keeping only the first one by created_at
-- DELETE FROM vehicle_model vm1
-- USING vehicle_model vm2
-- WHERE vm1.vehicle_id = vm2.vehicle_id
-- AND vm1.created_at > vm2.created_at; -- Keep the older one

-- Then change the primary key to be just vehicle_id (1-to-1 relationship)
-- This will enforce that each vehicle can only link to one model
-- ALTER TABLE vehicle_model
-- DROP CONSTRAINT IF EXISTS pk_vehicle_model;

-- ALTER TABLE vehicle_model
-- ADD CONSTRAINT pk_vehicle_model PRIMARY KEY (vehicle_id);

-- Add a NOT NULL constraint on vehicle_id (should already exist as it's FK)
-- ALTER TABLE vehicle_model
-- ALTER COLUMN vehicle_id SET NOT NULL;

-- ============================================
-- STEP 4: Create backup (optional but recommended)
-- ============================================
-- Uncomment to create a backup table
-- CREATE TABLE vehicles_backup AS SELECT * FROM vehicles;

-- ============================================
-- STEP 5: DROP COLUMNS
-- ============================================
-- Uncomment to actually drop the columns
-- WARNING: This will delete all make, model, and year data from vehicles table!
-- ALTER TABLE vehicles DROP COLUMN IF EXISTS make;
-- ALTER TABLE vehicles DROP COLUMN IF EXISTS model;
-- ALTER TABLE vehicles DROP COLUMN IF EXISTS year;

-- ============================================
-- VERIFICATION
-- ============================================
-- After dropping, verify the columns are gone
-- SELECT column_name, data_type 
-- FROM information_schema.columns 
-- WHERE table_name = 'vehicles'
-- ORDER BY ordinal_position;

-- Verify all vehicles still have their model data accessible through vehicle_model
-- SELECT 
--     COUNT(*) AS total_vehicles_with_model_info
-- FROM vehicles v
-- INNER JOIN vehicle_model vm ON v.id = vm.vehicle_id
-- INNER JOIN models m ON vm.model_id = m.id;

