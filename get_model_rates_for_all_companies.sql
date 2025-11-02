--
-- Get model rates from vehicle_model and models tables for all companies
-- This query shows which companies have which vehicle models and their rates
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Option 1: Get all unique vehicle models with their rates and company counts
SELECT 
    m.make,
    m.model,
    m.year,
    m.category_id,
    vc.category_name,
    vm.daily_rate,
    vm.created_at,
    COUNT(DISTINCT v.company_id) AS company_count,
    COUNT(v.id) AS vehicle_count,
    STRING_AGG(DISTINCT c.company_name, ', ' ORDER BY c.company_name) AS companies
FROM vehicle_model vm
INNER JOIN models m ON vm.model_id = m.id
LEFT JOIN vehicle_categories vc ON m.category_id = vc.id
LEFT JOIN vehicles v ON v.vehicle_model_id = vm.model_id
LEFT JOIN companies c ON v.company_id = c.id
GROUP BY 
    m.make,
    m.model,
    m.year,
    m.category_id,
    vc.category_name,
    vm.daily_rate,
    vm.created_at
ORDER BY 
    m.make,
    m.model,
    m.year;

-- Option 2: Detailed view - get all vehicles grouped by company and model
SELECT 
    c.company_name,
    m.make,
    m.model,
    m.year,
    vc.category_name,
    vm.daily_rate AS catalog_daily_rate,
    COUNT(v.id) AS vehicle_count,
    MIN(v.license_plate) AS sample_license_plate
FROM companies c
INNER JOIN vehicles v ON v.company_id = c.id
INNER JOIN vehicle_model vm ON v.vehicle_model_id = vm.model_id
INNER JOIN models m ON vm.model_id = m.id
LEFT JOIN vehicle_categories vc ON m.category_id = vc.id
GROUP BY 
    c.company_name,
    m.make,
    m.model,
    m.year,
    vc.category_name,
    vm.daily_rate
ORDER BY 
    c.company_name,
    m.make,
    m.model,
    m.year;

-- Option 3: Summary by company showing model rates
SELECT 
    c.company_name,
    COUNT(DISTINCT vm.model_id) AS unique_models,
    COUNT(v.id) AS total_vehicles,
    MIN(vm.daily_rate) AS min_rate,
    MAX(vm.daily_rate) AS max_rate,
    AVG(vm.daily_rate) AS avg_rate,
    SUM(vm.daily_rate) AS total_rate_sum
FROM companies c
INNER JOIN vehicles v ON v.company_id = c.id
INNER JOIN vehicle_model vm ON v.vehicle_model_id = vm.model_id
INNER JOIN models m ON vm.model_id = m.id
GROUP BY c.company_name
ORDER BY c.company_name;

-- Option 4: Get models with rates but NO vehicles assigned (catalog entries without fleet)
SELECT 
    m.make,
    m.model,
    m.year,
    vc.category_name,
    vm.daily_rate,
    vm.created_at,
    'No vehicles assigned' AS status
FROM vehicle_model vm
INNER JOIN models m ON vm.model_id = m.id
LEFT JOIN vehicle_categories vc ON m.category_id = vc.id
LEFT JOIN vehicles v ON v.vehicle_model_id = vm.model_id
WHERE v.id IS NULL
ORDER BY m.make, m.model, m.year;

-- Option 5: Get all models (with or without catalog entry) showing which have rates
SELECT 
    m.make,
    m.model,
    m.year,
    vc.category_name,
    CASE 
        WHEN vm.model_id IS NOT NULL THEN vm.daily_rate
        ELSE NULL
    END AS daily_rate,
    CASE 
        WHEN vm.model_id IS NOT NULL THEN 'Has rate catalog'
        ELSE 'No rate catalog'
    END AS catalog_status,
    COUNT(v.id) AS vehicle_count
FROM models m
LEFT JOIN vehicle_categories vc ON m.category_id = vc.id
LEFT JOIN vehicle_model vm ON m.id = vm.model_id
LEFT JOIN vehicles v ON v.vehicle_model_id = vm.model_id
GROUP BY 
    m.make,
    m.model,
    m.year,
    vc.category_name,
    vm.daily_rate,
    vm.model_id
ORDER BY 
    m.make,
    m.model,
    m.year;

