--
-- Add daily_rate column back to vehicles table for company-specific rates
-- This allows each company to have their own rates per vehicle
-- Falls back to vehicle_model catalog rate if vehicle.daily_rate is NULL
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Add daily_rate column to vehicles (nullable - defaults to catalog rate)
ALTER TABLE vehicles 
ADD COLUMN IF NOT EXISTS daily_rate NUMERIC(10,2);

-- Populate with rates from vehicle_model catalog for existing vehicles
UPDATE vehicles v
SET daily_rate = vm.daily_rate
FROM vehicle_model vm
WHERE v.vehicle_model_id = vm.model_id
  AND v.daily_rate IS NULL;

-- Verify the column was added and populated
SELECT 
    'vehicles.daily_rate column added' AS status,
    COUNT(*) AS total_vehicles,
    COUNT(v.daily_rate) AS vehicles_with_rate,
    COUNT(*) - COUNT(v.daily_rate) AS vehicles_without_rate
FROM vehicles v;

-- Show sample of populated rates
SELECT 
    v.id,
    c.company_name,
    m.make,
    m.model,
    m.year,
    vm.daily_rate AS catalog_rate,
    v.daily_rate AS vehicle_rate
FROM vehicles v
LEFT JOIN vehicle_model vm ON v.vehicle_model_id = vm.model_id
LEFT JOIN models m ON vm.model_id = m.id
LEFT JOIN companies c ON v.company_id = c.id
ORDER BY c.company_name, m.make, m.model, m.year
LIMIT 20;

