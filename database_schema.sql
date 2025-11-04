-- DROP SCHEMA public;

CREATE SCHEMA public AUTHORIZATION azure_pg_admin;

COMMENT ON SCHEMA public IS 'standard public schema';

-- DROP TYPE public.booking_status;

CREATE TYPE public.booking_status AS ENUM (
	'Pending',
	'Confirmed',
	'PickedUp',
	'Returned',
	'Cancelled',
	'NoShow');

COMMENT ON TYPE public.booking_status IS 'Valid status values for bookings';-- public.companies definition

-- Drop table

-- DROP TABLE public.companies;

CREATE TABLE public.companies (
	company_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_name varchar(255) NOT NULL,
	email varchar(255) NOT NULL,
	stripe_account_id varchar(255) NULL,
	tax_id varchar(100) NULL,
	is_active bool DEFAULT true NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	video_link varchar(500) NULL, -- URL link to company promotional or informational video (YouTube, Vimeo, etc.)
	banner_link varchar(500) NULL, -- URL link to company banner image for homepage or promotional display
	logo_link varchar(500) NULL, -- URL link to company logo image
	motto varchar(255) DEFAULT 'Meet our newest fleet yet'::character varying NULL, -- Company motto or tagline (e.g., "Drive Your Dreams")
	motto_description varchar(500) DEFAULT 'New rental cars. No lines. Let''s go!'::character varying NULL, -- Description or subtext for the motto
	invitation text DEFAULT 'Find & Book a Great Deal Today'::text NULL, -- Company invitation or welcome message for customers
	texts jsonb NULL, -- JSONB field for flexible text content storage
	website varchar(255) NULL, -- Company website URL
	background_link varchar(255) NULL, -- URL link to background image for the company
	about text NULL, -- Long text description about the company
	booking_integrated text NULL, -- Booking integration information or code
	company_path text NULL, -- Company path or URL slug
	subdomain varchar(100) NULL, -- Unique subdomain for the company
	primary_color varchar(7) NULL, -- Primary brand color in hex format (e.g., #FF5733)
	secondary_color varchar(7) NULL, -- Secondary brand color in hex format (e.g., #33C1FF)
	logo_url varchar(500) NULL, -- URL to company logo image
	favicon_url varchar(500) NULL, -- URL to company favicon
	custom_css text NULL, -- Custom CSS styles for the company
	CONSTRAINT companies_pkey PRIMARY KEY (company_id),
	CONSTRAINT rental_companies_email_key UNIQUE (email),
	CONSTRAINT rental_companies_stripe_account_id_key UNIQUE (stripe_account_id)
);
CREATE INDEX idx_companies_subdomain ON public.companies USING btree (subdomain);
CREATE INDEX idx_companies_texts ON public.companies USING gin (texts);

-- Column comments

COMMENT ON COLUMN public.companies.video_link IS 'URL link to company promotional or informational video (YouTube, Vimeo, etc.)';
COMMENT ON COLUMN public.companies.banner_link IS 'URL link to company banner image for homepage or promotional display';
COMMENT ON COLUMN public.companies.logo_link IS 'URL link to company logo image';
COMMENT ON COLUMN public.companies.motto IS 'Company motto or tagline (e.g., "Drive Your Dreams")';
COMMENT ON COLUMN public.companies.motto_description IS 'Description or subtext for the motto';
COMMENT ON COLUMN public.companies.invitation IS 'Company invitation or welcome message for customers';
COMMENT ON COLUMN public.companies.texts IS 'JSONB field for flexible text content storage';
COMMENT ON COLUMN public.companies.website IS 'Company website URL';
COMMENT ON COLUMN public.companies.background_link IS 'URL link to background image for the company';
COMMENT ON COLUMN public.companies.about IS 'Long text description about the company';
COMMENT ON COLUMN public.companies.booking_integrated IS 'Booking integration information or code';
COMMENT ON COLUMN public.companies.company_path IS 'Company path or URL slug';
COMMENT ON COLUMN public.companies.subdomain IS 'Unique subdomain for the company';
COMMENT ON COLUMN public.companies.primary_color IS 'Primary brand color in hex format (e.g., #FF5733)';
COMMENT ON COLUMN public.companies.secondary_color IS 'Secondary brand color in hex format (e.g., #33C1FF)';
COMMENT ON COLUMN public.companies.logo_url IS 'URL to company logo image';
COMMENT ON COLUMN public.companies.favicon_url IS 'URL to company favicon';
COMMENT ON COLUMN public.companies.custom_css IS 'Custom CSS styles for the company';

-- Table Triggers

create trigger update_companies_updated_at before
update
    on
    public.companies for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.companies OWNER TO alex;
GRANT ALL ON TABLE public.companies TO alex;


-- public.vehicle_categories definition

-- Drop table

-- DROP TABLE public.vehicle_categories;

CREATE TABLE public.vehicle_categories (
	category_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	category_name varchar(100) NOT NULL,
	description text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT vehicle_categories_pkey PRIMARY KEY (category_id)
);

-- Permissions

ALTER TABLE public.vehicle_categories OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_categories TO alex;


-- public.additional_services definition

-- Drop table

-- DROP TABLE public.additional_services;

CREATE TABLE public.additional_services (
	id uuid DEFAULT uuid_generate_v4() NOT NULL, -- Primary key
	company_id uuid NOT NULL, -- Company that offers this service
	"name" varchar(255) NOT NULL, -- Service name
	description text NULL, -- Detailed description of the service
	price numeric(10, 2) DEFAULT 0.00 NOT NULL, -- Price per unit/day for the service
	service_type varchar(50) NOT NULL, -- Type of service: Insurance, GPS, ChildSeat, AdditionalDriver, FuelPrepay, Cleaning, Delivery, Other
	is_mandatory bool DEFAULT false NOT NULL, -- Whether this service is mandatory for all bookings
	max_quantity int4 DEFAULT 1 NOT NULL, -- Maximum quantity allowed (e.g., max 4 child seats)
	is_active bool DEFAULT true NOT NULL, -- Whether this service is currently available
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT additional_services_pkey PRIMARY KEY (id),
	CONSTRAINT chk_service_type CHECK (((service_type)::text = ANY ((ARRAY['Insurance'::character varying, 'GPS'::character varying, 'ChildSeat'::character varying, 'AdditionalDriver'::character varying, 'FuelPrepay'::character varying, 'Cleaning'::character varying, 'Delivery'::character varying, 'Other'::character varying])::text[]))),
	CONSTRAINT fk_additional_services_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id)
);
CREATE INDEX idx_additional_services_company_id ON public.additional_services USING btree (company_id);
CREATE INDEX idx_additional_services_is_active ON public.additional_services USING btree (is_active);
CREATE INDEX idx_additional_services_service_type ON public.additional_services USING btree (service_type);
COMMENT ON TABLE public.additional_services IS 'Additional services that can be added to bookings';

-- Column comments

COMMENT ON COLUMN public.additional_services.id IS 'Primary key';
COMMENT ON COLUMN public.additional_services.company_id IS 'Company that offers this service';
COMMENT ON COLUMN public.additional_services."name" IS 'Service name';
COMMENT ON COLUMN public.additional_services.description IS 'Detailed description of the service';
COMMENT ON COLUMN public.additional_services.price IS 'Price per unit/day for the service';
COMMENT ON COLUMN public.additional_services.service_type IS 'Type of service: Insurance, GPS, ChildSeat, AdditionalDriver, FuelPrepay, Cleaning, Delivery, Other';
COMMENT ON COLUMN public.additional_services.is_mandatory IS 'Whether this service is mandatory for all bookings';
COMMENT ON COLUMN public.additional_services.max_quantity IS 'Maximum quantity allowed (e.g., max 4 child seats)';
COMMENT ON COLUMN public.additional_services.is_active IS 'Whether this service is currently available';

-- Permissions

ALTER TABLE public.additional_services OWNER TO alex;
GRANT ALL ON TABLE public.additional_services TO alex;


-- public.company_services definition

-- Drop table

-- DROP TABLE public.company_services;

CREATE TABLE public.company_services (
	company_id uuid NOT NULL, -- Reference to rental company
	additional_service_id uuid NOT NULL, -- Reference to additional service
	is_active bool DEFAULT true NOT NULL, -- Whether this service is currently active for this company
	created_at timestamptz DEFAULT now() NOT NULL, -- When this service was added to the company
	CONSTRAINT company_services_pkey PRIMARY KEY (company_id, additional_service_id),
	CONSTRAINT fk_company_services_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id),
	CONSTRAINT fk_company_services_service FOREIGN KEY (additional_service_id) REFERENCES public.additional_services(id) ON DELETE CASCADE
);
CREATE INDEX idx_company_services_company_id ON public.company_services USING btree (company_id);
CREATE INDEX idx_company_services_is_active ON public.company_services USING btree (is_active);
CREATE INDEX idx_company_services_service_id ON public.company_services USING btree (additional_service_id);
COMMENT ON TABLE public.company_services IS 'Junction table linking companies to their available additional services';

