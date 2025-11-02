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
-- WARNING: This script will DELETE ALL DATA from vehicles and vehicle_model tables
-- It will then recreate vehicle_model with a UUID id primary key
-- Make sure you have a backup before running this!

-- Step 1: Delete all vehicles first (CASCADE will delete related vehicle_model entries)
-- Note: We use CASCADE to clean up related data
SET session_replication_role = 'replica';

-- Step 2: Delete all data from vehicles (cascade to vehicle_model)
TRUNCATE TABLE vehicles CASCADE;

-- Step 3: Drop the vehicle_model table completely
DROP TABLE IF EXISTS vehicle_model CASCADE;

-- Step 4: Re-enable foreign key checks
SET session_replication_role = 'origin';

-- Step 5: Recreate vehicle_model table with id as primary key
CREATE TABLE vehicle_model (
    id UUID DEFAULT uuid_generate_v4() NOT NULL,
    company_id UUID NOT NULL,
    model_id UUID NOT NULL,
    daily_rate NUMERIC(10, 2),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_vehicle_model PRIMARY KEY (id),
    CONSTRAINT fk_vehicle_model_company FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    CONSTRAINT fk_vehicle_model_model FOREIGN KEY (model_id) REFERENCES models(id) ON DELETE CASCADE,
    CONSTRAINT uq_vehicle_model_company_model UNIQUE (company_id, model_id)
);

-- Step 6: Create indexes for better query performance
CREATE INDEX idx_vehicle_model_company_id ON vehicle_model(company_id);
CREATE INDEX idx_vehicle_model_model_id ON vehicle_model(model_id);

-- Step 7: Add foreign key from vehicles to vehicle_model
ALTER TABLE vehicles 
    ADD CONSTRAINT fk_vehicles_vehicle_model 
    FOREIGN KEY (vehicle_model_id) 
    REFERENCES vehicle_model(id) 
    ON DELETE SET NULL;

-- Step 8: Add comments
COMMENT ON TABLE vehicle_model IS 'Per-company catalog table for models with daily rates';
COMMENT ON COLUMN vehicle_model.id IS 'Primary key - UUID';
COMMENT ON COLUMN vehicle_model.company_id IS 'Reference to the company';
COMMENT ON COLUMN vehicle_model.model_id IS 'Reference to the model';
COMMENT ON COLUMN vehicle_model.daily_rate IS 'Daily rate for this model at this company';
COMMENT ON COLUMN vehicle_model.created_at IS 'When the catalog entry was created';

-- Step 9: Verify tables are empty
SELECT 'vehicles' AS table_name, COUNT(*) AS row_count FROM vehicles
UNION ALL
SELECT 'vehicle_model' AS table_name, COUNT(*) AS row_count FROM vehicle_model;

-- Step 10: Show table structure
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
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'vehicle_model'
ORDER BY ordinal_position;

-- Step 11: Show constraints
SELECT
    'vehicle_model' AS table_name,
    conname AS constraint_name,
    contype AS constraint_type,
    pg_get_constraintdef(oid) AS definition
FROM pg_constraint
WHERE conrelid = 'vehicle_model'::regclass
ORDER BY contype, conname;

-- Permissions
ALTER TABLE vehicle_model OWNER TO alex;
GRANT ALL ON TABLE vehicle_model TO alex;

