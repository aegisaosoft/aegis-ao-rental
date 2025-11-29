-- =====================================================
-- Add 'designer' role to aegis_users table constraint
-- =====================================================
-- This script safely updates the check constraint to include 'designer' role
-- Date: 2025
-- =====================================================

-- Drop the constraint if it exists (safe operation)
DO $$
BEGIN
    -- Drop constraint if it exists
    IF EXISTS (
        SELECT 1 
        FROM pg_constraint 
        WHERE conname = 'ck_aegis_users_role_valid' 
        AND conrelid = 'aegis_users'::regclass
    ) THEN
        ALTER TABLE aegis_users DROP CONSTRAINT ck_aegis_users_role_valid;
        RAISE NOTICE 'Dropped existing constraint ck_aegis_users_role_valid';
    ELSE
        RAISE NOTICE 'Constraint ck_aegis_users_role_valid does not exist, skipping drop';
    END IF;
END $$;

-- Add the updated constraint with 'designer' role included
ALTER TABLE aegis_users 
ADD CONSTRAINT ck_aegis_users_role_valid 
CHECK (role IN ('agent','admin','mainadmin','designer'));

-- Verify the constraint was created
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 
        FROM pg_constraint 
        WHERE conname = 'ck_aegis_users_role_valid' 
        AND conrelid = 'aegis_users'::regclass
    ) THEN
        RAISE NOTICE 'Successfully created constraint ck_aegis_users_role_valid with designer role';
    ELSE
        RAISE WARNING 'Failed to create constraint ck_aegis_users_role_valid';
    END IF;
END $$;

