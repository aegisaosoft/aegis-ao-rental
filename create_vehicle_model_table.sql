--
-- Create vehicle_model junction table
-- This script creates the vehicle_model junction table to link vehicles to models
-- It is idempotent - can be run multiple times safely
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
-- Author: Alexander Orlov
--

-- Step 0: Drop existing objects if they exist (for idempotency)
DROP TABLE IF EXISTS vehicle_model CASCADE;

-- Step 1: Create the vehicle_model junction table
CREATE TABLE vehicle_model (
    vehicle_id UUID NOT NULL,
    model_id UUID NOT NULL,
    daily_rate NUMERIC(10, 2),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT pk_vehicle_model PRIMARY KEY (vehicle_id, model_id),
    CONSTRAINT fk_vehicle_model_vehicle FOREIGN KEY (vehicle_id) REFERENCES vehicles(id) ON DELETE CASCADE,
    CONSTRAINT fk_vehicle_model_model FOREIGN KEY (model_id) REFERENCES models(id) ON DELETE CASCADE
);

-- Step 2: Create indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_vehicle_model_vehicle_id ON vehicle_model(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_vehicle_model_model_id ON vehicle_model(model_id);

-- Step 3: Add comments
COMMENT ON TABLE vehicle_model IS 'Junction table linking vehicles to their models';
COMMENT ON COLUMN vehicle_model.vehicle_id IS 'Reference to the vehicle';
COMMENT ON COLUMN vehicle_model.model_id IS 'Reference to the model';
COMMENT ON COLUMN vehicle_model.daily_rate IS 'Daily rate for this vehicle-model combination';
COMMENT ON COLUMN vehicle_model.created_at IS 'When the link was created';

-- Step 4: Verify the table structure
SELECT 
    column_name,
    data_type,
    is_nullable,
    column_default
FROM information_schema.columns
WHERE table_name = 'vehicle_model'
ORDER BY ordinal_position;

-- Step 5: Show indexes
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'vehicle_model'
AND schemaname = 'public';

-- Permissions
ALTER TABLE vehicle_model OWNER TO alex;
GRANT ALL ON TABLE vehicle_model TO alex;

