--
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
-- 
-- THIS SOFTWARE IS THE CONFIDENTIAL INFORMATION OF
-- Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
-- SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
-- WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
-- Alexander Orlov.
-- 
--  Author: Alexander Orlov
-- 
--
-- WARNING: This script will DELETE ALL DATA from vehicles and vehicle_model tables
-- Make sure you have a backup before running this!

-- Step 1: Disable foreign key checks temporarily
SET session_replication_role = 'replica';

-- Step 2: Delete all data from vehicle_model first (due to foreign key constraints)
TRUNCATE TABLE vehicle_model CASCADE;

-- Step 3: Delete all data from vehicles
TRUNCATE TABLE vehicles CASCADE;

-- Step 4: Re-enable foreign key checks
SET session_replication_role = 'origin';

-- Step 5: Verify tables are empty
SELECT 'vehicles' AS table_name, COUNT(*) AS row_count FROM vehicles
UNION ALL
SELECT 'vehicle_model' AS table_name, COUNT(*) AS row_count FROM vehicle_model;

-- Step 6: Show table structure
SELECT 
    'vehicles' AS table_name,
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'vehicles'
ORDER BY ordinal_position;

SELECT 
    'vehicle_model' AS table_name,
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'vehicle_model'
ORDER BY ordinal_position;

