-- Add refund-related columns to payments table
-- This migration adds support for tracking refunds

-- Add updated_at column if it doesn't exist
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payments' 
        AND column_name = 'updated_at'
    ) THEN
        ALTER TABLE payments 
        ADD COLUMN updated_at TIMESTAMP;
        
        RAISE NOTICE 'Added updated_at column to payments table';
    ELSE
        RAISE NOTICE 'updated_at column already exists in payments table';
    END IF;
END $$;

-- Add refund_amount column if it doesn't exist
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payments' 
        AND column_name = 'refund_amount'
    ) THEN
        ALTER TABLE payments 
        ADD COLUMN refund_amount NUMERIC(10,2);
        
        RAISE NOTICE 'Added refund_amount column to payments table';
    ELSE
        RAISE NOTICE 'refund_amount column already exists in payments table';
    END IF;
END $$;

-- Add refund_date column if it doesn't exist
DO $$ 
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payments' 
        AND column_name = 'refund_date'
    ) THEN
        ALTER TABLE payments 
        ADD COLUMN refund_date TIMESTAMP;
        
        RAISE NOTICE 'Added refund_date column to payments table';
    ELSE
        RAISE NOTICE 'refund_date column already exists in payments table';
    END IF;
END $$;

-- Create index on updated_at for faster queries
CREATE INDEX IF NOT EXISTS idx_payments_updated_at ON payments(updated_at);

-- Create index on refund_date for faster refund queries
CREATE INDEX IF NOT EXISTS idx_payments_refund_date ON payments(refund_date);

-- Display summary
DO $$
DECLARE
    has_updated_at BOOLEAN;
    has_refund_amount BOOLEAN;
    has_refund_date BOOLEAN;
BEGIN
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payments' AND column_name = 'updated_at'
    ) INTO has_updated_at;
    
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payments' AND column_name = 'refund_amount'
    ) INTO has_refund_amount;
    
    SELECT EXISTS (
        SELECT 1 FROM information_schema.columns 
        WHERE table_name = 'payments' AND column_name = 'refund_date'
    ) INTO has_refund_date;
    
    RAISE NOTICE '';
    RAISE NOTICE '=== Migration Summary ===';
    RAISE NOTICE 'updated_at column: %', CASE WHEN has_updated_at THEN '✓ EXISTS' ELSE '✗ MISSING' END;
    RAISE NOTICE 'refund_amount column: %', CASE WHEN has_refund_amount THEN '✓ EXISTS' ELSE '✗ MISSING' END;
    RAISE NOTICE 'refund_date column: %', CASE WHEN has_refund_date THEN '✓ EXISTS' ELSE '✗ MISSING' END;
    RAISE NOTICE '========================';
    RAISE NOTICE '';
END $$;

