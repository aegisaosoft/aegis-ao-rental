/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov Aegis AO Soft
 *
 */

-- Remove fuel_type and category_id columns from vehicles table

-- Step 1: Drop foreign key constraint for category_id if it exists
DO $$
DECLARE
    constraint_name TEXT;
BEGIN
    SELECT conname INTO constraint_name
    FROM pg_constraint
    WHERE conrelid = 'vehicles'::regclass
    AND confrelid = 'vehicle_categories'::regclass
    AND contype = 'f'
    LIMIT 1;

    IF constraint_name IS NOT NULL THEN
        EXECUTE format('ALTER TABLE vehicles DROP CONSTRAINT IF EXISTS %I CASCADE', constraint_name);
    END IF;
END $$;

-- Step 2: Drop index on category_id if it exists
DROP INDEX IF EXISTS idx_vehicles_category_id;

-- Step 3: Drop views that depend on these columns
DROP VIEW IF EXISTS vehicle_details CASCADE;
DROP VIEW IF EXISTS company_stats CASCADE;

-- Step 4: Drop the columns
ALTER TABLE vehicles DROP COLUMN IF EXISTS fuel_type;
ALTER TABLE vehicles DROP COLUMN IF EXISTS category_id;

-- Step 5: Recreate views (if needed) without the dropped columns
-- Note: Adjust these views based on your actual view definitions

COMMENT ON TABLE vehicles IS 'Vehicles table with fuel_type and category_id removed';