-- Column comments

COMMENT ON COLUMN public.company_services.company_id IS 'Reference to rental company';
COMMENT ON COLUMN public.company_services.additional_service_id IS 'Reference to additional service';
COMMENT ON COLUMN public.company_services.is_active IS 'Whether this service is currently active for this company';
COMMENT ON COLUMN public.company_services.created_at IS 'When this service was added to the company';

-- Permissions

ALTER TABLE public.company_services OWNER TO alex;
GRANT ALL ON TABLE public.company_services TO alex;


-- public.customers definition

-- Drop table

-- DROP TABLE public.customers;

CREATE TABLE public.customers (
	customer_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	email varchar(255) NOT NULL,
	first_name varchar(100) NOT NULL,
	last_name varchar(100) NOT NULL,
	phone varchar(50) NULL,
	password_hash varchar(500) NULL,
	date_of_birth date NULL,
	drivers_license_number varchar(100) NULL,
	drivers_license_state varchar(50) NULL,
	drivers_license_expiry date NULL,
	address text NULL,
	city varchar(100) NULL,
	state varchar(100) NULL,
	country varchar(100) NULL,
	postal_code varchar(20) NULL,
	stripe_customer_id varchar(255) NULL,
	is_verified bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	"role" varchar(50) DEFAULT 'customer'::character varying NULL,
	is_active bool DEFAULT true NULL,
	last_login timestamp NULL,
	company_id uuid NULL,
	CONSTRAINT customers_email_key UNIQUE (email),
	CONSTRAINT customers_pkey PRIMARY KEY (customer_id),
	CONSTRAINT customers_stripe_customer_id_key UNIQUE (stripe_customer_id),
	CONSTRAINT valid_customer_role CHECK (((role)::text = ANY ((ARRAY['customer'::character varying, 'worker'::character varying, 'admin'::character varying, 'mainadmin'::character varying])::text[]))),
	CONSTRAINT fk_customers_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id)
);
CREATE INDEX idx_customers_company_id ON public.customers USING btree (company_id);
CREATE INDEX idx_customers_is_active ON public.customers USING btree (is_active);
CREATE INDEX idx_customers_role ON public.customers USING btree (role);

