--
-- SQL QUERY: Find vehicles that don't have vehicle_model entries
-- This query helps identify data inconsistency issues
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
-- QUERY 1: Vehicles without vehicle_model_id
-- ============================================
SELECT 
    'Vehicles without vehicle_model_id' AS check_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.company_id,
    c.company_name,
    v.created_at
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
WHERE v.vehicle_model_id IS NULL
ORDER BY v.company_id, v.created_at;

-- ============================================
-- QUERY 2: Vehicles with vehicle_model_id that doesn't exist in vehicle_model table
-- ============================================
SELECT 
    'Vehicles with invalid vehicle_model_id' AS check_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.vehicle_model_id,
    v.company_id,
    c.company_name,
    v.created_at
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
WHERE v.vehicle_model_id IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM vehicle_model vm WHERE vm.id = v.vehicle_model_id
)
ORDER BY v.company_id, v.created_at;

-- ============================================
-- QUERY 3: Summary counts
-- ============================================
SELECT 
    'Summary' AS check_type,
    (SELECT COUNT(*) FROM vehicles WHERE vehicle_model_id IS NULL) AS vehicles_without_reference,
    (SELECT COUNT(*) FROM vehicles WHERE vehicle_model_id IS NOT NULL 
     AND NOT EXISTS (SELECT 1 FROM vehicle_model vm WHERE vm.id = vehicles.vehicle_model_id)) AS vehicles_with_invalid_reference,
    (SELECT COUNT(*) FROM vehicles) AS total_vehicles,
    (SELECT COUNT(*) FROM vehicle_model) AS total_vehicle_model_records;

-- ============================================
-- QUERY 4: Vehicles that should have vehicle_model but don't (with backup data)
-- ============================================
SELECT 
    'Vehicles missing vehicle_model entries' AS check_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.company_id,
    c.company_name,
    vb.make,
    vb.model,
    vb.year,
    vb.daily_rate,
    v.created_at
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
LEFT JOIN vehicles_backup vb ON v.id = vb.id
WHERE v.vehicle_model_id IS NULL
ORDER BY v.company_id, vb.make, vb.model, vb.year;

-- ============================================
-- QUERY 5: All orphaned vehicle references
-- ============================================
SELECT 
    'ALL_VEHICLES_MISSING_LINKS' AS report_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.company_id,
    c.company_name,
    v.vehicle_model_id,
    v.created_at,
    vb.make,
    vb.model,
    vb.year
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
LEFT JOIN vehicles_backup vb ON v.id = vb.id
WHERE 
    v.vehicle_model_id IS NULL 
    OR NOT EXISTS (
        SELECT 1 FROM vehicle_model vm WHERE vm.id = v.vehicle_model_id
    )
ORDER BY v.company_id, v.created_at;

