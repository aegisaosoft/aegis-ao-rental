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

-- Multi-Tenant Support Migration
-- Adds subdomain UNIQUE constraint and missing indexes for domain-based company resolution
-- Based on current companies table structure with 'id' as primary key column

-- Verify table exists
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.tables 
        WHERE table_schema = 'public' AND table_name = 'companies'
    ) THEN
        RAISE EXCEPTION 'Table "companies" does not exist in public schema';
    END IF;
    
    -- Verify primary key column name is 'id'
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_schema = 'public' 
        AND table_name = 'companies' 
        AND column_name = 'id'
    ) THEN
        RAISE EXCEPTION 'Column "id" does not exist in companies table. Please verify table structure.';
    END IF;
    
    RAISE NOTICE 'Table companies verified - using id as primary key column';
END $$;

-- 1. Make subdomain unique (add UNIQUE constraint if not already exists)
-- NOTE: This preserves existing subdomains and only resolves duplicates
-- NULL subdomains are left as NULL for manual configuration
DO $$
DECLARE
    duplicate_count INTEGER;
    subdomain_attnum SMALLINT;
BEGIN
    -- Get the attribute number for subdomain column
    SELECT attnum INTO subdomain_attnum
    FROM pg_attribute 
    WHERE attrelid = 'companies'::regclass 
    AND attname = 'subdomain';
    
    -- Check if unique constraint already exists
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint 
        WHERE conrelid = 'companies'::regclass 
        AND contype = 'u'
        AND (
            conname = 'companies_subdomain_key' 
            OR conname = 'uq_companies_subdomain'
            OR (conkey IS NOT NULL AND subdomain_attnum = ANY(conkey))
        )
    ) THEN
        -- Check for duplicate subdomains (excluding NULL values)
        SELECT COUNT(*) INTO duplicate_count
        FROM (
            SELECT subdomain, COUNT(*) as cnt
            FROM companies
            WHERE subdomain IS NOT NULL AND subdomain != ''
            GROUP BY subdomain
            HAVING COUNT(*) > 1
        ) duplicates;
        
        IF duplicate_count > 0 THEN
            RAISE NOTICE 'Found % duplicate subdomain(s). Resolving duplicates...', duplicate_count;
            
            -- Resolve duplicate subdomains by adding a number suffix to duplicates
            -- Keeps the oldest company's subdomain unchanged, modifies newer ones
            WITH duplicates AS (
                SELECT 
                    id,  -- Primary key column
                    subdomain, 
                    ROW_NUMBER() OVER (PARTITION BY subdomain ORDER BY created_at) as rn
                FROM companies
                WHERE subdomain IS NOT NULL AND subdomain != ''
            )
            UPDATE companies c
            SET subdomain = c.subdomain || '-' || d.rn
            FROM duplicates d
            WHERE c.id = d.id AND d.rn > 1;
            
            RAISE NOTICE 'Duplicate subdomains resolved by adding number suffixes';
        ELSE
            RAISE NOTICE 'No duplicate subdomains found';
        END IF;
        
        -- Now add unique constraint (allows NULL values in PostgreSQL)
        -- This means you can have multiple NULL subdomains, but non-NULL must be unique
        ALTER TABLE companies ADD CONSTRAINT companies_subdomain_key UNIQUE (subdomain);
        
        RAISE NOTICE 'Added UNIQUE constraint on subdomain column';
        RAISE NOTICE 'Existing subdomains preserved. NULL subdomains left for manual configuration.';
    ELSE
        RAISE NOTICE 'UNIQUE constraint on subdomain already exists';
    END IF;
END $$;

-- 2. Create missing indexes for performance (if not already exist)
-- Note: Following your existing index naming pattern: idx_companies_<column_name>
-- Note: idx_companies_subdomain, idx_companies_country, idx_companies_language already exist

