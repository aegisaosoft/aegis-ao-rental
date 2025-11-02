-- Complete SQL script for vehicle location setup
-- This script:
-- 1. Creates company_location table (if not exists)
-- 2. Links vehicles to company_location via location_id
-- 3. Adds current_location_id to vehicles table linked to locations table

-- ============================================================================
-- Step 1: Create company_location table (if not exists)
-- ============================================================================
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' 
        AND table_name = 'company_location'
    ) THEN
        CREATE TABLE public.company_location (
            id uuid DEFAULT uuid_generate_v4() NOT NULL,
            company_id uuid NOT NULL,
            location_name varchar(255) NOT NULL,
            address text NULL,
            city varchar(100) NULL,
            state varchar(100) NULL,
            country varchar(100) DEFAULT 'USA' NULL,
            postal_code varchar(20) NULL,
            phone varchar(50) NULL,
            email varchar(255) NULL,
            latitude decimal(10,8) NULL,
            longitude decimal(11,8) NULL,
            is_active bool DEFAULT true NOT NULL,
            is_pickup_location bool DEFAULT true NOT NULL,
            is_return_location bool DEFAULT true NOT NULL,
            opening_hours text NULL,
            created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
            updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
            CONSTRAINT company_location_pkey PRIMARY KEY (id),
            CONSTRAINT fk_company_location_company 
                FOREIGN KEY (company_id)
                REFERENCES public.companies(id)
                ON DELETE CASCADE
        );

        CREATE INDEX idx_company_location_company_id ON public.company_location(company_id);
        CREATE INDEX idx_company_location_is_active ON public.company_location(is_active);
        CREATE INDEX idx_company_location_is_pickup ON public.company_location(is_pickup_location);
        CREATE INDEX idx_company_location_is_return ON public.company_location(is_return_location);

        COMMENT ON TABLE public.company_location IS 'Company-specific locations for vehicles';
        COMMENT ON COLUMN public.company_location.company_id IS 'References the company that owns this location';
        COMMENT ON COLUMN public.company_location.location_name IS 'Name of the location (e.g., "Miami Downtown Office")';
    END IF;
END $$;

-- ============================================================================
-- Step 2: Add location_id column to vehicles if not exists and link to company_location
-- ============================================================================
DO $$
BEGIN
    -- Add location_id column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'vehicles' 
        AND column_name = 'location_id'
    ) THEN
        ALTER TABLE public.vehicles
        ADD COLUMN location_id uuid NULL;
    END IF;

    -- Drop existing foreign key if it references locations table
    IF EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_schema = 'public' 
        AND constraint_name = 'fk_vehicles_location'
        AND table_name = 'vehicles'
    ) THEN
        ALTER TABLE public.vehicles
        DROP CONSTRAINT fk_vehicles_location;
    END IF;

    -- Add foreign key to company_location if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_schema = 'public' 
        AND constraint_name = 'fk_vehicles_company_location'
        AND table_name = 'vehicles'
    ) THEN
        ALTER TABLE public.vehicles
        ADD CONSTRAINT fk_vehicles_company_location
        FOREIGN KEY (location_id)
        REFERENCES public.company_location(id)
        ON DELETE SET NULL;
    END IF;

    -- Create index if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
        AND tablename = 'vehicles' 
        AND indexname = 'idx_vehicles_location_id'
    ) THEN
        CREATE INDEX idx_vehicles_location_id ON public.vehicles(location_id);
    END IF;

    COMMENT ON COLUMN public.vehicles.location_id IS 'Assigned location from company_location table';
END $$;

-- ============================================================================
-- Step 3: Add current_location_id column to vehicles and link to locations table
-- ============================================================================
DO $$
BEGIN
    -- Add current_location_id column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'vehicles' 
        AND column_name = 'current_location_id'
    ) THEN
        ALTER TABLE public.vehicles
        ADD COLUMN current_location_id uuid NULL;
    END IF;

    -- Add foreign key to locations table if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.table_constraints 
        WHERE constraint_schema = 'public' 
        AND constraint_name = 'fk_vehicles_current_location'
        AND table_name = 'vehicles'
    ) THEN
        ALTER TABLE public.vehicles
        ADD CONSTRAINT fk_vehicles_current_location
        FOREIGN KEY (current_location_id)
        REFERENCES public.locations(id)
        ON DELETE SET NULL;
    END IF;

    -- Create index if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
        AND tablename = 'vehicles' 
        AND indexname = 'idx_vehicles_current_location_id'
    ) THEN
        CREATE INDEX idx_vehicles_current_location_id ON public.vehicles(current_location_id);
    END IF;

    COMMENT ON COLUMN public.vehicles.current_location_id IS 'Current physical location of the vehicle (references locations table, can be null)';
END $$;

-- ============================================================================
-- Summary
-- ============================================================================
-- This script creates:
-- 1. company_location table (if needed)
-- 2. Links vehicles.location_id to company_location(id)
-- 3. Adds vehicles.current_location_id linked to locations(id)
-- 4. Creates all necessary indexes and constraints
-- 5. Both location fields are nullable

