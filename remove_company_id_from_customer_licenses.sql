-- Remove company_id column from customer_licenses table
-- This script removes the company_id column and all related constraints/indexes

-- Step 1: Drop any views that reference customer_licenses.company_id
DO $$
DECLARE
    view_record RECORD;
    obj_record RECORD;
BEGIN
    -- Find all views in pg_views that mention customer_licenses and company_id
    FOR view_record IN
        SELECT DISTINCT schemaname, viewname
        FROM pg_views
        WHERE schemaname = 'public'
        AND (
            definition LIKE '%customer_licenses%company_id%'
            OR definition LIKE '%customer_licenses.company_id%'
            OR definition LIKE '%cl.company_id%'
        )
    LOOP
        BEGIN
            EXECUTE format('DROP VIEW IF EXISTS %I.%I CASCADE', view_record.schemaname, view_record.viewname);
            RAISE NOTICE 'Dropped view: %', view_record.viewname;
        EXCEPTION WHEN OTHERS THEN
            RAISE NOTICE 'Error dropping view %: %', view_record.viewname, SQLERRM;
        END;
    END LOOP;
    
    -- Find all objects that depend on customer_licenses.company_id using pg_depend
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
        AND t.relname = 'customer_licenses'
        AND a.attname = 'company_id'
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

-- Step 2: Drop foreign key constraint on company_id if it exists
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM pg_constraint 
        WHERE conrelid = 'customer_licenses'::regclass 
        AND conname LIKE '%company%'
    ) THEN
        ALTER TABLE customer_licenses DROP CONSTRAINT IF EXISTS fk_company CASCADE;
        ALTER TABLE customer_licenses DROP CONSTRAINT IF EXISTS fk_customer_licenses_company CASCADE;
        ALTER TABLE customer_licenses DROP CONSTRAINT IF EXISTS customer_licenses_company_id_fkey CASCADE;
        RAISE NOTICE 'Dropped foreign key constraints on company_id';
    END IF;
END $$;

-- Step 3: Drop indexes on company_id
DROP INDEX IF EXISTS idx_customer_licenses_company_id;
DROP INDEX IF EXISTS idx_customer_licenses_company_id_license_number_state_issued;

-- Step 4: Drop unique constraint that includes company_id if it exists
DO $$
DECLARE
    constraint_record RECORD;
BEGIN
    -- Find and drop unique constraints that include company_id
    FOR constraint_record IN 
        SELECT conname 
        FROM pg_constraint 
        WHERE conrelid = 'customer_licenses'::regclass 
        AND contype = 'u'
        AND (pg_get_constraintdef(oid) LIKE '%company_id%')
    LOOP
        EXECUTE format('ALTER TABLE customer_licenses DROP CONSTRAINT IF EXISTS %I CASCADE', constraint_record.conname);
        RAISE NOTICE 'Dropped unique constraint: %', constraint_record.conname;
    END LOOP;
END $$;

-- Step 5: Drop RLS policies that reference company_id
DO $$
DECLARE
    policy_record RECORD;
BEGIN
    -- Find and drop all policies on customer_licenses that reference company_id
    FOR policy_record IN
        SELECT policyname
        FROM pg_policies
        WHERE schemaname = 'public'
        AND tablename = 'customer_licenses'
        AND (
            qual LIKE '%company_id%'
            OR with_check LIKE '%company_id%'
            OR policyname LIKE '%company%'
        )
    LOOP
        EXECUTE format('DROP POLICY IF EXISTS %I ON customer_licenses', policy_record.policyname);
        RAISE NOTICE 'Dropped RLS policy: %', policy_record.policyname;
    END LOOP;
    
    -- Also try to drop the specific policy mentioned in the error
    DROP POLICY IF EXISTS customer_licenses_isolation ON customer_licenses;
    RAISE NOTICE 'Attempted to drop customer_licenses_isolation policy';
END $$;

-- Step 6: Remove company_id column
ALTER TABLE customer_licenses DROP COLUMN IF EXISTS company_id;

-- Step 7: Verify the column is removed
SELECT 
    column_name,
    data_type
FROM information_schema.columns
WHERE table_name = 'customer_licenses' 
AND column_name = 'company_id';

-- Step 8: List remaining columns for verification
SELECT 
    column_name,
    data_type,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'customer_licenses'
ORDER BY ordinal_position;

-- Step 9: Show remaining indexes
SELECT 
    indexname,
    indexdef
FROM pg_indexes
WHERE tablename = 'customer_licenses'
AND schemaname = 'public';