-- Table Triggers

create trigger update_customers_updated_at before
update
    on
    public.customers for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.customers OWNER TO alex;
GRANT ALL ON TABLE public.customers TO alex;


-- public.aegis_users definition

-- Drop table

-- DROP TABLE public.aegis_users;

CREATE TABLE public.aegis_users (
	aegis_user_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	userid varchar(255) NOT NULL,
	first_name varchar(100) NOT NULL,
	last_name varchar(100) NOT NULL,
	phone varchar(50) NULL,
	password_hash varchar(500) NULL,
	date_of_birth date NULL,
	address text NULL,
	city varchar(100) NULL,
	state varchar(100) NULL,
	country varchar(100) NULL,
	postal_code varchar(20) NULL,
	stripe_customer_id varchar(255) NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	"role" varchar(50) DEFAULT 'agent'::character varying NULL,
	is_active bool DEFAULT true NULL,
	last_login timestamp NULL,
	CONSTRAINT aegis_users_userid_key UNIQUE (userid),
	CONSTRAINT aegis_users_pkey PRIMARY KEY (aegis_user_id),
	CONSTRAINT aegis_users_stripe_customer_id_key UNIQUE (stripe_customer_id),
	CONSTRAINT valid_aegis_user_role CHECK (((role)::text = ANY ((ARRAY['agent'::character varying, 'admin'::character varying])::text[])))
);
CREATE INDEX idx_aegis_users_is_active ON public.aegis_users USING btree (is_active);
CREATE INDEX idx_aegis_users_role ON public.aegis_users USING btree (role);

-- Table Triggers

create trigger update_aegis_users_updated_at before
update
    on
    public.aegis_users for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.aegis_users OWNER TO alex;
GRANT ALL ON TABLE public.aegis_users TO alex;


-- public.locations definition

-- Drop table

-- DROP TABLE public.locations;

