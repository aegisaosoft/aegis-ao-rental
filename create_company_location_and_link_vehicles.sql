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
 * Author: Alexander Orlov
 *
 */

-- ============================================================================
-- Create company_location table with the same fields as locations table
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.company_location (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    company_id uuid NOT NULL,
    location_name varchar(255) NOT NULL,
    address text NULL,
    city varchar(100) NULL,
    state varchar(100) NULL,
    country varchar(100) DEFAULT 'USA'::character varying NULL,
    postal_code varchar(20) NULL,
    phone varchar(50) NULL,
    email varchar(255) NULL,
    latitude numeric(10, 8) NULL, -- GPS latitude coordinate for mapping
    longitude numeric(11, 8) NULL, -- GPS longitude coordinate for mapping
    is_active bool DEFAULT true NULL,
    is_pickup_location bool DEFAULT true NULL, -- Whether customers can pick up vehicles from this location
    is_return_location bool DEFAULT true NULL, -- Whether customers can return vehicles to this location
    opening_hours text NULL,
    created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    CONSTRAINT company_location_pkey PRIMARY KEY (id),
    CONSTRAINT fk_company_location_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_company_location_company ON public.company_location USING btree (company_id);
CREATE INDEX IF NOT EXISTS idx_company_location_is_active ON public.company_location USING btree (is_active);
CREATE INDEX IF NOT EXISTS idx_company_location_pickup ON public.company_location USING btree (is_pickup_location);
CREATE INDEX IF NOT EXISTS idx_company_location_return ON public.company_location USING btree (is_return_location);
CREATE INDEX IF NOT EXISTS idx_company_location_state ON public.company_location USING btree (state);

-- Table comments
COMMENT ON TABLE public.company_location IS 'Company locations with the same structure as locations table';

-- Column comments
COMMENT ON COLUMN public.company_location.latitude IS 'GPS latitude coordinate for mapping';
COMMENT ON COLUMN public.company_location.longitude IS 'GPS longitude coordinate for mapping';
COMMENT ON COLUMN public.company_location.is_pickup_location IS 'Whether customers can pick up vehicles from this location';
COMMENT ON COLUMN public.company_location.is_return_location IS 'Whether customers can return vehicles to this location';

-- Create trigger to update updated_at timestamp
CREATE TRIGGER update_company_location_updated_at
BEFORE UPDATE ON public.company_location
FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

-- Permissions
ALTER TABLE public.company_location OWNER TO alex;
GRANT ALL ON TABLE public.company_location TO alex;

-- ============================================================================
-- Link vehicles table to company_location instead of locations table
-- ============================================================================

-- Step 1: Drop the existing foreign key constraint pointing to locations
ALTER TABLE public.vehicles
DROP CONSTRAINT IF EXISTS fk_vehicles_location;

-- Step 2: Add new foreign key constraint pointing to company_location table
ALTER TABLE public.vehicles
ADD CONSTRAINT vehicles_location_id_fkey 
FOREIGN KEY (location_id) REFERENCES public.company_location(id) ON DELETE SET NULL;

-- Update column comment to reflect the new relationship
COMMENT ON COLUMN public.vehicles.location_id IS 'Reference to the company_location table';

