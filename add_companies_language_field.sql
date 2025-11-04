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

-- Add language and country fields to companies table
-- Check if columns already exist before adding

DO $$
BEGIN
    -- Add country column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'companies' 
        AND column_name = 'country'
    ) THEN
        ALTER TABLE public.companies ADD COLUMN country VARCHAR(100) NULL;
        COMMENT ON COLUMN public.companies.country IS 'Company country location';
    END IF;

    -- Add language column if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'companies' 
        AND column_name = 'language'
    ) THEN
        ALTER TABLE public.companies ADD COLUMN language VARCHAR(10) NULL DEFAULT 'en';
        COMMENT ON COLUMN public.companies.language IS 'Company preferred language (ISO 639-1 code like en, es, pt, etc.)';
    END IF;
END $$;

-- Create indexes for the new columns
CREATE INDEX IF NOT EXISTS idx_companies_country ON public.companies USING btree (country);
CREATE INDEX IF NOT EXISTS idx_companies_language ON public.companies USING btree (language);