CREATE TABLE public.locations (
	location_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	location_name varchar(255) NOT NULL,
	address text NULL,
	city varchar(100) NULL,
	state varchar(100) NULL,
	country varchar(100) DEFAULT 'USA'::character varying NULL,
	postal_code varchar(20) NULL,
	phone varchar(50) NULL,
	email varchar(255) NULL,
	latitude numeric(10, 8) NULL, -- GPS latitude coordinate for mapping
	longitude numeric(11, 8) NULL, -- GPS longitude coordinate for mapping
	is_active bool DEFAULT true NULL,
	is_pickup_location bool DEFAULT true NULL, -- Whether customers can pick up vehicles from this location
	is_return_location bool DEFAULT true NULL, -- Whether customers can return vehicles to this location
	opening_hours text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT locations_pkey PRIMARY KEY (location_id),
	CONSTRAINT fk_locations_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id)
);
CREATE INDEX idx_locations_company ON public.locations USING btree (company_id);
CREATE INDEX idx_locations_is_active ON public.locations USING btree (is_active);
CREATE INDEX idx_locations_pickup ON public.locations USING btree (is_pickup_location);
CREATE INDEX idx_locations_return ON public.locations USING btree (is_return_location);
CREATE INDEX idx_locations_state ON public.locations USING btree (state);
COMMENT ON TABLE public.locations IS 'Physical locations/branches for rental companies';

-- Column comments

COMMENT ON COLUMN public.locations.latitude IS 'GPS latitude coordinate for mapping';
COMMENT ON COLUMN public.locations.longitude IS 'GPS longitude coordinate for mapping';
COMMENT ON COLUMN public.locations.is_pickup_location IS 'Whether customers can pick up vehicles from this location';
COMMENT ON COLUMN public.locations.is_return_location IS 'Whether customers can return vehicles to this location';

-- Table Triggers

create trigger update_locations_updated_at before
update
    on
    public.locations for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.locations OWNER TO alex;
GRANT ALL ON TABLE public.locations TO alex;


-- public.users definition

-- Drop table

-- DROP TABLE public.users;

CREATE TABLE public.users (
	user_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	email varchar(255) NOT NULL,
	first_name varchar(100) NOT NULL,
	last_name varchar(100) NOT NULL,
	phone varchar(50) NULL,
	password_hash varchar(500) NOT NULL,
	"role" varchar(50) DEFAULT 'worker'::character varying NOT NULL,
	company_id uuid NULL,
	is_active bool DEFAULT true NULL,
	last_login timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT users_email_key UNIQUE (email),
	CONSTRAINT users_pkey PRIMARY KEY (user_id),
	CONSTRAINT valid_role CHECK (((role)::text = ANY ((ARRAY['worker'::character varying, 'admin'::character varying, 'mainadmin'::character varying])::text[]))),
	CONSTRAINT fk_users_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id)
);
CREATE INDEX idx_users_company_id ON public.users USING btree (company_id);
CREATE INDEX idx_users_email ON public.users USING btree (email);
CREATE INDEX idx_users_role ON public.users USING btree (role);

-- Table Triggers

create trigger update_users_updated_at before
update
    on
    public.users for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.users OWNER TO alex;
GRANT ALL ON TABLE public.users TO alex;


-- public.vehicles definition

-- Drop table

-- DROP TABLE public.vehicles;

CREATE TABLE public.vehicles (
	vehicle_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	category_id uuid NULL,
	make varchar(100) NOT NULL,
	model varchar(100) NOT NULL,
	"year" int4 NOT NULL,
	color varchar(50) NULL,
	license_plate varchar(50) NOT NULL,
	vin varchar(17) NULL,
	mileage int4 DEFAULT 0 NULL,
	fuel_type varchar(50) NULL,
	transmission varchar(50) NULL,
	seats int4 NULL,
	daily_rate numeric(10, 2) NOT NULL,
	status varchar(50) DEFAULT 'available'::character varying NULL,
	state varchar(2) NULL,
	"location" varchar(255) NULL,
	image_url text NULL,
	features _text NULL,
	is_active bool DEFAULT true NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	tag varchar(50) NULL,
	location_id uuid NULL, -- Reference to the locations table (replaces location VARCHAR)
	CONSTRAINT vehicles_license_plate_key UNIQUE (license_plate),
	CONSTRAINT vehicles_pkey PRIMARY KEY (vehicle_id),
	CONSTRAINT vehicles_vin_key UNIQUE (vin),
	CONSTRAINT fk_vehicles_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id),
	CONSTRAINT vehicles_category_id_fkey FOREIGN KEY (category_id) REFERENCES public.vehicle_categories(category_id),
	CONSTRAINT vehicles_location_id_fkey FOREIGN KEY (location_id) REFERENCES public.locations(location_id) ON DELETE SET NULL
);
CREATE INDEX idx_vehicles_category ON public.vehicles USING btree (category_id);
CREATE INDEX idx_vehicles_company ON public.vehicles USING btree (company_id);
CREATE INDEX idx_vehicles_location ON public.vehicles USING btree (location_id);
CREATE INDEX idx_vehicles_status ON public.vehicles USING btree (status);

