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
-- Add stripe_settings_id column to companies table
-- ============================================================================

-- Add stripe_settings_id column to companies table (if it doesn't exist)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'companies' 
        AND column_name = 'stripe_settings_id'
    ) THEN
        ALTER TABLE public.companies 
        ADD COLUMN stripe_settings_id uuid NULL;
        
        -- Add foreign key constraint
        ALTER TABLE public.companies
        ADD CONSTRAINT fk_companies_stripe_settings 
        FOREIGN KEY (stripe_settings_id) 
        REFERENCES public.stripe_settings(id) 
        ON DELETE SET NULL;
        
        -- Create index for better performance
        CREATE INDEX IF NOT EXISTS idx_companies_stripe_settings_id 
        ON public.companies USING btree (stripe_settings_id);
        
        -- Add column comment
        COMMENT ON COLUMN public.companies.stripe_settings_id IS 'Reference to stripe_settings table';
    END IF;
END $$;

-- Update companies table to set stripe_settings_id to the test record
UPDATE public.companies c
SET stripe_settings_id = ss.id
FROM public.stripe_settings ss
WHERE ss.name = 'test'
  AND c.stripe_settings_id IS NULL;

