-- Add vehicle status enum constraint and remove is_active field
-- This script updates the vehicles table to use the new VehicleStatus enum values
-- and replaces is_active with status field

-- Step 1: Drop constraint if it already exists
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM pg_constraint 
        WHERE conname = 'chk_vehicle_status' 
        AND conrelid = 'vehicles'::regclass
    ) THEN
        ALTER TABLE vehicles DROP CONSTRAINT chk_vehicle_status;
        RAISE NOTICE 'Dropped existing chk_vehicle_status constraint';
    END IF;
END $$;

-- Step 2: Update existing status values to match new enum
UPDATE vehicles 
SET status = CASE 
    WHEN LOWER(status) = 'available' THEN 'Available'
    WHEN LOWER(status) = 'rented' THEN 'Rented'
    WHEN LOWER(status) = 'maintenance' THEN 'Maintenance'
    WHEN LOWER(status) = 'outofservice' OR LOWER(status) = 'out_of_service' THEN 'OutOfService'
    WHEN LOWER(status) = 'cleaning' THEN 'Cleaning'
    ELSE 'Available'  -- Default for any unknown values
END;

-- Step 3: Update status based on is_active field before removing it
-- If is_active is false, set status to OutOfService, otherwise keep current status or set to Available
UPDATE vehicles 
SET status = CASE 
    WHEN is_active = false THEN 'OutOfService'
    WHEN status IS NULL OR status = '' THEN 'Available'
    ELSE status
END
WHERE is_active = false OR status IS NULL OR status = '';

-- Step 4: Drop ALL views that might reference vehicles.is_active
-- Explicitly drop vehicle_details view first (most common dependency)
DROP VIEW IF EXISTS vehicle_details CASCADE;

-- Find and drop any other views that reference vehicles.is_active
DO $$
DECLARE
    view_record RECORD;
    obj_record RECORD;
BEGIN
    -- Find all views in pg_views that mention vehicles and is_active
    FOR view_record IN
        SELECT DISTINCT schemaname, viewname
        FROM pg_views
        WHERE schemaname = 'public'
        AND (
            definition LIKE '%vehicles%is_active%'
            OR definition LIKE '%vehicles.is_active%'
            OR definition LIKE '%v.is_active%'
            OR viewname = 'vehicle_details'
        )
    LOOP
        BEGIN
            EXECUTE format('DROP VIEW IF EXISTS %I.%I CASCADE', view_record.schemaname, view_record.viewname);
            RAISE NOTICE 'Dropped view: %', view_record.viewname;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Error dropping view %: %', view_record.viewname, SQLERRM;
        END;
    END LOOP;
    
    -- Find all objects that depend on vehicles.is_active using pg_depend
    FOR obj_record IN
        SELECT DISTINCT
            n.nspname as schema_name,
            c.relname as object_name,
            c.relkind as object_kind
        FROM pg_depend d
        JOIN pg_class c ON c.oid = d.objid
        JOIN pg_namespace n ON n.oid = c.relnamespace
        JOIN pg_attribute a ON a.attrelid = d.refobjid AND a.attnum = d.refobjsubid
        JOIN pg_class t ON t.oid = a.attrelid
        WHERE n.nspname = 'public'
        AND t.relname = 'vehicles'
        AND a.attname = 'is_active'
        AND c.relkind IN ('v', 'm')  -- views and materialized views
    LOOP
        BEGIN
            IF obj_record.object_kind = 'v' THEN
                EXECUTE format('DROP VIEW IF EXISTS %I.%I CASCADE', obj_record.schema_name, obj_record.object_name);
                RAISE NOTICE 'Dropped dependent view: %', obj_record.object_name;
            ELSIF obj_record.object_kind = 'm' THEN
                EXECUTE format('DROP MATERIALIZED VIEW IF EXISTS %I.%I CASCADE', obj_record.schema_name, obj_record.object_name);
                RAISE NOTICE 'Dropped dependent materialized view: %', obj_record.object_name;
            END IF;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Error dropping %: %', obj_record.object_name, SQLERRM;
        END;
    END LOOP;
END $$;

-- Step 5: Drop any indexes on is_active column
DROP INDEX IF EXISTS idx_vehicles_is_active;

-- Step 6: Remove is_active column (all dependencies should be dropped now)
ALTER TABLE vehicles DROP COLUMN IF EXISTS is_active;

-- Step 7: Add CHECK constraint for valid status values
ALTER TABLE vehicles 
ADD CONSTRAINT chk_vehicle_status 
CHECK (status IN ('Available', 'Rented', 'Maintenance', 'OutOfService', 'Cleaning'));

-- Step 8: Update vehicle_details view (if it exists, remove is_active reference)
DROP VIEW IF EXISTS vehicle_details CASCADE;
CREATE OR REPLACE VIEW vehicle_details AS
SELECT 
    v.id,
    v.company_id,
    v.category_id,
    v.make,
    v.model,
    v.year,
    v.color,
    v.license_plate,
    v.vin,
    v.mileage,
    v.fuel_type,
    v.transmission,
    v.seats,
    v.daily_rate,
    v.status,
    v.state,
    v.location,
    v.location_id,
    v.image_url,
    v.features,
    v.created_at,
    v.updated_at,
    c.company_name,
    cat.category_name,
    l.location_name,
    l.address,
    l.city,
    l.state as location_state,
    l.postal_code,
    l.country
FROM vehicles v
LEFT JOIN companies c ON v.company_id = c.id
LEFT JOIN vehicle_categories cat ON v.category_id = cat.id
LEFT JOIN locations l ON v.location_id = l.id;

-- Step 9: Check for any other views that might reference vehicles table
-- List all views to verify
DO $$
DECLARE
    view_record RECORD;
BEGIN
    RAISE NOTICE 'Checking for views that reference vehicles table...';
    FOR view_record IN
        SELECT schemaname, viewname
        FROM pg_views
        WHERE schemaname = 'public'
        AND definition LIKE '%vehicles%'
    LOOP
        RAISE NOTICE 'Found view: %.%', view_record.schemaname, view_record.viewname;
    END LOOP;
END $$;

-- Step 10: Verify the changes
SELECT 
    status,
    COUNT(*) as count
FROM vehicles 
GROUP BY status
ORDER BY status;

-- Show the constraint
SELECT 
    conname as constraint_name,
    pg_get_constraintdef(oid) as constraint_definition
FROM pg_constraint 
WHERE conrelid = 'vehicles'::regclass 
AND conname = 'chk_vehicle_status';

-- Verify is_active column is removed
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = 'vehicles' 
AND column_name = 'is_active';