-- Column comments

COMMENT ON COLUMN public.vehicles.location_id IS 'Reference to the locations table (replaces location VARCHAR)';

-- Table Triggers

create trigger update_vehicles_updated_at before
update
    on
    public.vehicles for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.vehicles OWNER TO alex;
GRANT ALL ON TABLE public.vehicles TO alex;


-- public.booking_tokens definition

-- Drop table

-- DROP TABLE public.booking_tokens;

CREATE TABLE public.booking_tokens (
	token_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	customer_email varchar(255) NOT NULL,
	vehicle_id uuid NOT NULL,
	"token" varchar(255) NOT NULL,
	booking_data jsonb NOT NULL,
	expires_at timestamp NOT NULL,
	is_used bool DEFAULT false NULL,
	used_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT booking_tokens_pkey PRIMARY KEY (token_id),
	CONSTRAINT booking_tokens_token_key UNIQUE (token),
	CONSTRAINT booking_tokens_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(vehicle_id) ON DELETE RESTRICT,
	CONSTRAINT fk_booking_tokens_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id)
);
CREATE INDEX idx_booking_tokens_company ON public.booking_tokens USING btree (company_id);
CREATE INDEX idx_booking_tokens_customer ON public.booking_tokens USING btree (customer_email);
CREATE INDEX idx_booking_tokens_expires ON public.booking_tokens USING btree (expires_at);
CREATE INDEX idx_booking_tokens_token ON public.booking_tokens USING btree (token);
CREATE INDEX idx_booking_tokens_used ON public.booking_tokens USING btree (is_used);
CREATE INDEX idx_booking_tokens_vehicle ON public.booking_tokens USING btree (vehicle_id);

-- Table Triggers

create trigger update_booking_tokens_updated_at before
update
    on
    public.booking_tokens for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.booking_tokens OWNER TO alex;
GRANT ALL ON TABLE public.booking_tokens TO alex;


-- public.bookings definition

-- Drop table

-- DROP TABLE public.bookings;

CREATE TABLE public.bookings (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	customer_id uuid NOT NULL,
	vehicle_id uuid NOT NULL,
	company_id uuid NOT NULL,
	booking_number varchar(50) NOT NULL,
	pickup_date timestamp NOT NULL,
	return_date timestamp NOT NULL,
	pickup_location varchar(255) NULL,
	return_location varchar(255) NULL,
	daily_rate numeric(10, 2) NOT NULL,
	total_days int4 NOT NULL,
	subtotal numeric(10, 2) NOT NULL,
	tax_amount numeric(10, 2) DEFAULT 0 NULL,
	insurance_amount numeric(10, 2) DEFAULT 0 NULL,
	additional_fees numeric(10, 2) DEFAULT 0 NULL,
	total_amount numeric(10, 2) NOT NULL,
	status varchar(50) DEFAULT 'Pending'::character varying NULL,
	notes text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	alt_booking_number varchar(100) NULL, -- Alternative booking number for external reference or integration with other systems
	CONSTRAINT bookings_pkey PRIMARY KEY (id),
	CONSTRAINT chk_booking_status CHECK (((status)::text = ANY ((ARRAY['Pending'::character varying, 'Confirmed'::character varying, 'PickedUp'::character varying, 'Returned'::character varying, 'Cancelled'::character varying, 'NoShow'::character varying])::text[]))),
	CONSTRAINT reservations_reservation_number_key UNIQUE (booking_number),
	CONSTRAINT fk_bookings_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id),
	CONSTRAINT reservations_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(customer_id) ON DELETE CASCADE,
	CONSTRAINT reservations_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(vehicle_id) ON DELETE RESTRICT
);
CREATE INDEX idx_bookings_alt_booking_number ON public.bookings USING btree (alt_booking_number);
CREATE INDEX idx_bookings_status ON public.bookings USING btree (status);
CREATE INDEX idx_reservations_company ON public.bookings USING btree (company_id);
CREATE INDEX idx_reservations_customer ON public.bookings USING btree (customer_id);
CREATE INDEX idx_reservations_dates ON public.bookings USING btree (pickup_date, return_date);
CREATE INDEX idx_reservations_vehicle ON public.bookings USING btree (vehicle_id);
COMMENT ON TABLE public.bookings IS 'Renamed from reservations - stores vehicle booking information';

