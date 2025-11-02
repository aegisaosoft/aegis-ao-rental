-- Quick check to see current state of vehicles and vehicle_model
-- Run this to understand what migration was executed

-- Check if vehicles still has make/model/year/daily_rate
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns
WHERE table_name = 'vehicles'
AND column_name IN ('make', 'model', 'year', 'daily_rate', 'vehicle_model_id')
ORDER BY column_name;

-- Check vehicle_model table structure
SELECT 
    column_name, 
    data_type, 
    is_nullable
FROM information_schema.columns
WHERE table_name = 'vehicle_model'
ORDER BY ordinal_position;

-- Check primary key of vehicle_model
SELECT 
    constraint_name,
    constraint_type
FROM information_schema.table_constraints
WHERE table_name = 'vehicle_model'
AND constraint_type = 'PRIMARY KEY';

-- Check counts
SELECT 
    'vehicles' AS table_name,
    COUNT(*) AS record_count
FROM vehicles
UNION ALL
SELECT 
    'vehicle_model' AS table_name,
    COUNT(*) AS record_count
FROM vehicle_model;

-- Check if vehicles have vehicle_model_id
SELECT 
    COUNT(*) AS vehicles_with_link,
    COUNT(*) FILTER (WHERE vehicle_model_id IS NOT NULL) AS with_link,
    COUNT(*) FILTER (WHERE vehicle_model_id IS NULL) AS without_link
FROM vehicles;

-- Sample data
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    v.make,
    v.model,
    v.year,
    v.daily_rate,
    v.vehicle_model_id,
    vm.model_id,
    vm.daily_rate AS vm_daily_rate
FROM vehicles v
LEFT JOIN vehicle_model vm ON v.vehicle_model_id = vm.model_id
LIMIT 5;

