-- Add additional_services_json column to bookings table
-- This stores the selected services at booking time

ALTER TABLE bookings 
ADD COLUMN IF NOT EXISTS additional_services_json TEXT;

-- Add index for querying bookings with services
CREATE INDEX IF NOT EXISTS idx_bookings_has_services 
ON bookings ((additional_services_json IS NOT NULL));

COMMENT ON COLUMN bookings.additional_services_json IS 'JSON array of selected additional services at booking time';