-- Column comments

COMMENT ON COLUMN public.bookings.alt_booking_number IS 'Alternative booking number for external reference or integration with other systems';

-- Table Triggers

create trigger update_reservations_updated_at before
update
    on
    public.bookings for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.bookings OWNER TO alex;
GRANT ALL ON TABLE public.bookings TO alex;


-- public.customer_payment_methods definition

-- Drop table

-- DROP TABLE public.customer_payment_methods;

CREATE TABLE public.customer_payment_methods (
	payment_method_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	customer_id uuid NOT NULL,
	stripe_payment_method_id varchar(255) NOT NULL,
	card_brand varchar(50) NULL,
	card_last4 varchar(4) NULL,
	card_exp_month int4 NULL,
	card_exp_year int4 NULL,
	is_default bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT customer_payment_methods_pkey PRIMARY KEY (payment_method_id),
	CONSTRAINT customer_payment_methods_stripe_payment_method_id_key UNIQUE (stripe_payment_method_id),
	CONSTRAINT customer_payment_methods_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(customer_id) ON DELETE CASCADE
);
CREATE INDEX idx_customer_payment_methods_customer ON public.customer_payment_methods USING btree (customer_id);

-- Permissions

ALTER TABLE public.customer_payment_methods OWNER TO alex;
GRANT ALL ON TABLE public.customer_payment_methods TO alex;


-- public.email_notifications definition

-- Drop table

-- DROP TABLE public.email_notifications;

CREATE TABLE public.email_notifications (
	notification_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	booking_token_id uuid NULL,
	customer_email varchar(255) NOT NULL,
	notification_type varchar(50) NOT NULL,
	subject varchar(255) NOT NULL,
	body text NOT NULL,
	status varchar(50) DEFAULT 'pending'::character varying NULL,
	sent_at timestamp NULL,
	error_message text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT email_notifications_pkey PRIMARY KEY (notification_id),
	CONSTRAINT email_notifications_booking_token_id_fkey FOREIGN KEY (booking_token_id) REFERENCES public.booking_tokens(token_id) ON DELETE CASCADE
);
CREATE INDEX idx_email_notifications_booking_token ON public.email_notifications USING btree (booking_token_id);
CREATE INDEX idx_email_notifications_customer ON public.email_notifications USING btree (customer_email);
CREATE INDEX idx_email_notifications_status ON public.email_notifications USING btree (status);
CREATE INDEX idx_email_notifications_type ON public.email_notifications USING btree (notification_type);

-- Permissions

ALTER TABLE public.email_notifications OWNER TO alex;
GRANT ALL ON TABLE public.email_notifications TO alex;


-- public.rentals definition

-- Drop table

-- DROP TABLE public.rentals;

CREATE TABLE public.rentals (
	rental_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	reservation_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	vehicle_id uuid NOT NULL,
	company_id uuid NOT NULL,
	actual_pickup_date timestamp NOT NULL,
	expected_return_date timestamp NOT NULL,
	actual_return_date timestamp NULL,
	pickup_mileage int4 NULL,
	return_mileage int4 NULL,
	fuel_level_pickup varchar(50) NULL,
	fuel_level_return varchar(50) NULL,
	damage_notes_pickup text NULL,
	damage_notes_return text NULL,
	additional_charges numeric(10, 2) DEFAULT 0 NULL,
	status varchar(50) DEFAULT 'active'::character varying NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT rentals_pkey PRIMARY KEY (rental_id),
	CONSTRAINT fk_rentals_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id),
	CONSTRAINT rentals_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(customer_id) ON DELETE CASCADE,
	CONSTRAINT rentals_reservation_id_fkey FOREIGN KEY (reservation_id) REFERENCES public.bookings(id) ON DELETE CASCADE,
	CONSTRAINT rentals_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(vehicle_id) ON DELETE RESTRICT
);
CREATE INDEX idx_rentals_customer ON public.rentals USING btree (customer_id);
CREATE INDEX idx_rentals_dates ON public.rentals USING btree (actual_pickup_date, expected_return_date);
CREATE INDEX idx_rentals_status ON public.rentals USING btree (status);
CREATE INDEX idx_rentals_vehicle ON public.rentals USING btree (vehicle_id);

