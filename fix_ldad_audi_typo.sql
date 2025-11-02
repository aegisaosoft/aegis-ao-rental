--
-- Fix "LDAD AUDI" typo to "LOAD AUDI"
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- Show what will be updated (preview)
SELECT 
    'models' AS table_name,
    COUNT(*) AS records_to_update
FROM models
WHERE UPPER(model) = 'LDAD AUDI';

-- Update models table: LDAD AUDI -> LOAD AUDI
UPDATE models
SET model = 'LOAD AUDI'
WHERE UPPER(model) = 'LDAD AUDI';

-- Verify the fix
SELECT 
    'LDAD AUDI models remaining' AS check_type,
    COUNT(*) AS count
FROM models
WHERE UPPER(model) = 'LDAD AUDI';

SELECT 
    'LOAD AUDI models now exist' AS check_type,
    COUNT(*) AS count
FROM models
WHERE UPPER(model) = 'LOAD AUDI';

