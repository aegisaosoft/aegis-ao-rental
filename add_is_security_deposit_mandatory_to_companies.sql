--
-- MIGRATION SCRIPT: Add is_security_deposit_mandatory column to companies table
-- This script adds the is_security_deposit_mandatory boolean column to the companies table
-- with a default value of true.
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- ============================================
-- STEP 1: Add is_security_deposit_mandatory column
-- ============================================
ALTER TABLE companies 
ADD COLUMN IF NOT EXISTS is_security_deposit_mandatory BOOLEAN NOT NULL DEFAULT true;

-- ============================================
-- STEP 2: Update existing records to have default value
-- ============================================
UPDATE companies 
SET is_security_deposit_mandatory = true 
WHERE is_security_deposit_mandatory IS NULL;

-- ============================================
-- STEP 3: Verify the column was added
-- ============================================
SELECT 
    'Column added successfully' AS status,
    column_name,
    data_type,
    column_default,
    is_nullable
FROM information_schema.columns
WHERE table_name = 'companies' 
  AND column_name = 'is_security_deposit_mandatory';

-- ============================================
-- STEP 4: Show sample data
-- ============================================
SELECT 
    id,
    company_name,
    security_deposit,
    is_security_deposit_mandatory
FROM companies
LIMIT 5;

