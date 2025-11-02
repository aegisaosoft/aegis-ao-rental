--
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
-- 
-- THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
-- Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
-- SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
-- WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
-- Alexander Orlov.
-- 
--  Author: Alexander Orlov
-- 
--
-- This script adds a UUID id column to vehicle_model table and makes it the primary key
-- It then updates the vehicle.vehicle_model_id to reference vehicle_model.id
-- WARNING: This script will DELETE ALL DATA from vehicles and vehicle_model tables

-- Step 0: Delete all data from vehicles and vehicle_model first
-- This is necessary because foreign key constraints prevent schema changes
SET session_replication_role = 'replica';

-- Delete all data from vehicles (cascade to vehicle_model)
TRUNCATE TABLE vehicles CASCADE;

-- Delete any remaining data from vehicle_model
DELETE FROM vehicle_model;

-- Re-enable foreign key checks
SET session_replication_role = 'origin';

-- Step 1: Drop ALL foreign key constraints that might prevent changes
-- Note: CASCADE will drop any dependent constraints
ALTER TABLE vehicles DROP CONSTRAINT IF EXISTS fk_vehicles_vehicle_model CASCADE;
ALTER TABLE vehicles DROP CONSTRAINT IF EXISTS fk_vehicles_vehicle_model_id CASCADE;

-- Step 2: Add id column to vehicle_model table with NOT NULL and default
-- Since table is empty, we can add it directly as NOT NULL with default
ALTER TABLE vehicle_model ADD COLUMN id UUID DEFAULT uuid_generate_v4();
ALTER TABLE vehicle_model ALTER COLUMN id SET NOT NULL;

-- Step 3: Drop the existing composite primary key (should work now since we dropped FKs with CASCADE)
ALTER TABLE vehicle_model DROP CONSTRAINT IF EXISTS pk_vehicle_model;

-- Step 4: Set id as the new primary key
ALTER TABLE vehicle_model ADD CONSTRAINT pk_vehicle_model PRIMARY KEY (id);

-- Step 5: Create unique constraint on (vehicle_id, model_id) to prevent duplicates
ALTER TABLE vehicle_model ADD CONSTRAINT IF NOT EXISTS uq_vehicle_model_vehicle_model UNIQUE (vehicle_id, model_id);

-- Step 6: Recreate foreign key from vehicles to vehicle_model
ALTER TABLE vehicles 
    ADD CONSTRAINT fk_vehicles_vehicle_model 
    FOREIGN KEY (vehicle_model_id) 
    REFERENCES vehicle_model(id) 
    ON DELETE SET NULL;

-- Step 7: Add back foreign key from vehicle_model to models (if it doesn't exist)
ALTER TABLE vehicle_model 
    ADD CONSTRAINT IF NOT EXISTS fk_vehicle_model_model 
    FOREIGN KEY (model_id) 
    REFERENCES models(id) 
    ON DELETE CASCADE;

-- Step 8: Verify the structure
SELECT 
    'vehicle_model' AS table_name,
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'vehicle_model'
ORDER BY ordinal_position;

-- Show constraints for vehicle_model
SELECT
    'vehicle_model' AS table_name,
    conname AS constraint_name,
    contype AS constraint_type,
    pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE conrelid = 'vehicle_model'::regclass
ORDER BY contype, conname;
