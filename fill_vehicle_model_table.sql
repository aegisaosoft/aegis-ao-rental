--
-- Fill vehicle_model table with existing data
-- This script populates the vehicle_model junction table by matching vehicles to models
-- based on make, model name, and year
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

-- Step 1: Clear existing data to avoid duplicates
TRUNCATE TABLE vehicle_model;

-- Step 2: Insert vehicle-model relationships based on matching make, model, and year
-- Use the vehicle's daily_rate if available, otherwise use NULL
INSERT INTO vehicle_model (vehicle_id, model_id, daily_rate)
SELECT 
    v.id AS vehicle_id,
    m.id AS model_id,
    v.daily_rate AS daily_rate
FROM vehicles v
INNER JOIN models m 
    ON UPPER(TRIM(v.make)) = UPPER(TRIM(m.make))
    AND UPPER(TRIM(v.model)) = UPPER(TRIM(m.model))
    AND v.year = m.year
WHERE NOT EXISTS (
    SELECT 1 FROM vehicle_model vm 
    WHERE vm.vehicle_id = v.id AND vm.model_id = m.id
);

-- Step 3: Display statistics about the insert
SELECT 
    'Total records inserted' AS description,
    COUNT(*) AS count
FROM vehicle_model
UNION ALL
SELECT 
    'Vehicles without model match' AS description,
    COUNT(DISTINCT v.id) AS count
FROM vehicles v
LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
WHERE vm.vehicle_id IS NULL
UNION ALL
SELECT 
    'Vehicles with model match' AS description,
    COUNT(DISTINCT vm.vehicle_id) AS count
FROM vehicle_model vm;

-- Step 4: Show sample of inserted data
SELECT 
    v.id AS vehicle_id,
    v.make AS vehicle_make,
    v.model AS vehicle_model,
    v.year AS vehicle_year,
    m.id AS model_id,
    m.make AS model_make,
    m.model AS model_name,
    m.year AS model_year,
    vm.daily_rate
FROM vehicle_model vm
INNER JOIN vehicles v ON vm.vehicle_id = v.id
INNER JOIN models m ON vm.model_id = m.id
ORDER BY v.make, v.model, v.year
LIMIT 20;

-- Step 5: Show vehicles that couldn't be matched to models
SELECT 
    id,
    make,
    model,
    year,
    daily_rate
FROM vehicles v
WHERE NOT EXISTS (
    SELECT 1 FROM vehicle_model vm WHERE vm.vehicle_id = v.id
)
ORDER BY make, model, year;

-- Step 6: Summary of daily rates
SELECT 
    'Vehicles with daily rate in vehicle_model' AS category,
    COUNT(*) AS count
FROM vehicle_model
WHERE daily_rate IS NOT NULL
UNION ALL
SELECT 
    'Vehicles with NULL daily rate in vehicle_model' AS category,
    COUNT(*) AS count
FROM vehicle_model
WHERE daily_rate IS NULL;

-- Step 7: If you want to update NULL daily rates with model's daily rate, uncomment this:
-- UPDATE vehicle_model vm
-- SET daily_rate = m.daily_rate
-- FROM models m
-- WHERE vm.model_id = m.id
-- AND vm.daily_rate IS NULL
-- AND m.daily_rate IS NOT NULL;

