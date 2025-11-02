--
-- Remove vehicles with "LDAD AUDI" model from the database
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Show what will be deleted (preview)
SELECT 
    'vehicles' AS table_name,
    COUNT(*) AS records_to_delete
FROM vehicles
WHERE UPPER(model) = 'LDAD AUDI';

-- Delete vehicle_model records that reference vehicles with "LDAD AUDI" model
DELETE FROM vehicle_model
WHERE vehicle_id IN (
    SELECT id FROM vehicles WHERE UPPER(model) = 'LDAD AUDI'
);

-- Delete vehicles with "LDAD AUDI" model from vehicles table
DELETE FROM vehicles
WHERE UPPER(model) = 'LDAD AUDI';

-- Verify deletion
SELECT 
    'LDAD AUDI vehicles remaining in vehicles table' AS check_type,
    COUNT(*) AS count
FROM vehicles
WHERE UPPER(model) = 'LDAD AUDI';

SELECT 
    'LDAD AUDI vehicle_model records remaining' AS check_type,
    COUNT(*) AS count
FROM vehicle_model vm
INNER JOIN vehicles v ON vm.vehicle_id = v.id
WHERE UPPER(v.model) = 'LDAD AUDI';

