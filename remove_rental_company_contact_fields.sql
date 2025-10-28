-- Remove contact and address fields from rental_companies table (except email)
-- Created: 2025-10-28

-- Drop columns from rental_companies (keeping email)
ALTER TABLE rental_companies DROP COLUMN IF EXISTS phone;
ALTER TABLE rental_companies DROP COLUMN IF EXISTS address;
ALTER TABLE rental_companies DROP COLUMN IF EXISTS city;
ALTER TABLE rental_companies DROP COLUMN IF EXISTS state;
ALTER TABLE rental_companies DROP COLUMN IF EXISTS country;
ALTER TABLE rental_companies DROP COLUMN IF EXISTS postal_code;

-- Success message
DO $$
BEGIN
    RAISE NOTICE 'Successfully removed phone, address, city, state, country, and postal_code from rental_companies table';
    RAISE NOTICE 'Email field is kept in rental_companies table';
    RAISE NOTICE 'Full contact details are now available in the locations table';
END $$;

