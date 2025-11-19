-- Add Active and Completed statuses to booking status constraint
-- This script updates the bookings table to support additional status values

-- Step 1: Drop the existing check constraint
ALTER TABLE bookings DROP CONSTRAINT IF EXISTS chk_booking_status;

-- Step 2: Add the new check constraint with Active and Completed statuses
ALTER TABLE bookings 
ADD CONSTRAINT chk_booking_status 
CHECK (status IN (
    'Pending',
    'Confirmed',
    'PickedUp',
    'Returned',
    'Cancelled',
    'NoShow',
    'Active',
    'Completed'
));

-- Step 3: Update the booking_status enum type (if it exists)
-- Note: PostgreSQL doesn't allow modifying enums in place, so we need to:
-- 1. Add new values to the enum
-- 2. Or recreate the enum if needed

-- Check if the enum type exists and add new values
DO $$
BEGIN
    -- Add 'Active' if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM pg_enum 
        WHERE enumlabel = 'Active' 
        AND enumtypid = (SELECT oid FROM pg_type WHERE typname = 'booking_status')
    ) THEN
        ALTER TYPE booking_status ADD VALUE 'Active';
        RAISE NOTICE 'Added Active to booking_status enum';
    END IF;

    -- Add 'Completed' if it doesn't exist
    IF NOT EXISTS (
        SELECT 1 FROM pg_enum 
        WHERE enumlabel = 'Completed' 
        AND enumtypid = (SELECT oid FROM pg_type WHERE typname = 'booking_status')
    ) THEN
        ALTER TYPE booking_status ADD VALUE 'Completed';
        RAISE NOTICE 'Added Completed to booking_status enum';
    END IF;
EXCEPTION
    WHEN undefined_object THEN
        RAISE NOTICE 'booking_status enum type does not exist, skipping enum update';
END $$;

-- Verify the constraint
SELECT 
    conname AS constraint_name,
    pg_get_constraintdef(oid) AS constraint_definition
FROM pg_constraint
WHERE conname = 'chk_booking_status'
    AND conrelid = 'bookings'::regclass;

