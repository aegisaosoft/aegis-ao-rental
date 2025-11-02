--
-- Find vehicles that don't have records in vehicle_model table
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Query: Find vehicles without vehicle_model records
SELECT 
    v.make,
    v.model,
    v.year
FROM vehicles v
LEFT JOIN vehicle_model vm ON vm.vehicle_id = v.id
WHERE vm.vehicle_id IS NULL
GROUP BY v.make, v.model, v.year
ORDER BY v.make, v.model, v.year;