-- Check and create index on is_active (for filtering active companies)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
        AND tablename = 'companies' 
        AND indexname = 'idx_companies_is_active'
    ) THEN
        CREATE INDEX idx_companies_is_active ON companies(is_active);
        RAISE NOTICE 'Created index idx_companies_is_active';
    ELSE
        RAISE NOTICE 'Index idx_companies_is_active already exists';
    END IF;
END $$;

-- Check and create index on created_at (for sorting by date)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_indexes 
        WHERE schemaname = 'public' 
        AND tablename = 'companies' 
        AND indexname = 'idx_companies_created_at'
    ) THEN
        CREATE INDEX idx_companies_created_at ON companies(created_at DESC);
        RAISE NOTICE 'Created index idx_companies_created_at';
    ELSE
        RAISE NOTICE 'Index idx_companies_created_at already exists';
    END IF;
END $$;

-- 3. Verify trigger function exists (should already exist, but ensure it's correct)
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- 4. Verify trigger exists (should already exist based on your schema)
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_trigger 
        WHERE tgname = 'update_companies_updated_at'
    ) THEN
        CREATE TRIGGER update_companies_updated_at
            BEFORE UPDATE ON companies
            FOR EACH ROW
            EXECUTE FUNCTION update_updated_at_column();
        RAISE NOTICE 'Created trigger update_companies_updated_at';
    ELSE
        RAISE NOTICE 'Trigger update_companies_updated_at already exists';
    END IF;
END $$;

-- 5. Update comments for better documentation (if needed)
COMMENT ON COLUMN companies.subdomain IS 'Unique subdomain for the company (e.g., company1 for company1.aegis-rental.com). Used for domain-based multi-tenancy.';
COMMENT ON COLUMN companies.primary_color IS 'Hex color code for primary brand color (#RRGGBB format)';
COMMENT ON COLUMN companies.secondary_color IS 'Hex color code for secondary brand color (#RRGGBB format)';
COMMENT ON COLUMN companies.custom_css IS 'Custom CSS overrides for company-specific styling';

-- 6. Verify the migration
SELECT 
    'Migration completed successfully' as status,
    COUNT(*) as total_companies,
    COUNT(subdomain) as companies_with_subdomain,
    COUNT(*) FILTER (WHERE is_active = true) as active_companies,
    COUNT(*) FILTER (WHERE subdomain IS NULL) as companies_without_subdomain
FROM companies;

-- Show companies and their subdomains (using id as primary key)
SELECT 
    id,  -- Primary key column
    company_name,
    subdomain,
    CASE 
        WHEN subdomain IS NOT NULL AND subdomain != '' THEN subdomain || '.aegis-rental.com'
        ELSE 'No subdomain set - please add one'
    END as full_domain,
    is_active,
    created_at
FROM companies
ORDER BY created_at DESC
LIMIT 20;

-- Show any companies missing subdomains (these can be set manually via admin panel or API)
SELECT 
    id,  -- Primary key column
    company_name,
    email,
    subdomain,
    CASE 
        WHEN subdomain IS NULL THEN 'NULL - can be set manually'
        WHEN subdomain = '' THEN 'Empty string - should be set manually'
        ELSE 'Has subdomain'
    END as subdomain_status,
    'Set subdomain manually via admin panel or API' as note
FROM companies
WHERE subdomain IS NULL OR subdomain = ''
ORDER BY created_at DESC;

-- Summary: Show constraint status
SELECT 
    'Constraint Check' as check_type,
    CASE 
        WHEN EXISTS (
            SELECT 1 FROM pg_constraint 
            WHERE conrelid = 'companies'::regclass 
            AND contype = 'u'
            AND conkey::text[] && ARRAY[(
                SELECT attnum::text 
                FROM pg_attribute 
                WHERE attrelid = 'companies'::regclass 
                AND attname = 'subdomain'
            )]
        ) THEN 'UNIQUE constraint on subdomain EXISTS'
        ELSE 'UNIQUE constraint on subdomain MISSING'
    END as status;

