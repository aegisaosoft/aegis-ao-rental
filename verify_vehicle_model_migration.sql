-- Verification query to check vehicle-model relationships
-- This shows how many vehicles share each model

SELECT 
    m.make,
    m.model,
    m.year,
    COUNT(vm.vehicle_id) AS vehicle_count
FROM models m
LEFT JOIN vehicle_model vm ON m.id = vm.model_id
GROUP BY m.make, m.model, m.year
HAVING COUNT(vm.vehicle_id) > 0
ORDER BY vehicle_count DESC
LIMIT 20;

-- Sample of actual relationships
SELECT 
    v.id AS vehicle_id,
    v.license_plate,
    m.make || ' ' || m.model || ' ' || m.year::text AS model_name,
    vm.daily_rate
FROM vehicles v
INNER JOIN vehicle_model vm ON v.id = vm.vehicle_id
INNER JOIN models m ON vm.model_id = m.id
LIMIT 10;

