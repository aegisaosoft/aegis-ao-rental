--
-- MIGRATION SCRIPT: Fill vehicles and vehicle_model from vehicles_backup
-- This script processes each vehicle from backup:
-- 1. Insert vehicle data to vehicles table
-- 2. Find model by make/model/year
-- 3. Try to find vehicle_model record with model_id and company_id
-- 4. If found: update vehicle_model_id in vehicle
-- 5. If not: create vehicle_model record and update vehicle
--
-- IMPORTANT: Make sure you've already run recreate_vehicle_model_with_id.sql first!
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
-- Author: Alexander Orlov
--

-- ============================================
-- STEP 1: Verify vehicles_backup exists
-- ============================================
DO $$
BEGIN
    IF NOT EXISTS (SELECT FROM information_schema.tables WHERE table_name = 'vehicles_backup') THEN
        RAISE EXCEPTION 'vehicles_backup table does not exist! Please create backup first.';
    END IF;
END $$;

-- Show backup count
SELECT 
    'vehicles_backup exists' AS status,
    COUNT(*) AS backup_count
FROM vehicles_backup;

-- ============================================
-- STEP 2: Verify current tables are empty
-- ============================================
SELECT 
    'vehicles current count' AS status,
    COUNT(*) AS count
FROM vehicles;

SELECT 
    'vehicle_model current count' AS status,
    COUNT(*) AS count
FROM vehicle_model;

-- ============================================
-- STEP 3: Ensure all models exist in models table
-- ============================================
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

-- Verify models created
SELECT 
    'Models added from backup' AS status,
    COUNT(*) AS new_model_count
FROM models m
WHERE EXISTS (
    SELECT 1 FROM vehicles_backup b
    WHERE UPPER(TRIM(b.make)) = m.make
    AND UPPER(TRIM(b.model)) = m.model
    AND b.year = m.year
);

-- ============================================
-- STEP 4: Insert vehicles from backup
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

-- Verify vehicles inserted
SELECT 
    'Vehicles inserted' AS status,
    COUNT(*) AS vehicle_count
FROM vehicles;

-- ============================================
-- STEP 5: Process each vehicle to create/update vehicle_model
-- ============================================
-- Create vehicle_model records for each unique company_id + model_id combination
INSERT INTO vehicle_model (id, company_id, model_id, daily_rate, created_at)
SELECT DISTINCT ON (b.company_id, m.id)
    uuid_generate_v4() AS id,
    b.company_id,
    m.id AS model_id,
    COALESCE(AVG(b.daily_rate)::numeric(10,2), 0) AS daily_rate,
    MIN(b.created_at) AS created_at
FROM vehicles_backup b
INNER JOIN models m 
    ON UPPER(TRIM(b.make)) = m.make
    AND UPPER(TRIM(b.model)) = m.model
    AND b.year = m.year
WHERE NOT EXISTS (
    SELECT 1 FROM vehicle_model vm 
    WHERE vm.company_id = b.company_id 
    AND vm.model_id = m.id
)
GROUP BY b.company_id, m.id;

-- Verify vehicle_model created
SELECT 
    'vehicle_model records created' AS status,
    COUNT(*) AS record_count
FROM vehicle_model;

-- Show sample vehicle_model entries
SELECT 
    vm.id,
    vm.company_id,
    vm.model_id,
    vm.daily_rate,
    c.company_name,
    m.make,
    m.model,
    m.year
FROM vehicle_model vm
INNER JOIN models m ON vm.model_id = m.id
INNER JOIN companies c ON vm.company_id = c.id
LIMIT 10;

-- ============================================
-- STEP 6: Update vehicles with vehicle_model_id
-- ============================================
UPDATE vehicles v
SET vehicle_model_id = vm.id
FROM vehicles_backup vb
INNER JOIN models m 
    ON UPPER(TRIM(vb.make)) = m.make
    AND UPPER(TRIM(vb.model)) = m.model
    AND vb.year = m.year
INNER JOIN vehicle_model vm 
    ON vm.company_id = vb.company_id
    AND vm.model_id = m.id
WHERE v.id = vb.id;

-- Verify vehicles updated
SELECT 
    'Vehicles with vehicle_model_id' AS status,
    COUNT(*) AS count
FROM vehicles
WHERE vehicle_model_id IS NOT NULL;

SELECT 
    'Vehicles WITHOUT vehicle_model_id' AS status,
    COUNT(*) AS count
FROM vehicles
WHERE vehicle_model_id IS NULL;

-- ============================================
-- STEP 7: Final verification
-- ============================================
-- Check for orphaned records
SELECT 
    'vehicles_backup without model' AS check_type,
    COUNT(*) AS count
FROM vehicles_backup b
WHERE NOT EXISTS (
    SELECT 1 FROM models m 
    WHERE UPPER(TRIM(b.make)) = m.make
    AND UPPER(TRIM(b.model)) = m.model
    AND b.year = m.year
);

-- Summary
SELECT 
    'vehicles_backup' AS table_name,
    COUNT(*) AS row_count
FROM vehicles_backup
UNION ALL
SELECT 
    'vehicles' AS table_name,
    COUNT(*) AS row_count
FROM vehicles
UNION ALL
SELECT 
    'vehicle_model' AS table_name,
    COUNT(*) AS row_count
FROM vehicle_model
UNION ALL
SELECT 
    'models' AS table_name,
    COUNT(*) AS row_count
FROM models;

-- Show final state
SELECT 
    'FINAL STATE - All vehicles linked' AS check_type,
    COUNT(*) AS total_vehicles,
    COUNT(v.vehicle_model_id) AS vehicles_with_model,
    COUNT(*) - COUNT(v.vehicle_model_id) AS vehicles_without_model
FROM vehicles v;
