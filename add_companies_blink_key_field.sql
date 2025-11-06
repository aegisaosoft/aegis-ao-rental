--
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
-- 
-- THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
-- Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
-- SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
-- WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
-- Alexander Orlov.
-- 
-- Author: Alexander Orlov
--

-- Add blink_key field to companies table
-- This field stores the BlinkID license key for each company
-- Check if column already exists before adding

DO $$
BEGIN
    -- Add blink_key column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'companies' 
        AND column_name = 'blink_key'
    ) THEN
        ALTER TABLE public.companies ADD COLUMN blink_key TEXT NULL;
        COMMENT ON COLUMN public.companies.blink_key IS 'BlinkID license key for the company (domain-specific license)';
    END IF;
END $$;

