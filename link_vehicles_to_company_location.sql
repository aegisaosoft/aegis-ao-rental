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

-- Link vehicles table to company_location instead of locations table

-- Step 1: Drop the existing foreign key constraint
ALTER TABLE public.vehicles
DROP CONSTRAINT IF EXISTS vehicles_location_id_fkey;

-- Step 2: Add new foreign key constraint pointing to company_location table
ALTER TABLE public.vehicles
ADD CONSTRAINT vehicles_location_id_fkey 
FOREIGN KEY (location_id) REFERENCES public.company_location(id) ON DELETE SET NULL;

-- Note: The index on location_id already exists and will work with the new foreign key
-- No need to recreate it as it references the column, not the constraint

COMMENT ON COLUMN public.vehicles.location_id IS 'Reference to the company_location table';

