--
-- Add missing models for vehicles that don't have matching models
-- This script creates models based on vehicles without vehicle_model records
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Step 1: Show what will be added (preview)
SELECT 
    'Missing models to be added' AS description,
    COUNT(*) AS count
FROM (
    SELECT DISTINCT
        v.make,
        v.model,
        v.year
    FROM vehicles v
    LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
    WHERE vm.vehicle_id IS NULL
    AND NOT EXISTS (
        SELECT 1 FROM models m 
        WHERE UPPER(TRIM(m.make)) = UPPER(TRIM(v.make))
        AND UPPER(TRIM(m.model)) = UPPER(TRIM(v.model))
        AND m.year = v.year
    )
) missing_models;

-- Step 2: Insert missing models from unmatched vehicles
-- Note: We check NOT EXISTS to avoid duplicates since there's no unique constraint
INSERT INTO models (make, model, year, daily_rate)
SELECT DISTINCT
    UPPER(TRIM(v.make)) AS make,
    UPPER(TRIM(v.model)) AS model,
    v.year AS year,
    v.daily_rate AS daily_rate
FROM vehicles v
LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
WHERE vm.vehicle_id IS NULL
AND NOT EXISTS (
    SELECT 1 FROM models m 
    WHERE UPPER(TRIM(m.make)) = UPPER(TRIM(v.make))
    AND UPPER(TRIM(m.model)) = UPPER(TRIM(v.model))
    AND m.year = v.year
);

-- Step 3: Now link vehicles to the newly created models
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

-- Step 4: Verify results
SELECT 
    'Total models now in models table' AS description,
    COUNT(*) AS count
FROM models
UNION ALL
SELECT 
    'Total records in vehicle_model table' AS description,
    COUNT(*) AS count
FROM vehicle_model
UNION ALL
SELECT 
    'Vehicles still without vehicle_model records' AS description,
    COUNT(DISTINCT v.id) AS count
FROM vehicles v
LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
WHERE vm.vehicle_id IS NULL;

-- Step 5: Show sample of models from unmatched vehicles (these were likely just added)
SELECT 
    make,
    model,
    year,
    daily_rate
FROM (
    SELECT DISTINCT
        v.make,
        v.model,
        v.year,
        v.daily_rate
    FROM vehicles v
    LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
    WHERE vm.vehicle_id IS NULL
) sample_models
ORDER BY make, model, year
LIMIT 20;

