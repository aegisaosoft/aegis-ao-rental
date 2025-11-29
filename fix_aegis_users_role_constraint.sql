-- Fix aegis_users role constraint to include 'designer' role
-- This script handles both constraint names: 'valid_aegis_user_role' and 'ck_aegis_users_role_valid'

DO $$
BEGIN
    -- Drop constraint if it exists with name 'valid_aegis_user_role'
    IF EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'valid_aegis_user_role' 
        AND conrelid = 'aegis_users'::regclass
    ) THEN
        ALTER TABLE aegis_users DROP CONSTRAINT valid_aegis_user_role;
        RAISE NOTICE 'Dropped existing constraint valid_aegis_user_role';
    END IF;

    -- Drop constraint if it exists with name 'ck_aegis_users_role_valid'
    IF EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conname = 'ck_aegis_users_role_valid' 
        AND conrelid = 'aegis_users'::regclass
    ) THEN
        ALTER TABLE aegis_users DROP CONSTRAINT ck_aegis_users_role_valid;
        RAISE NOTICE 'Dropped existing constraint ck_aegis_users_role_valid';
    END IF;

    -- Add the new constraint with 'designer' role included
    -- Using the name that matches the error: valid_aegis_user_role
    ALTER TABLE aegis_users
    ADD CONSTRAINT valid_aegis_user_role
    CHECK (role IN ('agent','admin','mainadmin','designer'));
    
    RAISE NOTICE 'Successfully created constraint valid_aegis_user_role with designer role';
EXCEPTION
    WHEN OTHERS THEN
        RAISE EXCEPTION 'Error updating constraint: %', SQLERRM;
END $$;

