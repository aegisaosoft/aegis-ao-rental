-- Remove drivers license fields from customers table
-- This script removes drivers_license_number, drivers_license_state, and drivers_license_expiry columns

-- Step 1: Drop any views that reference these columns
DO $$
DECLARE
    view_record RECORD;
    obj_record RECORD;
BEGIN
    -- Explicitly check for customer-related views
    FOR view_record IN
        SELECT DISTINCT schemaname, viewname
        FROM pg_views
        WHERE schemaname = 'public'
        AND (
            definition LIKE '%customers%drivers_license%'
            OR definition LIKE '%customers.drivers_license%'
            OR definition LIKE '%c.drivers_license%'
        )
    LOOP
        BEGIN
            EXECUTE format('DROP VIEW IF EXISTS %I.%I CASCADE', view_record.schemaname, view_record.viewname);
            RAISE NOTICE 'Dropped view: %', view_record.viewname;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Error dropping view %: %', view_record.viewname, SQLERRM;
        END;
    END LOOP;
    
    -- Find all objects that depend on customers.drivers_license columns using pg_depend
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
        AND t.relname = 'customers'
        AND a.attname IN ('drivers_license_number', 'drivers_license_state', 'drivers_license_expiry')
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

-- Step 2: Drop any indexes on these columns
DROP INDEX IF EXISTS idx_customers_drivers_license_number;
DROP INDEX IF EXISTS idx_customers_drivers_license_state;
DROP INDEX IF EXISTS idx_customers_drivers_license_expiry;

-- Step 3: Remove the columns
ALTER TABLE customers DROP COLUMN IF EXISTS drivers_license_number;
ALTER TABLE customers DROP COLUMN IF EXISTS drivers_license_state;
ALTER TABLE customers DROP COLUMN IF EXISTS drivers_license_expiry;

-- Step 4: Verify the columns are removed
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = 'customers' 
AND column_name IN ('drivers_license_number', 'drivers_license_state', 'drivers_license_expiry');

-- Step 5: List remaining customer columns for verification
SELECT 
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'customers'
ORDER BY ordinal_position;
