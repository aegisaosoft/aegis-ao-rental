-- Create a function to get available vehicles by company
-- This function returns vehicle models that are available for rent
-- during the specified date range, filtered by company and optionally by location

-- Drop the existing function first (required when changing return type)
DROP FUNCTION IF EXISTS get_available_vehicles_by_company(UUID, TIMESTAMP, TIMESTAMP, UUID);

CREATE OR REPLACE FUNCTION get_available_vehicles_by_company(
    p_company_id UUID,
    p_pickup_date TIMESTAMP,
    p_return_date TIMESTAMP,
    p_location_id UUID DEFAULT NULL  -- NULL = search all locations
)
RETURNS TABLE (
    model_id UUID,
    make VARCHAR(100),
    model VARCHAR(100),
    fuel_type VARCHAR(50),
    transmission VARCHAR(50),
    seats INT,
    category_id UUID,
    category_name VARCHAR(100),
    min_daily_rate NUMERIC(10,2),
    max_daily_rate NUMERIC(10,2),
    avg_daily_rate NUMERIC(10,2),
    available_count BIGINT,
    total_available_vehicles BIGINT,
    all_vehicles_count BIGINT,
    years_available TEXT,
    available_colors TEXT[],
    available_locations TEXT[],
    sample_image_url TEXT,
    model_features TEXT[]
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        m.id::UUID,
        m.make::VARCHAR(100),
        m.model::VARCHAR(100),
        m.fuel_type::VARCHAR(50),
        m.transmission::VARCHAR(50),
        m.seats::INT,
        m.category_id::UUID,
        vc.category_name::VARCHAR(100),
        MIN(vm.daily_rate)::NUMERIC(10,2) as min_rate,
        MAX(vm.daily_rate)::NUMERIC(10,2) as max_rate,
        AVG(vm.daily_rate)::NUMERIC(10,2) as avg_rate,
        COUNT(DISTINCT v.id) FILTER (
            WHERE v.status = 'Available'
            AND NOT EXISTS (
                SELECT 1
                FROM bookings b
                WHERE b.vehicle_id = v.id
                AND b.status IN ('Pending', 'Confirmed', 'PickedUp')
                AND b.pickup_date <= p_return_date
                AND b.return_date >= p_pickup_date
            )
        )::BIGINT as available_count,
        COUNT(DISTINCT v.id) FILTER (WHERE v.status = 'Available')::BIGINT as total_available_vehicles,
        COUNT(DISTINCT v.id)::BIGINT as all_vehicles_count,
        STRING_AGG(DISTINCT m.year::text, ', ' ORDER BY m.year::text)::TEXT as years_available,
        COALESCE(
            ARRAY_AGG(DISTINCT v.color ORDER BY v.color) FILTER (
                WHERE v.color IS NOT NULL 
                AND v.status = 'Available'
                AND NOT EXISTS (
                    SELECT 1
                    FROM bookings b
                    WHERE b.vehicle_id = v.id
                    AND b.status IN ('Pending', 'Confirmed', 'PickedUp')
                    AND b.pickup_date <= p_return_date
                    AND b.return_date >= p_pickup_date
                )
            ), 
            ARRAY[]::TEXT[]
        )::TEXT[] as available_colors,
        COALESCE(
            ARRAY_AGG(DISTINCT cl.location_name ORDER BY cl.location_name) FILTER (
                WHERE cl.location_name IS NOT NULL
                AND v.status = 'Available'
                AND cl.is_active = true
                AND NOT EXISTS (
                    SELECT 1
                    FROM bookings b
                    WHERE b.vehicle_id = v.id
                    AND b.status IN ('Pending', 'Confirmed', 'PickedUp')
                    AND b.pickup_date <= p_return_date
                    AND b.return_date >= p_pickup_date
                )
            ),
            ARRAY[]::TEXT[]
        )::TEXT[] as available_locations,
        MIN(v.image_url) FILTER (WHERE v.image_url IS NOT NULL)::TEXT as sample_image_url,
        COALESCE(m.features, ARRAY[]::TEXT[])::TEXT[] as model_features
    FROM vehicles v
    INNER JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
    INNER JOIN models m ON vm.model_id = m.id
    LEFT JOIN vehicle_categories vc ON m.category_id = vc.id
    LEFT JOIN company_location cl ON v.location_id = cl.id AND cl.is_active = true
    WHERE
        v.company_id = p_company_id
        -- If p_location_id is NULL, include all locations; otherwise filter by specific location
        AND (p_location_id IS NULL OR v.location_id = p_location_id)
    GROUP BY
        m.id, m.make, m.model, m.fuel_type, m.transmission,
        m.seats, m.category_id, vc.category_name, m.features
    HAVING COUNT(DISTINCT v.id) FILTER (
        WHERE v.status = 'Available'
        AND NOT EXISTS (
            SELECT 1
            FROM bookings b
            WHERE b.vehicle_id = v.id
            AND b.status IN ('Pending', 'Confirmed', 'PickedUp')
            AND b.pickup_date <= p_return_date
            AND b.return_date >= p_pickup_date
        )
    ) > 0
    ORDER BY
        vc.category_name NULLS LAST,
        m.make,
        m.model;
END;
$$;

-- Grant execute permission to the application user
-- Replace 'alex' with your actual application database user if different
GRANT EXECUTE ON FUNCTION get_available_vehicles_by_company(UUID, TIMESTAMP, TIMESTAMP, UUID) TO alex;

COMMENT ON FUNCTION get_available_vehicles_by_company IS 'Returns available vehicle models for a company during a date range, with optional location filter. Checks booking conflicts.';