-- Table Triggers

create trigger update_rentals_updated_at before
update
    on
    public.rentals for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.rentals OWNER TO alex;
GRANT ALL ON TABLE public.rentals TO alex;


-- public.reviews definition

-- Drop table

-- DROP TABLE public.reviews;

CREATE TABLE public.reviews (
	review_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	rental_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	company_id uuid NOT NULL,
	vehicle_id uuid NOT NULL,
	rating int4 NULL,
	"comment" text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT reviews_pkey PRIMARY KEY (review_id),
	CONSTRAINT reviews_rating_check CHECK (((rating >= 1) AND (rating <= 5))),
	CONSTRAINT fk_reviews_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id),
	CONSTRAINT reviews_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(customer_id) ON DELETE CASCADE,
	CONSTRAINT reviews_rental_id_fkey FOREIGN KEY (rental_id) REFERENCES public.rentals(rental_id) ON DELETE CASCADE,
	CONSTRAINT reviews_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(vehicle_id) ON DELETE CASCADE
);

-- Permissions

ALTER TABLE public.reviews OWNER TO alex;
GRANT ALL ON TABLE public.reviews TO alex;


-- public.booking_confirmations definition

-- Drop table

-- DROP TABLE public.booking_confirmations;

CREATE TABLE public.booking_confirmations (
	confirmation_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	booking_token_id uuid NOT NULL,
	reservation_id uuid NULL,
	customer_email varchar(255) NOT NULL,
	confirmation_number varchar(50) NOT NULL,
	booking_details jsonb NOT NULL,
	payment_status varchar(50) NOT NULL,
	stripe_payment_intent_id varchar(255) NULL,
	confirmation_sent bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT booking_confirmations_confirmation_number_key UNIQUE (confirmation_number),
	CONSTRAINT booking_confirmations_pkey PRIMARY KEY (confirmation_id),
	CONSTRAINT booking_confirmations_booking_token_id_fkey FOREIGN KEY (booking_token_id) REFERENCES public.booking_tokens(token_id) ON DELETE CASCADE,
	CONSTRAINT booking_confirmations_reservation_id_fkey FOREIGN KEY (reservation_id) REFERENCES public.bookings(id) ON DELETE SET NULL
);
CREATE INDEX idx_booking_confirmations_confirmation_number ON public.booking_confirmations USING btree (confirmation_number);
CREATE INDEX idx_booking_confirmations_customer ON public.booking_confirmations USING btree (customer_email);
CREATE INDEX idx_booking_confirmations_payment_status ON public.booking_confirmations USING btree (payment_status);
CREATE INDEX idx_booking_confirmations_token ON public.booking_confirmations USING btree (booking_token_id);

-- Table Triggers

create trigger update_booking_confirmations_updated_at before
update
    on
    public.booking_confirmations for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.booking_confirmations OWNER TO alex;
GRANT ALL ON TABLE public.booking_confirmations TO alex;


-- public.payments definition

-- Drop table

-- DROP TABLE public.payments;

CREATE TABLE public.payments (
	payment_id uuid DEFAULT uuid_generate_v4() NOT NULL,
	reservation_id uuid NULL,
	rental_id uuid NULL,
	customer_id uuid NOT NULL,
	company_id uuid NOT NULL,
	amount numeric(10, 2) NOT NULL,
	currency varchar(10) DEFAULT 'USD'::character varying NULL,
	payment_type varchar(50) NOT NULL,
	payment_method varchar(50) NULL,
	stripe_payment_intent_id varchar(255) NULL,
	stripe_charge_id varchar(255) NULL,
	stripe_payment_method_id varchar(255) NULL,
	status varchar(50) DEFAULT 'pending'::character varying NULL,
	failure_reason text NULL,
	processed_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT payments_pkey PRIMARY KEY (payment_id),
	CONSTRAINT payments_stripe_payment_intent_id_key UNIQUE (stripe_payment_intent_id),
	CONSTRAINT fk_payments_company FOREIGN KEY (company_id) REFERENCES public.companies(company_id),
	CONSTRAINT payments_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(customer_id) ON DELETE CASCADE,
	CONSTRAINT payments_rental_id_fkey FOREIGN KEY (rental_id) REFERENCES public.rentals(rental_id) ON DELETE SET NULL,
	CONSTRAINT payments_reservation_id_fkey FOREIGN KEY (reservation_id) REFERENCES public.bookings(id) ON DELETE SET NULL
);
CREATE INDEX idx_payments_company ON public.payments USING btree (company_id);
CREATE INDEX idx_payments_customer ON public.payments USING btree (customer_id);
CREATE INDEX idx_payments_reservation ON public.payments USING btree (reservation_id);
CREATE INDEX idx_payments_status ON public.payments USING btree (status);
CREATE INDEX idx_payments_stripe_intent ON public.payments USING btree (stripe_payment_intent_id);

