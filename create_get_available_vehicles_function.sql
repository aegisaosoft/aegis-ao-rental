-- Create a function to get available vehicles by company
-- This function returns vehicle models that are available for rent
-- during the specified date range, filtered by company and optionally by location

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
    year INT,
    fuel_type VARCHAR(50),
    transmission VARCHAR(50),
    seats INT,
    category_name VARCHAR(100),
    min_daily_rate NUMERIC(10,2),
    max_daily_rate NUMERIC(10,2),
    avg_daily_rate NUMERIC(10,2),
    available_count BIGINT,
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
        m.id,
        m.make,
        m.model,
        m.year,
        m.fuel_type,
        m.transmission,
        m.seats,
        vc.category_name,
        MIN(vm.daily_rate) as min_rate,
        MAX(vm.daily_rate) as max_rate,
        AVG(vm.daily_rate) as avg_rate,
        COUNT(DISTINCT v.id)::BIGINT,
        ARRAY_AGG(DISTINCT v.color ORDER BY v.color) FILTER (WHERE v.color IS NOT NULL),
        ARRAY_AGG(DISTINCT cl.location_name ORDER BY cl.location_name),
        (SELECT v2.image_url
         FROM vehicles v2
         WHERE v2.vehicle_model_id = vm.id
         AND v2.image_url IS NOT NULL
         LIMIT 1),
        m.features
    FROM vehicles v
    INNER JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
    INNER JOIN models m ON vm.model_id = m.id
    LEFT JOIN vehicle_categories vc ON m.category_id = vc.id
    INNER JOIN company_location cl ON v.location_id = cl.id
    INNER JOIN companies c ON v.company_id = c.id
    WHERE
        v.company_id = p_company_id
        AND v.status = 'Available'
        AND c.is_active = true
        AND cl.is_active = true
        AND cl.is_pickup_location = true
        -- If p_location_id is NULL, include all locations; otherwise filter by specific location
        AND (p_location_id IS NULL OR v.location_id = p_location_id)
        AND NOT EXISTS (
            SELECT 1
            FROM bookings b
            WHERE b.vehicle_id = v.id
            AND b.status IN ('Pending', 'Confirmed', 'PickedUp')
            AND (b.pickup_date <= p_return_date AND b.return_date >= p_pickup_date)
        )
    GROUP BY
        m.id, m.make, m.model, m.year, m.fuel_type, m.transmission,
        m.seats, vc.category_name, m.features, vm.id
    HAVING COUNT(DISTINCT v.id) > 0
    ORDER BY
        vc.category_name NULLS LAST,
        m.make,
        m.model,
        m.year;
END;
$$;

-- Grant execute permission to the application user
-- Replace 'alex' with your actual application database user if different
GRANT EXECUTE ON FUNCTION get_available_vehicles_by_company(UUID, TIMESTAMP, TIMESTAMP, UUID) TO alex;

COMMENT ON FUNCTION get_available_vehicles_by_company IS 'Returns available vehicle models for a company during a date range, with optional location filter. Checks booking conflicts.';

