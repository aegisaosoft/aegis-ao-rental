--
-- Fix Volkswagen model name from "UP" to "UP!"
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Update vehicles table
UPDATE vehicles
SET model = 'UP!'
WHERE make = 'VOLKSWAGEN' AND model = 'UP';

-- Update models table
UPDATE models
SET model = 'UP!'
WHERE make = 'VOLKSWAGEN' AND model = 'UP';

-- Verify the changes
SELECT 
    'vehicles' AS table_name,
    COUNT(*) AS updated_count
FROM vehicles
WHERE make = 'VOLKSWAGEN' AND model = 'UP!'
UNION ALL
SELECT 
    'models' AS table_name,
    COUNT(*) AS updated_count
FROM models
WHERE make = 'VOLKSWAGEN' AND model = 'UP!';

