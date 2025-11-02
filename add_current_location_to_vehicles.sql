-- Add current_location_id field to vehicles table
-- This field tracks the current physical location of the vehicle (which may differ from the assigned location_id)
-- This field references the location table (not company_location)

ALTER TABLE vehicles
ADD COLUMN current_location_id uuid NULL;

-- Add foreign key constraint to location table
ALTER TABLE vehicles
ADD CONSTRAINT fk_vehicles_current_location
FOREIGN KEY (current_location_id)
REFERENCES locations(id)
ON DELETE SET NULL;

-- Add index for performance
CREATE INDEX idx_vehicles_current_location_id ON vehicles(current_location_id);

-- Add comment
COMMENT ON COLUMN vehicles.current_location_id IS 'Current physical location of the vehicle (references locations table, can be null)';