-- Permissions

ALTER TABLE public.payments OWNER TO alex;
GRANT ALL ON TABLE public.payments TO alex;



-- DROP FUNCTION public.update_updated_at_column();

CREATE OR REPLACE FUNCTION public.update_updated_at_column()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.update_updated_at_column() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_updated_at_column() TO alex;

-- DROP FUNCTION public.uuid_generate_v1();

CREATE OR REPLACE FUNCTION public.uuid_generate_v1()
 RETURNS uuid
 LANGUAGE c
 PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_generate_v1$function$
;

-- Permissions

ALTER FUNCTION public.uuid_generate_v1() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_generate_v1() TO azuresu;

-- DROP FUNCTION public.uuid_generate_v1mc();

CREATE OR REPLACE FUNCTION public.uuid_generate_v1mc()
 RETURNS uuid
 LANGUAGE c
 PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_generate_v1mc$function$
;

-- Permissions

ALTER FUNCTION public.uuid_generate_v1mc() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_generate_v1mc() TO azuresu;

-- DROP FUNCTION public.uuid_generate_v3(uuid, text);

CREATE OR REPLACE FUNCTION public.uuid_generate_v3(namespace uuid, name text)
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_generate_v3$function$
;

-- Permissions

ALTER FUNCTION public.uuid_generate_v3(uuid, text) OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_generate_v3(uuid, text) TO azuresu;

-- DROP FUNCTION public.uuid_generate_v4();

CREATE OR REPLACE FUNCTION public.uuid_generate_v4()
 RETURNS uuid
 LANGUAGE c
 PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_generate_v4$function$
;

-- Permissions

ALTER FUNCTION public.uuid_generate_v4() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_generate_v4() TO azuresu;

-- DROP FUNCTION public.uuid_generate_v5(uuid, text);

CREATE OR REPLACE FUNCTION public.uuid_generate_v5(namespace uuid, name text)
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_generate_v5$function$
;

-- Permissions

ALTER FUNCTION public.uuid_generate_v5(uuid, text) OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_generate_v5(uuid, text) TO azuresu;

-- DROP FUNCTION public.uuid_nil();

CREATE OR REPLACE FUNCTION public.uuid_nil()
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_nil$function$
;

-- Permissions

ALTER FUNCTION public.uuid_nil() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_nil() TO azuresu;

-- DROP FUNCTION public.uuid_ns_dns();

CREATE OR REPLACE FUNCTION public.uuid_ns_dns()
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_ns_dns$function$
;

-- Permissions

ALTER FUNCTION public.uuid_ns_dns() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_ns_dns() TO azuresu;

-- DROP FUNCTION public.uuid_ns_oid();

CREATE OR REPLACE FUNCTION public.uuid_ns_oid()
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_ns_oid$function$
;

-- Permissions

ALTER FUNCTION public.uuid_ns_oid() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_ns_oid() TO azuresu;

-- DROP FUNCTION public.uuid_ns_url();

CREATE OR REPLACE FUNCTION public.uuid_ns_url()
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_ns_url$function$
;

-- Permissions

ALTER FUNCTION public.uuid_ns_url() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_ns_url() TO azuresu;

-- DROP FUNCTION public.uuid_ns_x500();

CREATE OR REPLACE FUNCTION public.uuid_ns_x500()
 RETURNS uuid
 LANGUAGE c
 IMMUTABLE PARALLEL SAFE STRICT
AS '$libdir/uuid-ossp', $function$uuid_ns_x500$function$
;

-- Permissions

ALTER FUNCTION public.uuid_ns_x500() OWNER TO azuresu;
GRANT ALL ON FUNCTION public.uuid_ns_x500() TO azuresu;


-- Permissions

GRANT ALL ON SCHEMA public TO azure_pg_admin;
GRANT USAGE ON SCHEMA public TO public;