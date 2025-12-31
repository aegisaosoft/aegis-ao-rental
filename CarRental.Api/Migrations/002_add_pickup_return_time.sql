-- Migration: Add pickup_time and return_time columns to bookings table
-- This enables time-based availability checking for vehicle rentals
-- Duration calculation: 25 hours = 2 days, 24 hours 59 minutes = 1 day

-- Add pickup_time column (default 10:00 AM)
ALTER TABLE bookings 
ADD COLUMN IF NOT EXISTS pickup_time VARCHAR(5) DEFAULT '10:00';

-- Add return_time column (default 10:00 PM)
ALTER TABLE bookings 
ADD COLUMN IF NOT EXISTS return_time VARCHAR(5) DEFAULT '22:00';

-- Update existing records to have default times
UPDATE bookings 
SET pickup_time = '10:00' 
WHERE pickup_time IS NULL;

UPDATE bookings 
SET return_time = '22:00' 
WHERE return_time IS NULL;

-- Update the stored procedure to use datetime with time for availability check
CREATE OR REPLACE FUNCTION get_available_vehicles_by_company(
    p_company_id UUID,
    p_pickup_datetime TIMESTAMP,
    p_return_datetime TIMESTAMP,
    p_location_id UUID DEFAULT NULL
)
RETURNS TABLE (
    model_id UUID,
    make VARCHAR,
    model VARCHAR,
    fuel_type VARCHAR,
    transmission VARCHAR,
    seats INTEGER,
    years_available TEXT,
    avg_daily_rate DECIMAL,
    model_features TEXT,
    category_id UUID,
    category_name VARCHAR,
    all_vehicles_count BIGINT,
    available_count BIGINT
) AS $$
BEGIN
    RETURN QUERY
    WITH booked_vehicles AS (
        -- Find vehicles that are booked during the requested period
        -- Now using datetime comparison for precise time-based availability
        SELECT DISTINCT b.vehicle_id
        FROM bookings b
        WHERE b.company_id = p_company_id
          AND b.status NOT IN ('Cancelled', 'Completed', 'NoShow')
          -- Check for overlap using datetime (includes time)
          AND NOT (
              -- New booking ends before existing booking starts
              p_return_datetime <= (b.pickup_date + (COALESCE(b.pickup_time, '10:00')::TIME))
              OR
              -- New booking starts after existing booking ends
              p_pickup_datetime >= (b.return_date + (COALESCE(b.return_time, '22:00')::TIME))
          )
    ),
    available_vehicles AS (
        -- Get all vehicles that are NOT booked during the period
        SELECT 
            v.id AS vehicle_id,
            vm.model_id,
            m.make,
            m.model,
            m.fuel_type,
            m.transmission,
            m.seats,
            m.year,
            COALESCE(vm.daily_rate, 0) AS daily_rate,
            m.features AS model_features,
            m.category_id,
            c.category_name
        FROM vehicles v
        JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
        JOIN models m ON vm.model_id = m.id
        LEFT JOIN vehicle_categories c ON m.category_id = c.id
        WHERE v.company_id = p_company_id
          AND v.status = 'Available'
          AND (p_location_id IS NULL OR v.location_id = p_location_id)
          AND v.id NOT IN (SELECT vehicle_id FROM booked_vehicles)
    )
    SELECT 
        av.model_id,
        av.make,
        av.model,
        av.fuel_type,
        av.transmission,
        av.seats,
        STRING_AGG(DISTINCT av.year::TEXT, ', ' ORDER BY av.year::TEXT) AS years_available,
        AVG(av.daily_rate)::DECIMAL AS avg_daily_rate,
        MAX(av.model_features) AS model_features,
        av.category_id,
        MAX(av.category_name) AS category_name,
        -- Count all vehicles for this model (regardless of availability)
        (SELECT COUNT(*) 
         FROM vehicles v2 
         JOIN vehicle_model vm2 ON v2.vehicle_model_id = vm2.id 
         WHERE vm2.model_id = av.model_id 
           AND v2.company_id = p_company_id
           AND (p_location_id IS NULL OR v2.location_id = p_location_id)
        ) AS all_vehicles_count,
        -- Count available vehicles for this model during the period
        COUNT(DISTINCT av.vehicle_id) AS available_count
    FROM available_vehicles av
    GROUP BY av.model_id, av.make, av.model, av.fuel_type, av.transmission, av.seats, av.category_id
    ORDER BY av.make, av.model;
END;
$$ LANGUAGE plpgsql;

-- Add comment explaining the time-based availability logic
COMMENT ON FUNCTION get_available_vehicles_by_company IS 
'Checks vehicle availability using precise datetime comparison including time.
Duration is calculated as ceiling of hours divided by 24.
Example: 25 hours = 2 days, 24 hours 59 minutes = 1 day';
