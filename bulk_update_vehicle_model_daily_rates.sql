--
-- Bulk update daily rates in vehicle_model and vehicles tables
-- This script updates daily rates based on model criteria (category, make, model, year)
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
--

-- ============================================
-- Update by Category ID
-- ============================================
-- Uncomment and set the parameters as needed:
/*
DO $$
DECLARE
    v_category_id UUID := 'your-category-id-here';
    v_daily_rate NUMERIC(10,2) := 70.00;
    v_updated_count INTEGER := 0;
BEGIN
    -- Update vehicle_model records
    UPDATE vehicle_model vm
    SET daily_rate = v_daily_rate
    WHERE vm.model_id IN (
        SELECT m.id 
        FROM models m 
        WHERE m.category_id = v_category_id
    );
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicle_model records', v_updated_count;
    
    -- Update vehicles records
    UPDATE vehicles v
    SET daily_rate = v_daily_rate,
        updated_at = CURRENT_TIMESTAMP
    WHERE EXISTS (
        SELECT 1 
        FROM models m 
        WHERE m.category_id = v_category_id
        AND UPPER(TRIM(v.make)) = UPPER(TRIM(m.make))
        AND UPPER(TRIM(v.model)) = UPPER(TRIM(m.model_name))
        AND v.year = m.year
    );
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicles records', v_updated_count;
END $$;
*/

-- ============================================
-- Update by Make
-- ============================================
/*
DO $$
DECLARE
    v_make VARCHAR(100) := 'TOYOTA';
    v_daily_rate NUMERIC(10,2) := 70.00;
    v_updated_count INTEGER := 0;
BEGIN
    -- Update vehicle_model records
    UPDATE vehicle_model vm
    SET daily_rate = v_daily_rate
    WHERE vm.model_id IN (
        SELECT m.id 
        FROM models m 
        WHERE UPPER(TRIM(m.make)) = UPPER(TRIM(v_make))
    );
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicle_model records', v_updated_count;
    
    -- Update vehicles records
    UPDATE vehicles v
    SET daily_rate = v_daily_rate,
        updated_at = CURRENT_TIMESTAMP
    WHERE UPPER(TRIM(v.make)) = UPPER(TRIM(v_make));
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicles records', v_updated_count;
END $$;
*/

-- ============================================
-- Update by Make and Model
-- ============================================
/*
DO $$
DECLARE
    v_make VARCHAR(100) := 'TOYOTA';
    v_model_name VARCHAR(100) := 'CAMRY';
    v_daily_rate NUMERIC(10,2) := 70.00;
    v_updated_count INTEGER := 0;
BEGIN
    -- Update vehicle_model records
    UPDATE vehicle_model vm
    SET daily_rate = v_daily_rate
    WHERE vm.model_id IN (
        SELECT m.id 
        FROM models m 
        WHERE UPPER(TRIM(m.make)) = UPPER(TRIM(v_make))
        AND UPPER(TRIM(m.model_name)) = UPPER(TRIM(v_model_name))
    );
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicle_model records', v_updated_count;
    
    -- Update vehicles records
    UPDATE vehicles v
    SET daily_rate = v_daily_rate,
        updated_at = CURRENT_TIMESTAMP
    WHERE UPPER(TRIM(v.make)) = UPPER(TRIM(v_make))
    AND UPPER(TRIM(v.model)) = UPPER(TRIM(v_model_name));
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicles records', v_updated_count;
END $$;
*/

-- ============================================
-- Update by Make, Model, and Year
-- ============================================
/*
DO $$
DECLARE
    v_make VARCHAR(100) := 'TOYOTA';
    v_model_name VARCHAR(100) := 'CAMRY';
    v_year INTEGER := 2023;
    v_daily_rate NUMERIC(10,2) := 70.00;
    v_updated_count INTEGER := 0;
BEGIN
    -- Update vehicle_model records
    UPDATE vehicle_model vm
    SET daily_rate = v_daily_rate
    WHERE vm.model_id IN (
        SELECT m.id 
        FROM models m 
        WHERE UPPER(TRIM(m.make)) = UPPER(TRIM(v_make))
        AND UPPER(TRIM(m.model_name)) = UPPER(TRIM(v_model_name))
        AND m.year = v_year
    );
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicle_model records', v_updated_count;
    
    -- Update vehicles records
    UPDATE vehicles v
    SET daily_rate = v_daily_rate,
        updated_at = CURRENT_TIMESTAMP
    WHERE UPPER(TRIM(v.make)) = UPPER(TRIM(v_make))
    AND UPPER(TRIM(v.model)) = UPPER(TRIM(v_model_name))
    AND v.year = v_year;
    
    GET DIAGNOSTICS v_updated_count = ROW_COUNT;
    RAISE NOTICE 'Updated % vehicles records', v_updated_count;
END $$;
*/

-- ============================================
-- Preview changes before updating
-- ============================================

-- Preview by Category ID
-- SELECT 
--     vm.vehicle_id,
--     vm.model_id,
--     vm.daily_rate AS current_rate,
--     70.00 AS new_rate,
--     v.make,
--     v.model,
--     v.year
-- FROM vehicle_model vm
-- INNER JOIN vehicles v ON vm.vehicle_id = v.id
-- INNER JOIN models m ON vm.model_id = m.id
-- WHERE m.category_id = 'your-category-id-here';

-- Preview by Make
-- SELECT 
--     vm.vehicle_id,
--     vm.model_id,
--     vm.daily_rate AS current_rate,
--     70.00 AS new_rate,
--     v.make,
--     v.model,
--     v.year
-- FROM vehicle_model vm
-- INNER JOIN vehicles v ON vm.vehicle_id = v.id
-- INNER JOIN models m ON vm.model_id = m.id
-- WHERE UPPER(TRIM(v.make)) = 'TOYOTA';

-- Preview by Make and Model
-- SELECT 
--     vm.vehicle_id,
--     vm.model_id,
--     vm.daily_rate AS current_rate,
--     70.00 AS new_rate,
--     v.make,
--     v.model,
--     v.year
-- FROM vehicle_model vm
-- INNER JOIN vehicles v ON vm.vehicle_id = v.id
-- INNER JOIN models m ON vm.model_id = m.id
-- WHERE UPPER(TRIM(v.make)) = 'TOYOTA'
-- AND UPPER(TRIM(v.model)) = 'CAMRY';

-- Preview by Make, Model, and Year
-- SELECT 
--     vm.vehicle_id,
--     vm.model_id,
--     vm.daily_rate AS current_rate,
--     70.00 AS new_rate,
--     v.make,
--     v.model,
--     v.year
-- FROM vehicle_model vm
-- INNER JOIN vehicles v ON vm.vehicle_id = v.id
-- INNER JOIN models m ON vm.model_id = m.id
-- WHERE UPPER(TRIM(v.make)) = 'TOYOTA'
-- AND UPPER(TRIM(v.model)) = 'CAMRY'
-- AND v.year = 2023;

