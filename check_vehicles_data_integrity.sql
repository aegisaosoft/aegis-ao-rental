--
-- SQL QUERY: Check vehicles data integrity after migration
-- This query validates that vehicles backup data matches the migrated data
-- Checks: make, model, year, daily_rate consistency
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
-- CHECK 1: Vehicles with mismatched make/model/year
-- ============================================
SELECT 
    'Mismatched make/model/year' AS check_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.company_id,
    c.company_name,
    'Backup' AS source,
    vb.make AS backup_make,
    vb.model AS backup_model,
    vb.year AS backup_year,
    vb.daily_rate AS backup_daily_rate,
    'Current' AS current_source,
    m.make AS current_make,
    m.model_name AS current_model,
    m.year AS current_year,
    vm.daily_rate AS current_daily_rate
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
LEFT JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
LEFT JOIN models m ON vm.model_id = m.id
WHERE 
    (UPPER(TRIM(vb.make)) != UPPER(TRIM(m.make)) 
     OR UPPER(TRIM(vb.model)) != UPPER(TRIM(m.model_name))
     OR vb.year != m.year)
ORDER BY v.company_id, v.license_plate;

-- ============================================
-- CHECK 2: Count of vehicles with mismatched data
-- ============================================
SELECT 
    'Summary - Mismatched records' AS check_type,
    COUNT(*) AS mismatch_count,
    COUNT(DISTINCT v.company_id) AS affected_companies
FROM vehicles v
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
LEFT JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
LEFT JOIN models m ON vm.model_id = m.id
WHERE 
    (UPPER(TRIM(vb.make)) != UPPER(TRIM(m.make)) 
     OR UPPER(TRIM(vb.model)) != UPPER(TRIM(m.model_name))
     OR vb.year != m.year);

-- ============================================
-- CHECK 3: Vehicles missing in vehicle_model
-- ============================================
SELECT 
    'Missing vehicle_model entries' AS check_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.company_id,
    c.company_name,
    v.vehicle_model_id,
    vb.make,
    vb.model,
    vb.year
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
WHERE v.vehicle_model_id IS NULL
ORDER BY v.company_id, v.license_plate;

-- ============================================
-- CHECK 4: Vehicles with invalid vehicle_model_id
-- ============================================
SELECT 
    'Invalid vehicle_model_id' AS check_type,
    v.id AS vehicle_id,
    v.license_plate,
    v.vehicle_model_id,
    v.company_id,
    c.company_name,
    vb.make,
    vb.model,
    vb.year
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
WHERE v.vehicle_model_id IS NOT NULL
AND NOT EXISTS (
    SELECT 1 FROM vehicle_model vm WHERE vm.id = v.vehicle_model_id
)
ORDER BY v.company_id, v.license_plate;

-- ============================================
-- CHECK 5: Count vehicles missing vehicle_model completely
-- ============================================
SELECT 
    'Summary - Missing vehicle_model' AS check_type,
    COUNT(*) AS missing_count,
    COUNT(DISTINCT v.company_id) AS affected_companies
FROM vehicles v
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
WHERE v.vehicle_model_id IS NULL;

-- ============================================
-- CHECK 6: All vehicles verification (no mismatches)
-- ============================================
SELECT 
    'Summary - All correct' AS check_type,
    COUNT(*) AS correct_count,
    COUNT(DISTINCT v.company_id) AS companies
FROM vehicles v
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
INNER JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
INNER JOIN models m ON vm.model_id = m.id
WHERE 
    UPPER(TRIM(vb.make)) = UPPER(TRIM(m.make)) 
    AND UPPER(TRIM(vb.model)) = UPPER(TRIM(m.model_name))
    AND vb.year = m.year;

-- ============================================
-- CHECK 7: Overall summary
-- ============================================
SELECT 
    'Total vehicles in vehicles table' AS check_type,
    COUNT(*) AS count
FROM vehicles
UNION ALL
SELECT 
    'Total vehicles in vehicles_backup table',
    COUNT(*) 
FROM vehicles_backup
UNION ALL
SELECT 
    'Total vehicle_model entries',
    COUNT(*) 
FROM vehicle_model
UNION ALL
SELECT 
    'Vehicles with vehicle_model_id set',
    COUNT(*) 
FROM vehicles 
WHERE vehicle_model_id IS NOT NULL
UNION ALL
SELECT 
    'Vehicles without vehicle_model_id',
    COUNT(*) 
FROM vehicles 
WHERE vehicle_model_id IS NULL;

-- ============================================
-- CHECK 8: Detailed comparison per vehicle
-- ============================================
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    v.company_id,
    c.company_name,
    vb.make AS backup_make,
    vb.model AS backup_model,
    vb.year AS backup_year,
    vb.daily_rate AS backup_daily_rate,
    m.make AS current_make,
    m.model_name AS current_model,
    m.year AS current_year,
    vm.daily_rate AS current_daily_rate,
    v.vehicle_model_id,
    vm.id AS vehicle_model_id_from_table,
    vm.company_id AS vm_company_id,
    vm.model_id AS vm_model_id,
    CASE 
        WHEN v.vehicle_model_id IS NULL THEN 'MISSING'
        WHEN UPPER(TRIM(vb.make)) != UPPER(TRIM(m.make)) THEN 'MAKE_MISMATCH'
        WHEN UPPER(TRIM(vb.model)) != UPPER(TRIM(m.model_name)) THEN 'MODEL_MISMATCH'
        WHEN vb.year != m.year THEN 'YEAR_MISMATCH'
        ELSE 'OK'
    END AS status
FROM vehicles v
INNER JOIN companies c ON v.company_id = c.id
INNER JOIN vehicles_backup vb ON v.license_plate = vb.license_plate AND v.company_id = vb.company_id
LEFT JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
LEFT JOIN models m ON vm.model_id = m.id
ORDER BY 
    CASE 
        WHEN v.vehicle_model_id IS NULL THEN 1
        WHEN UPPER(TRIM(vb.make)) != UPPER(TRIM(m.make)) THEN 2
        WHEN UPPER(TRIM(vb.model)) != UPPER(TRIM(m.model_name)) THEN 3
        WHEN vb.year != m.year THEN 4
        ELSE 5
    END,
    v.company_id, 
    v.license_plate;

