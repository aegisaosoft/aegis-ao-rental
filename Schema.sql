-- DROP SCHEMA public;

CREATE SCHEMA public AUTHORIZATION azure_pg_admin;

COMMENT ON SCHEMA public IS 'standard public schema';

-- DROP TYPE public."booking_status";

CREATE TYPE public."booking_status" AS ENUM (
	'Pending',
	'Confirmed',
	'PickedUp',
	'Returned',
	'Cancelled',
	'NoShow',
	'Active',
	'Completed');

COMMENT ON TYPE public."booking_status" IS 'Valid status values for bookings';
-- DROP TYPE public."meta_credential_status";

CREATE TYPE public."meta_credential_status" AS ENUM (
	'PendingPageSelection',
	'Active',
	'TokenExpired',
	'Revoked',
	'Error');

COMMENT ON TYPE public."meta_credential_status" IS 'Status values for Meta (Facebook/Instagram) OAuth credentials';
-- DROP TYPE public."social_platform";

CREATE TYPE public."social_platform" AS ENUM (
	'Facebook',
	'Instagram');

COMMENT ON TYPE public."social_platform" IS 'Social media platforms for posting';
-- DROP SEQUENCE public.auto_insurance_cards_id_seq;

CREATE SEQUENCE public.auto_insurance_cards_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 2147483647
	START 1
	CACHE 1
	NO CYCLE;

-- Permissions

ALTER SEQUENCE public.auto_insurance_cards_id_seq OWNER TO alex;
GRANT ALL ON SEQUENCE public.auto_insurance_cards_id_seq TO alex;

-- DROP SEQUENCE public.company_meta_credentials_id_seq;

CREATE SEQUENCE public.company_meta_credentials_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 2147483647
	START 1
	CACHE 1
	NO CYCLE;

-- Permissions

ALTER SEQUENCE public.company_meta_credentials_id_seq OWNER TO alex;
GRANT ALL ON SEQUENCE public.company_meta_credentials_id_seq TO alex;
GRANT ALL ON SEQUENCE public.company_meta_credentials_id_seq TO azure_pg_admin;

-- DROP SEQUENCE public.instagram_conversations_id_seq;

CREATE SEQUENCE public.instagram_conversations_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 2147483647
	START 1
	CACHE 1
	NO CYCLE;

-- Permissions

ALTER SEQUENCE public.instagram_conversations_id_seq OWNER TO alex;
GRANT ALL ON SEQUENCE public.instagram_conversations_id_seq TO alex;

-- DROP SEQUENCE public.instagram_messages_id_seq;

CREATE SEQUENCE public.instagram_messages_id_seq
	INCREMENT BY 1
	MINVALUE 1
	MAXVALUE 9223372036854775807
	START 1
	CACHE 1
	NO CYCLE;

-- Permissions

ALTER SEQUENCE public.instagram_messages_id_seq OWNER TO alex;
GRANT ALL ON SEQUENCE public.instagram_messages_id_seq TO alex;
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
	CONSTRAINT aegis_users_pkey PRIMARY KEY (aegis_user_id),
	CONSTRAINT aegis_users_stripe_customer_id_key UNIQUE (stripe_customer_id),
	CONSTRAINT aegis_users_userid_key UNIQUE (userid),
	CONSTRAINT ck_aegis_users_role_valid CHECK (((role)::text = ANY ((ARRAY['agent'::character varying, 'admin'::character varying, 'mainadmin'::character varying, 'designer'::character varying])::text[]))),
	CONSTRAINT valid_aegis_user_role CHECK (((role)::text = ANY ((ARRAY['agent'::character varying, 'admin'::character varying, 'mainadmin'::character varying, 'designer'::character varying])::text[])))
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


-- public.currencies definition

-- Drop table

-- DROP TABLE public.currencies;

CREATE TABLE public.currencies (
	code varchar(3) NOT NULL,
	"name" text NOT NULL,
	symbol text NULL,
	is_default bool DEFAULT false NOT NULL,
	allow_global bool DEFAULT false NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT currencies_pkey PRIMARY KEY (code)
);

-- Table Triggers

create trigger trg_set_currencies_updated_at before
update
    on
    public.currencies for each row execute function trg_set_currencies_updated_at();

-- Permissions

ALTER TABLE public.currencies OWNER TO alex;
GRANT ALL ON TABLE public.currencies TO alex;


-- public.location_types definition

-- Drop table

-- DROP TABLE public.location_types;

CREATE TABLE public.location_types (
	id int2 NOT NULL,
	"name" varchar(50) NOT NULL,
	description varchar(200) NULL,
	CONSTRAINT location_types_pkey PRIMARY KEY (id)
);

-- Permissions

ALTER TABLE public.location_types OWNER TO alex;
GRANT ALL ON TABLE public.location_types TO alex;


-- public.refresh_tokens definition

-- Drop table

-- DROP TABLE public.refresh_tokens;

CREATE TABLE public.refresh_tokens (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	"token" varchar(500) NOT NULL,
	user_id uuid NOT NULL,
	user_type varchar(50) NOT NULL, -- Type of user: customer or aegis_user
	expires_at timestamp NOT NULL,
	created_at timestamp DEFAULT now() NOT NULL,
	revoked_at timestamp NULL, -- Timestamp when token was revoked (null if active)
	revoked_by_ip varchar(100) NULL,
	replaced_by_token varchar(500) NULL, -- New token that replaced this one during refresh
	created_by_ip varchar(100) NULL,
	CONSTRAINT refresh_tokens_pkey PRIMARY KEY (id)
);
CREATE INDEX idx_refresh_tokens_expires ON public.refresh_tokens USING btree (expires_at) WHERE (revoked_at IS NULL);
CREATE INDEX idx_refresh_tokens_token ON public.refresh_tokens USING btree (token);
CREATE UNIQUE INDEX idx_refresh_tokens_token_unique ON public.refresh_tokens USING btree (token);
CREATE INDEX idx_refresh_tokens_user ON public.refresh_tokens USING btree (user_id, user_type);
COMMENT ON TABLE public.refresh_tokens IS 'Stores refresh tokens for JWT authentication';

-- Column comments

COMMENT ON COLUMN public.refresh_tokens.user_type IS 'Type of user: customer or aegis_user';
COMMENT ON COLUMN public.refresh_tokens.revoked_at IS 'Timestamp when token was revoked (null if active)';
COMMENT ON COLUMN public.refresh_tokens.replaced_by_token IS 'New token that replaced this one during refresh';

-- Permissions

ALTER TABLE public.refresh_tokens OWNER TO alex;
GRANT ALL ON TABLE public.refresh_tokens TO alex;


-- public.settings definition

-- Drop table

-- DROP TABLE public.settings;

CREATE TABLE public.settings (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	"key" text NOT NULL,
	value text NOT NULL,
	description text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT settings_key_key UNIQUE (key),
	CONSTRAINT settings_pkey PRIMARY KEY (id)
);
CREATE UNIQUE INDEX settings_key_unique ON public.settings USING btree (key);

-- Table Triggers

create trigger trg_settings_updated_at before
update
    on
    public.settings for each row execute function set_settings_updated_at();

-- Permissions

ALTER TABLE public.settings OWNER TO alex;
GRANT ALL ON TABLE public.settings TO alex;


-- public.stripe_settings definition

-- Drop table

-- DROP TABLE public.stripe_settings;

CREATE TABLE public.stripe_settings (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	"name" varchar(20) NOT NULL, -- Name identifier for the Stripe settings
	secret_key text NULL, -- Stripe secret key
	publishable_key text NULL, -- Stripe publishable key
	webhook_secret text NULL, -- Stripe webhook secret
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT stripe_settings_pkey PRIMARY KEY (id)
);
CREATE INDEX idx_stripe_settings_name ON public.stripe_settings USING btree (name);
COMMENT ON TABLE public.stripe_settings IS 'Stripe API settings configuration';

-- Column comments

COMMENT ON COLUMN public.stripe_settings."name" IS 'Name identifier for the Stripe settings';
COMMENT ON COLUMN public.stripe_settings.secret_key IS 'Stripe secret key';
COMMENT ON COLUMN public.stripe_settings.publishable_key IS 'Stripe publishable key';
COMMENT ON COLUMN public.stripe_settings.webhook_secret IS 'Stripe webhook secret';

-- Table Triggers

create trigger update_stripe_settings_updated_at before
update
    on
    public.stripe_settings for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.stripe_settings OWNER TO alex;
GRANT ALL ON TABLE public.stripe_settings TO alex;


-- public.tracking_sync_log definition

-- Drop table

-- DROP TABLE public.tracking_sync_log;

CREATE TABLE public.tracking_sync_log (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	sync_type varchar(50) NOT NULL,
	started_at timestamp NOT NULL,
	completed_at timestamp NULL,
	records_fetched int4 DEFAULT 0 NULL,
	records_inserted int4 DEFAULT 0 NULL,
	records_updated int4 DEFAULT 0 NULL,
	status varchar(20) DEFAULT 'running'::character varying NULL,
	error_message text NULL,
	CONSTRAINT chk_sync_status CHECK (((status)::text = ANY ((ARRAY['running'::character varying, 'completed'::character varying, 'failed'::character varying])::text[]))),
	CONSTRAINT tracking_sync_log_pkey PRIMARY KEY (id)
);
CREATE INDEX idx_tracking_sync_log_type_time ON public.tracking_sync_log USING btree (sync_type, started_at DESC);
COMMENT ON TABLE public.tracking_sync_log IS 'Log of data synchronization with Datatrack API';

-- Permissions

ALTER TABLE public.tracking_sync_log OWNER TO alex;
GRANT ALL ON TABLE public.tracking_sync_log TO alex;


-- public.vehicle_categories definition

-- Drop table

-- DROP TABLE public.vehicle_categories;

CREATE TABLE public.vehicle_categories (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	category_name varchar(100) NOT NULL,
	description text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT vehicle_categories_pkey PRIMARY KEY (id)
);

-- Permissions

ALTER TABLE public.vehicle_categories OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_categories TO alex;


-- public.vehicles_backup definition

-- Drop table

-- DROP TABLE public.vehicles_backup;

CREATE TABLE public.vehicles_backup (
	id uuid NULL,
	company_id uuid NULL,
	make varchar(100) NULL,
	model varchar(100) NULL,
	"year" int4 NULL,
	color varchar(50) NULL,
	license_plate varchar(50) NULL,
	vin varchar(17) NULL,
	mileage int4 NULL,
	transmission varchar(50) NULL,
	seats int4 NULL,
	daily_rate numeric(10, 2) NULL,
	status varchar(50) NULL,
	state varchar(2) NULL,
	"location" varchar(255) NULL,
	image_url text NULL,
	features _text NULL,
	created_at timestamp NULL,
	updated_at timestamp NULL,
	tag varchar(50) NULL,
	location_id uuid NULL,
	current_location_id uuid NULL
);

-- Permissions

ALTER TABLE public.vehicles_backup OWNER TO alex;
GRANT ALL ON TABLE public.vehicles_backup TO alex;


-- public.companies definition

-- Drop table

-- DROP TABLE public.companies;

CREATE TABLE public.companies (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_name varchar(255) NOT NULL,
	email varchar(255) NOT NULL,
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
	about jsonb NULL, -- Long text description about the company
	booking_integrated text NULL, -- Booking integration information or code
	company_path text NULL, -- Company path or URL slug
	subdomain varchar(100) NULL, -- Unique subdomain for the company (e.g., company1 for company1.aegis-rental.com). Used for domain-based multi-tenancy.
	primary_color varchar(7) NULL, -- Hex color code for primary brand color (#RRGGBB format)
	secondary_color varchar(7) NULL, -- Hex color code for secondary brand color (#RRGGBB format)
	logo_url varchar(500) NULL, -- URL to company logo image
	favicon_url varchar(500) NULL, -- URL to company favicon
	custom_css text NULL, -- Custom CSS overrides for company-specific styling
	country varchar(100) NULL,
	"language" varchar(10) DEFAULT 'en'::character varying NULL, -- Company preferred language (ISO 639-1 code like en, es, pt, etc.)
	blink_key text NULL, -- BlinkID license key for the company (domain-specific license)
	currency varchar(3) DEFAULT 'USD'::character varying NOT NULL,
	ai_integration varchar(20) DEFAULT 'claude'::character varying NOT NULL,
	security_deposit numeric(10, 2) DEFAULT 1000 NOT NULL,
	terms_of_use jsonb NULL,
	is_security_deposit_mandatory bool DEFAULT true NOT NULL,
	stripe_onboarding_completed bool DEFAULT false NULL, -- Whether Stripe onboarding is fully completed
	stripe_charges_enabled bool DEFAULT false NULL, -- Whether company can accept charges
	stripe_payouts_enabled bool DEFAULT false NULL, -- Whether company can receive payouts
	stripe_details_submitted bool DEFAULT false NULL, -- Whether all required details have been submitted
	stripe_account_type varchar(20) DEFAULT 'express'::character varying NULL, -- Type of Stripe account: express or custom
	platform_fee_percentage numeric(5, 2) DEFAULT 10.00 NULL, -- Platform commission percentage (default 10%)
	stripe_requirements_currently_due _text NULL, -- Array of currently due verification requirements
	stripe_requirements_eventually_due _text NULL, -- Array of eventually due verification requirements
	stripe_requirements_past_due _text NULL, -- Array of past due verification requirements
	stripe_requirements_disabled_reason varchar(255) NULL, -- Reason why account is disabled
	stripe_last_sync_at timestamptz NULL, -- Last time account status was synced with Stripe
	stripe_onboarding_link text NULL, -- Latest onboarding link URL
	stripe_onboarding_link_expires_at timestamptz NULL, -- When the onboarding link expires
	is_test_company bool DEFAULT true NULL,
	stripe_settings_id uuid NULL, -- Reference to stripe_settings table
	deep_link_base_url text NULL,
	CONSTRAINT companies_pkey PRIMARY KEY (id),
	CONSTRAINT companies_subdomain_key UNIQUE (subdomain),
	CONSTRAINT rental_companies_email_key UNIQUE (email),
	CONSTRAINT fk_companies_currency FOREIGN KEY (currency) REFERENCES public.currencies(code),
	CONSTRAINT fk_companies_stripe_settings FOREIGN KEY (stripe_settings_id) REFERENCES public.stripe_settings(id) ON DELETE SET NULL
);
CREATE INDEX idx_companies_country ON public.companies USING btree (country);
CREATE INDEX idx_companies_created_at ON public.companies USING btree (created_at DESC);
CREATE INDEX idx_companies_is_active ON public.companies USING btree (is_active);
CREATE INDEX idx_companies_language ON public.companies USING btree (language);
CREATE INDEX idx_companies_onboarding_completed ON public.companies USING btree (stripe_onboarding_completed);
CREATE INDEX idx_companies_stripe_settings_id ON public.companies USING btree (stripe_settings_id);
CREATE INDEX idx_companies_stripe_status ON public.companies USING btree (stripe_charges_enabled, stripe_payouts_enabled);
CREATE INDEX idx_companies_stripe_sync ON public.companies USING btree (stripe_last_sync_at);
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
COMMENT ON COLUMN public.companies.subdomain IS 'Unique subdomain for the company (e.g., company1 for company1.aegis-rental.com). Used for domain-based multi-tenancy.';
COMMENT ON COLUMN public.companies.primary_color IS 'Hex color code for primary brand color (#RRGGBB format)';
COMMENT ON COLUMN public.companies.secondary_color IS 'Hex color code for secondary brand color (#RRGGBB format)';
COMMENT ON COLUMN public.companies.logo_url IS 'URL to company logo image';
COMMENT ON COLUMN public.companies.favicon_url IS 'URL to company favicon';
COMMENT ON COLUMN public.companies.custom_css IS 'Custom CSS overrides for company-specific styling';
COMMENT ON COLUMN public.companies."language" IS 'Company preferred language (ISO 639-1 code like en, es, pt, etc.)';
COMMENT ON COLUMN public.companies.blink_key IS 'BlinkID license key for the company (domain-specific license)';
COMMENT ON COLUMN public.companies.stripe_onboarding_completed IS 'Whether Stripe onboarding is fully completed';
COMMENT ON COLUMN public.companies.stripe_charges_enabled IS 'Whether company can accept charges';
COMMENT ON COLUMN public.companies.stripe_payouts_enabled IS 'Whether company can receive payouts';
COMMENT ON COLUMN public.companies.stripe_details_submitted IS 'Whether all required details have been submitted';
COMMENT ON COLUMN public.companies.stripe_account_type IS 'Type of Stripe account: express or custom';
COMMENT ON COLUMN public.companies.platform_fee_percentage IS 'Platform commission percentage (default 10%)';
COMMENT ON COLUMN public.companies.stripe_requirements_currently_due IS 'Array of currently due verification requirements';
COMMENT ON COLUMN public.companies.stripe_requirements_eventually_due IS 'Array of eventually due verification requirements';
COMMENT ON COLUMN public.companies.stripe_requirements_past_due IS 'Array of past due verification requirements';
COMMENT ON COLUMN public.companies.stripe_requirements_disabled_reason IS 'Reason why account is disabled';
COMMENT ON COLUMN public.companies.stripe_last_sync_at IS 'Last time account status was synced with Stripe';
COMMENT ON COLUMN public.companies.stripe_onboarding_link IS 'Latest onboarding link URL';
COMMENT ON COLUMN public.companies.stripe_onboarding_link_expires_at IS 'When the onboarding link expires';
COMMENT ON COLUMN public.companies.stripe_settings_id IS 'Reference to stripe_settings table';

-- Table Triggers

create trigger update_companies_updated_at before
update
    on
    public.companies for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.companies OWNER TO alex;
GRANT ALL ON TABLE public.companies TO alex;


-- public.company_auto_post_settings definition

-- Drop table

-- DROP TABLE public.company_auto_post_settings;

CREATE TABLE public.company_auto_post_settings (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	is_enabled bool DEFAULT false NOT NULL,
	post_on_vehicle_added bool DEFAULT true NOT NULL,
	post_on_vehicle_updated bool DEFAULT false NOT NULL,
	post_on_vehicle_available bool DEFAULT false NOT NULL,
	post_on_price_change bool DEFAULT false NOT NULL,
	include_price_in_posts bool DEFAULT true NOT NULL,
	default_hashtags _text NULL,
	default_call_to_action text NULL,
	cross_post_to_facebook bool DEFAULT false NOT NULL,
	min_hours_between_posts int4 DEFAULT 4 NOT NULL,
	last_auto_post_at timestamptz NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT company_auto_post_settings_company_id_key UNIQUE (company_id),
	CONSTRAINT company_auto_post_settings_pkey PRIMARY KEY (id),
	CONSTRAINT company_auto_post_settings_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_company_auto_post_settings_company_id ON public.company_auto_post_settings USING btree (company_id);
COMMENT ON TABLE public.company_auto_post_settings IS 'Company-level settings for automatic posting';

-- Table Triggers

create trigger update_company_auto_post_settings_updated_at before
update
    on
    public.company_auto_post_settings for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.company_auto_post_settings OWNER TO alex;
GRANT ALL ON TABLE public.company_auto_post_settings TO alex;


-- public.company_location definition

-- Drop table

-- DROP TABLE public.company_location;

CREATE TABLE public.company_location (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
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
	is_office bool DEFAULT false NOT NULL,
	CONSTRAINT company_location_pkey PRIMARY KEY (id),
	CONSTRAINT fk_company_location_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_company_location_company ON public.company_location USING btree (company_id);
CREATE INDEX idx_company_location_is_active ON public.company_location USING btree (is_active);
CREATE INDEX idx_company_location_is_office ON public.company_location USING btree (is_office);
CREATE INDEX idx_company_location_pickup ON public.company_location USING btree (is_pickup_location);
CREATE INDEX idx_company_location_return ON public.company_location USING btree (is_return_location);
CREATE INDEX idx_company_location_state ON public.company_location USING btree (state);
COMMENT ON TABLE public.company_location IS 'Company locations with the same structure as locations table';

-- Column comments

COMMENT ON COLUMN public.company_location.latitude IS 'GPS latitude coordinate for mapping';
COMMENT ON COLUMN public.company_location.longitude IS 'GPS longitude coordinate for mapping';
COMMENT ON COLUMN public.company_location.is_pickup_location IS 'Whether customers can pick up vehicles from this location';
COMMENT ON COLUMN public.company_location.is_return_location IS 'Whether customers can return vehicles to this location';

-- Table Triggers

create trigger update_company_location_updated_at before
update
    on
    public.company_location for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.company_location OWNER TO alex;
GRANT ALL ON TABLE public.company_location TO alex;


-- public.company_meta_credentials definition

-- Drop table

-- DROP TABLE public.company_meta_credentials;

CREATE TABLE public.company_meta_credentials (
	id serial4 NOT NULL,
	company_id uuid NOT NULL, -- Reference to the rental company
	user_access_token text NOT NULL, -- Long-lived user access token (60 days expiration)
	token_expires_at timestamptz NOT NULL, -- When the user access token expires
	page_id varchar(50) NULL, -- Facebook Page ID selected for this company
	page_name varchar(255) NULL, -- Facebook Page name
	page_access_token text NULL, -- Page access token (never expires while user token is valid)
	catalog_id varchar(50) NULL, -- Facebook Product Catalog ID
	pixel_id varchar(50) NULL, -- Facebook Pixel ID for tracking
	instagram_account_id varchar(50) NULL, -- Instagram Business Account ID
	instagram_username varchar(100) NULL, -- Instagram username
	available_pages jsonb NULL, -- JSON array of available Facebook Pages for selection
	status varchar(50) DEFAULT 'PendingPageSelection'::character varying NOT NULL, -- Current status of the Meta integration
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	last_token_refresh timestamptz NULL,
	auto_publish_facebook bool DEFAULT false NULL,
	auto_publish_instagram bool DEFAULT false NULL,
	auto_publish_include_price bool DEFAULT true NULL,
	auto_publish_hashtags text NULL,
	deep_link_base_url varchar(500) NULL, -- Base URL for social media deep links (e.g., https://mycompany.aegis-rental.com)
	deep_link_vehicle_pattern varchar(500) NULL, -- URL pattern for vehicle pages. Placeholders: {modelId}, {vehicleId}, {make}, {model}, {companyId}, {category}
	deep_link_booking_pattern varchar(500) NULL, -- URL pattern for booking pages. Placeholders: {bookingId}, {companyId}
	"DeepLinkBaseUrl" text NULL,
	"DeepLinkBookingPattern" text NULL,
	"DeepLinkVehiclePattern" text NULL,
	facebook_domain_verification_code varchar(100) NULL, -- Facebook domain verification code for Instagram Shopping. Obtained from Meta Business Settings → Domains.
	"FacebookDomainVerificationCode" varchar(100) NULL,
	CONSTRAINT company_meta_credentials_company_id_key UNIQUE (company_id),
	CONSTRAINT company_meta_credentials_pkey PRIMARY KEY (id),
	CONSTRAINT fk_company_meta_credentials_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_company_meta_credentials_company_id ON public.company_meta_credentials USING btree (company_id);
CREATE INDEX idx_company_meta_credentials_status ON public.company_meta_credentials USING btree (status);
CREATE INDEX idx_company_meta_credentials_token_expires ON public.company_meta_credentials USING btree (token_expires_at) WHERE ((status)::text = 'Active'::text);
COMMENT ON TABLE public.company_meta_credentials IS 'Stores Meta/Facebook/Instagram OAuth credentials per company';

-- Column comments

COMMENT ON COLUMN public.company_meta_credentials.company_id IS 'Reference to the rental company';
COMMENT ON COLUMN public.company_meta_credentials.user_access_token IS 'Long-lived user access token (60 days expiration)';
COMMENT ON COLUMN public.company_meta_credentials.token_expires_at IS 'When the user access token expires';
COMMENT ON COLUMN public.company_meta_credentials.page_id IS 'Facebook Page ID selected for this company';
COMMENT ON COLUMN public.company_meta_credentials.page_name IS 'Facebook Page name';
COMMENT ON COLUMN public.company_meta_credentials.page_access_token IS 'Page access token (never expires while user token is valid)';
COMMENT ON COLUMN public.company_meta_credentials.catalog_id IS 'Facebook Product Catalog ID';
COMMENT ON COLUMN public.company_meta_credentials.pixel_id IS 'Facebook Pixel ID for tracking';
COMMENT ON COLUMN public.company_meta_credentials.instagram_account_id IS 'Instagram Business Account ID';
COMMENT ON COLUMN public.company_meta_credentials.instagram_username IS 'Instagram username';
COMMENT ON COLUMN public.company_meta_credentials.available_pages IS 'JSON array of available Facebook Pages for selection';
COMMENT ON COLUMN public.company_meta_credentials.status IS 'Current status of the Meta integration';
COMMENT ON COLUMN public.company_meta_credentials.deep_link_base_url IS 'Base URL for social media deep links (e.g., https://mycompany.aegis-rental.com)';
COMMENT ON COLUMN public.company_meta_credentials.deep_link_vehicle_pattern IS 'URL pattern for vehicle pages. Placeholders: {modelId}, {vehicleId}, {make}, {model}, {companyId}, {category}';
COMMENT ON COLUMN public.company_meta_credentials.deep_link_booking_pattern IS 'URL pattern for booking pages. Placeholders: {bookingId}, {companyId}';
COMMENT ON COLUMN public.company_meta_credentials.facebook_domain_verification_code IS 'Facebook domain verification code for Instagram Shopping. Obtained from Meta Business Settings → Domains.';

-- Table Triggers

create trigger trigger_company_meta_credentials_updated_at before
update
    on
    public.company_meta_credentials for each row execute function update_company_meta_credentials_updated_at();
create trigger trigger_update_company_meta_credentials_updated_at before
update
    on
    public.company_meta_credentials for each row execute function update_company_meta_credentials_updated_at();

-- Permissions

ALTER TABLE public.company_meta_credentials OWNER TO alex;
GRANT ALL ON TABLE public.company_meta_credentials TO alex;
GRANT ALL ON TABLE public.company_meta_credentials TO azure_pg_admin;


-- public.company_mode definition

-- Drop table

-- DROP TABLE public.company_mode;

CREATE TABLE public.company_mode (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	is_rental bool DEFAULT true NOT NULL,
	is_violations bool DEFAULT true NOT NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT company_mode_pkey PRIMARY KEY (id),
	CONSTRAINT fk_company_mode_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE UNIQUE INDEX idx_company_mode_company_id ON public.company_mode USING btree (company_id);

-- Permissions

ALTER TABLE public.company_mode OWNER TO alex;
GRANT ALL ON TABLE public.company_mode TO alex;


-- public.customers definition

-- Drop table

-- DROP TABLE public.customers;

CREATE TABLE public.customers (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	email varchar(255) NOT NULL,
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
	is_verified bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	"role" varchar(50) DEFAULT 'customer'::character varying NULL,
	is_active bool DEFAULT true NULL,
	last_login timestamp NULL,
	company_id uuid NULL,
	customer_type varchar(50) DEFAULT 'Individual'::character varying NOT NULL,
	is_security_deposit_mandatory bool DEFAULT true NULL,
	"token" text NULL,
	google_id varchar(255) NULL, -- Google user ID from OAuth authentication
	google_picture varchar(500) NULL, -- Google profile picture URL
	auth_provider varchar(50) NULL, -- Authentication provider used (e.g., "google", "email")
	CONSTRAINT chk_customer_type CHECK (((customer_type)::text = ANY ((ARRAY['Individual'::character varying, 'Corporate'::character varying])::text[]))),
	CONSTRAINT customers_email_key UNIQUE (email),
	CONSTRAINT customers_pkey PRIMARY KEY (id),
	CONSTRAINT customers_stripe_customer_id_key UNIQUE (stripe_customer_id),
	CONSTRAINT valid_customer_role CHECK (((role)::text = ANY ((ARRAY['customer'::character varying, 'worker'::character varying, 'admin'::character varying, 'mainadmin'::character varying])::text[]))),
	CONSTRAINT fk_customers_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
);
CREATE INDEX idx_customers_auth_provider ON public.customers USING btree (auth_provider) WHERE (auth_provider IS NOT NULL);
CREATE INDEX idx_customers_company_id ON public.customers USING btree (company_id);
CREATE INDEX idx_customers_customer_type ON public.customers USING btree (customer_type);
CREATE INDEX idx_customers_google_id ON public.customers USING btree (google_id) WHERE (google_id IS NOT NULL);
CREATE INDEX idx_customers_is_active ON public.customers USING btree (is_active);
CREATE INDEX idx_customers_role ON public.customers USING btree (role);

-- Column comments

COMMENT ON COLUMN public.customers.google_id IS 'Google user ID from OAuth authentication';
COMMENT ON COLUMN public.customers.google_picture IS 'Google profile picture URL';
COMMENT ON COLUMN public.customers.auth_provider IS 'Authentication provider used (e.g., "google", "email")';

-- Table Triggers

create trigger update_customers_updated_at before
update
    on
    public.customers for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.customers OWNER TO alex;
GRANT ALL ON TABLE public.customers TO alex;


-- public.dispute_analytics definition

-- Drop table

-- DROP TABLE public.dispute_analytics;

CREATE TABLE public.dispute_analytics (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NULL,
	period_start date NOT NULL,
	period_end date NOT NULL,
	total_disputes int4 DEFAULT 0 NULL,
	disputes_won int4 DEFAULT 0 NULL,
	disputes_lost int4 DEFAULT 0 NULL,
	disputes_pending int4 DEFAULT 0 NULL,
	total_disputed_amount numeric(10, 2) DEFAULT 0 NULL,
	total_lost_amount numeric(10, 2) DEFAULT 0 NULL,
	avg_resolution_days numeric(5, 2) NULL,
	created_at timestamp DEFAULT now() NULL,
	CONSTRAINT dispute_analytics_company_id_period_start_period_end_key UNIQUE (company_id, period_start, period_end),
	CONSTRAINT dispute_analytics_pkey PRIMARY KEY (id),
	CONSTRAINT dispute_analytics_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id)
);

-- Permissions

ALTER TABLE public.dispute_analytics OWNER TO alex;
GRANT ALL ON TABLE public.dispute_analytics TO alex;


-- public.external_companies definition

-- Drop table

-- DROP TABLE public.external_companies;

CREATE TABLE public.external_companies (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_name varchar(255) NOT NULL,
	api_base_url varchar(500) NULL,
	api_key_name varchar(100) NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	api_username varchar(255) NULL,
	api_password varchar(255) NULL,
	api_token text NULL,
	token_expires_at timestamptz NULL,
	rental_company_id uuid NULL,
	CONSTRAINT external_companies_pkey PRIMARY KEY (id),
	CONSTRAINT uq_external_companies_name UNIQUE (company_name),
	CONSTRAINT external_companies_rental_company_id_fkey FOREIGN KEY (rental_company_id) REFERENCES public.companies(id) ON DELETE SET NULL
);

-- Table Triggers

create trigger tr_external_companies_updated before
update
    on
    public.external_companies for each row execute function update_external_updated_at();

-- Permissions

ALTER TABLE public.external_companies OWNER TO alex;
GRANT ALL ON TABLE public.external_companies TO alex;


-- public.external_company_vehicles definition

-- Drop table

-- DROP TABLE public.external_company_vehicles;

CREATE TABLE public.external_company_vehicles (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	external_company_id uuid NOT NULL,
	external_id varchar(100) NOT NULL,
	"name" varchar(255) NULL,
	vin varchar(17) NULL,
	license_plate varchar(50) NULL,
	make varchar(100) NULL,
	model varchar(100) NULL,
	"year" int4 NULL,
	color varchar(50) NULL,
	notes text NULL,
	raw_data jsonb NULL,
	is_active bool DEFAULT true NOT NULL,
	last_synced_at timestamptz NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT external_company_vehicles_pkey PRIMARY KEY (id),
	CONSTRAINT uq_external_company_vehicles_ext_id UNIQUE (external_company_id, external_id),
	CONSTRAINT external_company_vehicles_external_company_id_fkey FOREIGN KEY (external_company_id) REFERENCES public.external_companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_external_company_vehicles_company ON public.external_company_vehicles USING btree (external_company_id);
CREATE INDEX idx_external_company_vehicles_external_id ON public.external_company_vehicles USING btree (external_id);

-- Table Triggers

create trigger tr_external_company_vehicles_updated before
update
    on
    public.external_company_vehicles for each row execute function update_external_updated_at();

-- Permissions

ALTER TABLE public.external_company_vehicles OWNER TO alex;
GRANT ALL ON TABLE public.external_company_vehicles TO alex;


-- public.finders_list definition

-- Drop table

-- DROP TABLE public.finders_list;

CREATE TABLE public.finders_list (
	id uuid DEFAULT gen_random_uuid() NOT NULL, -- Unique finders list identifier (UUID)
	company_id uuid NOT NULL, -- Foreign key reference to companies table
	finders_list jsonb DEFAULT '[]'::jsonb NOT NULL, -- JSON array of state/province codes enabled for violation finders
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	CONSTRAINT finders_list_company_id_key UNIQUE (company_id),
	CONSTRAINT finders_list_pkey PRIMARY KEY (id),
	CONSTRAINT fk_finders_list_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_finders_list_company_id ON public.finders_list USING btree (company_id);
COMMENT ON TABLE public.finders_list IS 'Stores violation finder configurations (enabled states/provinces) for each company';

-- Column comments

COMMENT ON COLUMN public.finders_list.id IS 'Unique finders list identifier (UUID)';
COMMENT ON COLUMN public.finders_list.company_id IS 'Foreign key reference to companies table';
COMMENT ON COLUMN public.finders_list.finders_list IS 'JSON array of state/province codes enabled for violation finders';

-- Table Triggers

create trigger trigger_update_finders_list_updated_at before
update
    on
    public.finders_list for each row execute function update_finders_list_updated_at();

-- Permissions

ALTER TABLE public.finders_list OWNER TO alex;
GRANT ALL ON TABLE public.finders_list TO alex;


-- public.geofences definition

-- Drop table

-- DROP TABLE public.geofences;

CREATE TABLE public.geofences (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	"name" varchar(100) NOT NULL,
	description text NULL,
	geofence_type varchar(20) NOT NULL,
	center_latitude numeric(10, 7) NULL,
	center_longitude numeric(10, 7) NULL,
	radius_meters int4 NULL,
	polygon_coordinates jsonb NULL,
	alert_on_enter bool DEFAULT true NULL,
	alert_on_exit bool DEFAULT true NULL,
	alert_on_speeding bool DEFAULT false NULL,
	speed_limit_kmh int2 NULL,
	active_days _int2 NULL,
	active_start_time time NULL,
	active_end_time time NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT chk_geofence_type CHECK (((geofence_type)::text = ANY ((ARRAY['polygon'::character varying, 'circle'::character varying])::text[]))),
	CONSTRAINT geofences_pkey PRIMARY KEY (id),
	CONSTRAINT fk_geofences_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_geofences_active ON public.geofences USING btree (is_active) WHERE (is_active = true);
CREATE INDEX idx_geofences_company ON public.geofences USING btree (company_id);
COMMENT ON TABLE public.geofences IS 'Geographic boundaries for vehicle monitoring alerts';

-- Table Triggers

create trigger update_geofences_updated_at before
update
    on
    public.geofences for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.geofences OWNER TO alex;
GRANT ALL ON TABLE public.geofences TO alex;


-- public.license_scans definition

-- Drop table

-- DROP TABLE public.license_scans;

CREATE TABLE public.license_scans (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	customer_id uuid NOT NULL,
	customer_license_id uuid NULL,
	scan_date timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	scanned_by uuid NULL,
	scan_source varchar(50) DEFAULT 'mobile_app'::character varying NULL,
	device_id varchar(100) NULL,
	device_type varchar(50) NULL,
	app_version varchar(20) NULL,
	scan_quality varchar(20) NULL,
	all_fields_captured bool DEFAULT true NULL,
	captured_data jsonb NULL, -- Full JSON snapshot of data at scan time
	barcode_data text NULL,
	age_at_scan int4 NULL,
	was_expired bool DEFAULT false NULL,
	days_until_expiration int4 NULL,
	validation_passed bool DEFAULT true NULL,
	validation_errors _text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT license_scans_pkey PRIMARY KEY (id),
	CONSTRAINT fk_scan_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id) ON DELETE CASCADE
);
CREATE INDEX idx_license_scans_customer_id ON public.license_scans USING btree (customer_id);
CREATE INDEX idx_license_scans_license_id ON public.license_scans USING btree (customer_license_id);
CREATE INDEX idx_license_scans_scan_date ON public.license_scans USING btree (scan_date DESC);
COMMENT ON TABLE public.license_scans IS 'Audit trail of license scans - personal records not tied to companies';

-- Column comments

COMMENT ON COLUMN public.license_scans.captured_data IS 'Full JSON snapshot of data at scan time';

-- Permissions

ALTER TABLE public.license_scans OWNER TO alex;
GRANT ALL ON TABLE public.license_scans TO alex;


-- public.locations definition

-- Drop table

-- DROP TABLE public.locations;

CREATE TABLE public.locations (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NULL,
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
	CONSTRAINT locations_pkey PRIMARY KEY (id),
	CONSTRAINT fk_locations_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
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


-- public.models definition

-- Drop table

-- DROP TABLE public.models;

CREATE TABLE public.models (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	make varchar(100) NOT NULL,
	model varchar(100) NOT NULL,
	"year" int4 NOT NULL,
	fuel_type varchar(50) NULL,
	transmission varchar(50) NULL,
	seats int4 NULL,
	features _text NULL,
	description text NULL,
	category_id uuid NULL,
	CONSTRAINT chk_models_make_uppercase CHECK (((make)::text = upper((make)::text))),
	CONSTRAINT chk_models_model_uppercase CHECK (((model)::text = upper((model)::text))),
	CONSTRAINT models_pkey PRIMARY KEY (id),
	CONSTRAINT fk_models_category FOREIGN KEY (category_id) REFERENCES public.vehicle_categories(id) ON DELETE SET NULL
);
CREATE INDEX idx_models_category_id ON public.models USING btree (category_id);
CREATE INDEX idx_models_make ON public.models USING btree (make);
CREATE INDEX idx_models_make_model ON public.models USING btree (make, model);
CREATE INDEX idx_models_year ON public.models USING btree (year);

-- Table Triggers

create trigger trigger_uppercase_models before
insert
    or
update
    on
    public.models for each row execute function ensure_uppercase_models();

-- Permissions

ALTER TABLE public.models OWNER TO alex;
GRANT ALL ON TABLE public.models TO alex;


-- public.refund_analytics definition

-- Drop table

-- DROP TABLE public.refund_analytics;

CREATE TABLE public.refund_analytics (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NULL,
	period_start date NOT NULL,
	period_end date NOT NULL,
	total_refunds int4 DEFAULT 0 NULL,
	total_refund_amount numeric(10, 2) DEFAULT 0 NULL,
	security_deposit_refunds int4 DEFAULT 0 NULL,
	security_deposit_refund_amount numeric(10, 2) DEFAULT 0 NULL,
	rental_adjustment_refunds int4 DEFAULT 0 NULL,
	rental_adjustment_amount numeric(10, 2) DEFAULT 0 NULL,
	cancellation_refunds int4 DEFAULT 0 NULL,
	cancellation_refund_amount numeric(10, 2) DEFAULT 0 NULL,
	created_at timestamp DEFAULT now() NULL,
	CONSTRAINT refund_analytics_company_id_period_start_period_end_key UNIQUE (company_id, period_start, period_end),
	CONSTRAINT refund_analytics_pkey PRIMARY KEY (id),
	CONSTRAINT refund_analytics_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id)
);

-- Permissions

ALTER TABLE public.refund_analytics OWNER TO alex;
GRANT ALL ON TABLE public.refund_analytics TO alex;


-- public.social_post_templates definition

-- Drop table

-- DROP TABLE public.social_post_templates;

CREATE TABLE public.social_post_templates (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	"name" varchar(100) NOT NULL,
	description varchar(500) NULL,
	caption_template text NOT NULL,
	hashtags _text NULL,
	call_to_action text NULL,
	include_price bool DEFAULT true NOT NULL,
	applicable_categories _text NULL,
	is_default bool DEFAULT false NOT NULL,
	is_active bool DEFAULT true NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT social_post_templates_pkey PRIMARY KEY (id),
	CONSTRAINT social_post_templates_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_social_post_templates_active ON public.social_post_templates USING btree (company_id, is_active);
CREATE INDEX idx_social_post_templates_company_id ON public.social_post_templates USING btree (company_id);
COMMENT ON TABLE public.social_post_templates IS 'Reusable templates for post captions';

-- Table Triggers

create trigger update_social_post_templates_updated_at before
update
    on
    public.social_post_templates for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.social_post_templates OWNER TO alex;
GRANT ALL ON TABLE public.social_post_templates TO alex;


-- public.stripe_account_capabilities definition

-- Drop table

-- DROP TABLE public.stripe_account_capabilities;

CREATE TABLE public.stripe_account_capabilities (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	capability_name varchar(100) NOT NULL, -- card_payments, transfers, etc
	status varchar(50) NOT NULL, -- active, inactive, pending, unrequested
	requested_at timestamptz NULL,
	requirements jsonb NULL, -- JSON of requirements for this capability
	created_at timestamptz DEFAULT now() NULL,
	updated_at timestamptz DEFAULT now() NULL,
	CONSTRAINT stripe_account_capabilities_pkey PRIMARY KEY (id),
	CONSTRAINT unique_company_capability UNIQUE (company_id, capability_name),
	CONSTRAINT fk_capabilities_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_capabilities_company ON public.stripe_account_capabilities USING btree (company_id);
CREATE INDEX idx_capabilities_status ON public.stripe_account_capabilities USING btree (status);
COMMENT ON TABLE public.stripe_account_capabilities IS 'Tracks Stripe account capabilities (card_payments, transfers, etc)';

-- Column comments

COMMENT ON COLUMN public.stripe_account_capabilities.capability_name IS 'card_payments, transfers, etc';
COMMENT ON COLUMN public.stripe_account_capabilities.status IS 'active, inactive, pending, unrequested';
COMMENT ON COLUMN public.stripe_account_capabilities.requirements IS 'JSON of requirements for this capability';

-- Table Triggers

create trigger trigger_capabilities_updated_at before
update
    on
    public.stripe_account_capabilities for each row execute function update_capabilities_updated_at();

-- Permissions

ALTER TABLE public.stripe_account_capabilities OWNER TO alex;
GRANT ALL ON TABLE public.stripe_account_capabilities TO alex;


-- public.stripe_balance_transactions definition

-- Drop table

-- DROP TABLE public.stripe_balance_transactions;

CREATE TABLE public.stripe_balance_transactions (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	stripe_balance_transaction_id varchar(255) NOT NULL,
	amount numeric(10, 2) NOT NULL,
	currency varchar(3) DEFAULT 'USD'::character varying NULL,
	net numeric(10, 2) NOT NULL, -- Net amount after fees
	fee numeric(10, 2) DEFAULT 0 NULL, -- Stripe fees
	transaction_type varchar(50) NOT NULL, -- charge, refund, transfer, payout, etc
	description text NULL,
	source_id varchar(255) NULL,
	source_type varchar(50) NULL,
	available_on date NULL, -- Date funds become available
	created timestamptz NOT NULL, -- Original Stripe creation timestamp
	created_at timestamptz DEFAULT now() NULL,
	CONSTRAINT stripe_balance_transactions_pkey PRIMARY KEY (id),
	CONSTRAINT stripe_balance_transactions_stripe_balance_transaction_id_key UNIQUE (stripe_balance_transaction_id),
	CONSTRAINT fk_balance_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
);
CREATE INDEX idx_balance_available ON public.stripe_balance_transactions USING btree (available_on);
CREATE INDEX idx_balance_company ON public.stripe_balance_transactions USING btree (company_id);
CREATE INDEX idx_balance_source ON public.stripe_balance_transactions USING btree (source_id, source_type);
CREATE INDEX idx_balance_stripe_id ON public.stripe_balance_transactions USING btree (stripe_balance_transaction_id);
CREATE INDEX idx_balance_type ON public.stripe_balance_transactions USING btree (transaction_type);
COMMENT ON TABLE public.stripe_balance_transactions IS 'Balance transactions from Stripe connected accounts';

-- Column comments

COMMENT ON COLUMN public.stripe_balance_transactions.net IS 'Net amount after fees';
COMMENT ON COLUMN public.stripe_balance_transactions.fee IS 'Stripe fees';
COMMENT ON COLUMN public.stripe_balance_transactions.transaction_type IS 'charge, refund, transfer, payout, etc';
COMMENT ON COLUMN public.stripe_balance_transactions.available_on IS 'Date funds become available';
COMMENT ON COLUMN public.stripe_balance_transactions.created IS 'Original Stripe creation timestamp';

-- Permissions

ALTER TABLE public.stripe_balance_transactions OWNER TO alex;
GRANT ALL ON TABLE public.stripe_balance_transactions TO alex;


-- public.stripe_company definition

-- Drop table

-- DROP TABLE public.stripe_company;

CREATE TABLE public.stripe_company (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	settings_id uuid NOT NULL, -- Reference to stripe_settings table
	company_id uuid NOT NULL, -- Reference to companies table
	stripe_account_id varchar(255) NULL, -- Stripe account ID for the company
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT stripe_company_company_unique UNIQUE (company_id),
	CONSTRAINT stripe_company_pkey PRIMARY KEY (id),
	CONSTRAINT fk_stripe_company_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE,
	CONSTRAINT fk_stripe_company_settings FOREIGN KEY (settings_id) REFERENCES public.stripe_settings(id) ON DELETE CASCADE
);
CREATE INDEX idx_stripe_company_company ON public.stripe_company USING btree (company_id);
CREATE INDEX idx_stripe_company_settings ON public.stripe_company USING btree (settings_id);
COMMENT ON TABLE public.stripe_company IS 'Junction table linking companies to their Stripe settings';

-- Column comments

COMMENT ON COLUMN public.stripe_company.settings_id IS 'Reference to stripe_settings table';
COMMENT ON COLUMN public.stripe_company.company_id IS 'Reference to companies table';
COMMENT ON COLUMN public.stripe_company.stripe_account_id IS 'Stripe account ID for the company';

-- Table Triggers

create trigger update_stripe_company_updated_at before
update
    on
    public.stripe_company for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.stripe_company OWNER TO alex;
GRANT ALL ON TABLE public.stripe_company TO alex;


-- public.stripe_onboarding_sessions definition

-- Drop table

-- DROP TABLE public.stripe_onboarding_sessions;

CREATE TABLE public.stripe_onboarding_sessions (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	account_link_url text NOT NULL, -- Stripe-hosted onboarding URL
	return_url text NOT NULL, -- URL to redirect after successful onboarding
	refresh_url text NOT NULL, -- URL to redirect if link expires
	expires_at timestamptz NOT NULL, -- When this onboarding link expires
	completed bool DEFAULT false NULL, -- Whether onboarding was completed
	completed_at timestamptz NULL,
	created_at timestamptz DEFAULT now() NULL,
	updated_at timestamptz DEFAULT now() NULL,
	CONSTRAINT stripe_onboarding_sessions_pkey PRIMARY KEY (id),
	CONSTRAINT fk_onboarding_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_onboarding_company ON public.stripe_onboarding_sessions USING btree (company_id);
CREATE INDEX idx_onboarding_completed ON public.stripe_onboarding_sessions USING btree (completed);
CREATE INDEX idx_onboarding_expires ON public.stripe_onboarding_sessions USING btree (expires_at);
COMMENT ON TABLE public.stripe_onboarding_sessions IS 'Tracks Stripe Connect onboarding sessions for companies';

-- Column comments

COMMENT ON COLUMN public.stripe_onboarding_sessions.account_link_url IS 'Stripe-hosted onboarding URL';
COMMENT ON COLUMN public.stripe_onboarding_sessions.return_url IS 'URL to redirect after successful onboarding';
COMMENT ON COLUMN public.stripe_onboarding_sessions.refresh_url IS 'URL to redirect if link expires';
COMMENT ON COLUMN public.stripe_onboarding_sessions.expires_at IS 'When this onboarding link expires';
COMMENT ON COLUMN public.stripe_onboarding_sessions.completed IS 'Whether onboarding was completed';

-- Table Triggers

create trigger trigger_onboarding_sessions_updated_at before
update
    on
    public.stripe_onboarding_sessions for each row execute function update_onboarding_sessions_updated_at();

-- Permissions

ALTER TABLE public.stripe_onboarding_sessions OWNER TO alex;
GRANT ALL ON TABLE public.stripe_onboarding_sessions TO alex;


-- public.stripe_payout_records definition

-- Drop table

-- DROP TABLE public.stripe_payout_records;

CREATE TABLE public.stripe_payout_records (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	stripe_payout_id varchar(255) NOT NULL, -- Stripe Payout ID (po_xxx)
	amount numeric(10, 2) NOT NULL,
	currency varchar(3) DEFAULT 'USD'::character varying NULL,
	status varchar(50) NOT NULL, -- pending, paid, failed, canceled, in_transit
	payout_type varchar(50) DEFAULT 'bank_account'::character varying NULL, -- bank_account or card
	arrival_date date NULL, -- Expected arrival date to bank account
	description text NULL,
	"method" varchar(50) NULL, -- standard or instant
	failure_code varchar(100) NULL,
	failure_message text NULL,
	statement_descriptor varchar(255) NULL,
	metadata jsonb NULL,
	created_at timestamptz DEFAULT now() NULL,
	updated_at timestamptz DEFAULT now() NULL,
	CONSTRAINT stripe_payout_records_pkey PRIMARY KEY (id),
	CONSTRAINT stripe_payout_records_stripe_payout_id_key UNIQUE (stripe_payout_id),
	CONSTRAINT fk_payouts_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
);
CREATE INDEX idx_payouts_arrival ON public.stripe_payout_records USING btree (arrival_date);
CREATE INDEX idx_payouts_company ON public.stripe_payout_records USING btree (company_id);
CREATE INDEX idx_payouts_created ON public.stripe_payout_records USING btree (created_at DESC);
CREATE INDEX idx_payouts_status ON public.stripe_payout_records USING btree (status);
CREATE INDEX idx_payouts_stripe_id ON public.stripe_payout_records USING btree (stripe_payout_id);
COMMENT ON TABLE public.stripe_payout_records IS 'Stripe payouts from connected accounts to bank accounts';

-- Column comments

COMMENT ON COLUMN public.stripe_payout_records.stripe_payout_id IS 'Stripe Payout ID (po_xxx)';
COMMENT ON COLUMN public.stripe_payout_records.status IS 'pending, paid, failed, canceled, in_transit';
COMMENT ON COLUMN public.stripe_payout_records.payout_type IS 'bank_account or card';
COMMENT ON COLUMN public.stripe_payout_records.arrival_date IS 'Expected arrival date to bank account';
COMMENT ON COLUMN public.stripe_payout_records."method" IS 'standard or instant';

-- Table Triggers

create trigger trigger_payouts_updated_at before
update
    on
    public.stripe_payout_records for each row execute function update_payouts_updated_at();

-- Permissions

ALTER TABLE public.stripe_payout_records OWNER TO alex;
GRANT ALL ON TABLE public.stripe_payout_records TO alex;


-- public.vehicle_model definition

-- Drop table

-- DROP TABLE public.vehicle_model;

CREATE TABLE public.vehicle_model (
	id uuid DEFAULT uuid_generate_v4() NOT NULL, -- Primary key - UUID
	model_id uuid NOT NULL, -- Reference to the model
	daily_rate numeric(10, 2) NULL, -- Daily rate for this vehicle-model combination
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL, -- When the link was created
	company_id uuid NOT NULL,
	CONSTRAINT pk_vehicle_model PRIMARY KEY (id),
	CONSTRAINT fk_vehicle_model_model FOREIGN KEY (model_id) REFERENCES public.models(id) ON DELETE CASCADE
);
CREATE INDEX idx_vehicle_model_model_id ON public.vehicle_model USING btree (model_id);
COMMENT ON TABLE public.vehicle_model IS 'Junction table linking vehicles to their models with UUID primary key';

-- Column comments

COMMENT ON COLUMN public.vehicle_model.id IS 'Primary key - UUID';
COMMENT ON COLUMN public.vehicle_model.model_id IS 'Reference to the model';
COMMENT ON COLUMN public.vehicle_model.daily_rate IS 'Daily rate for this vehicle-model combination';
COMMENT ON COLUMN public.vehicle_model.created_at IS 'When the link was created';

-- Permissions

ALTER TABLE public.vehicle_model OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_model TO alex;


-- public.vehicles definition

-- Drop table

-- DROP TABLE public.vehicles;

CREATE TABLE public.vehicles (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	company_id uuid NOT NULL,
	color varchar(50) NULL,
	license_plate varchar(50) NOT NULL,
	vin varchar(17) NULL,
	mileage int4 DEFAULT 0 NULL,
	transmission varchar(50) NULL,
	seats int4 NULL,
	status varchar(50) DEFAULT 'available'::character varying NULL,
	state varchar(2) NULL,
	"location" varchar(255) NULL,
	image_url text NULL,
	features _text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	tag varchar(50) NULL,
	location_id uuid NULL, -- Assigned location from company_location table
	current_location_id uuid NULL, -- Current physical location of the vehicle (references locations table, can be null)
	vehicle_model_id uuid NULL,
	CONSTRAINT chk_vehicle_status CHECK (((status)::text = ANY ((ARRAY['Available'::character varying, 'Rented'::character varying, 'Maintenance'::character varying, 'OutOfService'::character varying, 'Cleaning'::character varying])::text[]))),
	CONSTRAINT vehicles_license_plate_key UNIQUE (license_plate),
	CONSTRAINT vehicles_pkey PRIMARY KEY (id),
	CONSTRAINT vehicles_vin_key UNIQUE (vin),
	CONSTRAINT fk_vehicles_company FOREIGN KEY (company_id) REFERENCES public.companies(id),
	CONSTRAINT fk_vehicles_company_location FOREIGN KEY (location_id) REFERENCES public.company_location(id) ON DELETE SET NULL,
	CONSTRAINT fk_vehicles_current_location FOREIGN KEY (current_location_id) REFERENCES public.locations(id) ON DELETE SET NULL,
	CONSTRAINT fk_vehicles_vehicle_model FOREIGN KEY (vehicle_model_id) REFERENCES public.vehicle_model(id) ON DELETE SET NULL,
	CONSTRAINT vehicles_location_id_fkey FOREIGN KEY (location_id) REFERENCES public.company_location(id) ON DELETE SET NULL
);
CREATE INDEX idx_vehicles_company ON public.vehicles USING btree (company_id);
CREATE INDEX idx_vehicles_current_location_id ON public.vehicles USING btree (current_location_id);
CREATE INDEX idx_vehicles_location ON public.vehicles USING btree (location_id);
CREATE INDEX idx_vehicles_location_id ON public.vehicles USING btree (location_id);
CREATE INDEX idx_vehicles_status ON public.vehicles USING btree (status);
CREATE INDEX idx_vehicles_vehicle_model_id ON public.vehicles USING btree (vehicle_model_id);
COMMENT ON TABLE public.vehicles IS 'Vehicles table with fuel_type and category_id removed';

-- Column comments

COMMENT ON COLUMN public.vehicles.location_id IS 'Assigned location from company_location table';
COMMENT ON COLUMN public.vehicles.current_location_id IS 'Current physical location of the vehicle (references locations table, can be null)';

-- Table Triggers

create trigger update_vehicles_updated_at before
update
    on
    public.vehicles for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.vehicles OWNER TO alex;
GRANT ALL ON TABLE public.vehicles TO alex;


-- public.violations definition

-- Drop table

-- DROP TABLE public.violations;

CREATE TABLE public.violations (
	id uuid DEFAULT uuid_generate_v4() NOT NULL, -- Unique violation identifier (UUID)
	company_id uuid NOT NULL, -- Foreign key reference to companies table
	citation_number varchar(255) NULL, -- Citation number from the issuing agency
	notice_number varchar(255) NULL, -- Notice number from the issuing agency
	provider int4 DEFAULT 0 NOT NULL, -- Provider identifier (integer)
	agency varchar(255) NULL, -- Agency that issued the violation
	address text NULL, -- Address where the violation occurred
	tag varchar(50) NULL, -- License plate number
	state varchar(10) NULL, -- State code where violation occurred
	issue_date timestamp NULL, -- Date when the violation was issued
	start_date timestamp NULL, -- Start date for the violation period
	end_date timestamp NULL, -- End date for the violation period
	amount numeric(10, 2) DEFAULT 0.00 NOT NULL, -- Fine amount
	currency varchar(3) NULL, -- Currency code (ISO 4217)
	payment_status int4 DEFAULT 0 NOT NULL, -- Payment status (integer code)
	fine_type int4 DEFAULT 0 NOT NULL, -- Type of fine (integer code)
	note text NULL, -- Additional notes about the violation
	link text NULL, -- Link to violation details or payment page
	is_active bool DEFAULT true NOT NULL, -- Whether the violation record is active
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	CONSTRAINT violations_pkey PRIMARY KEY (id),
	CONSTRAINT fk_violations_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE
);
CREATE INDEX idx_violations_citation_number ON public.violations USING btree (citation_number);
CREATE INDEX idx_violations_company_id ON public.violations USING btree (company_id);
CREATE INDEX idx_violations_company_state ON public.violations USING btree (company_id, state);
CREATE INDEX idx_violations_company_tag ON public.violations USING btree (company_id, tag);
CREATE INDEX idx_violations_created_at ON public.violations USING btree (created_at DESC);
CREATE INDEX idx_violations_is_active ON public.violations USING btree (is_active);
CREATE INDEX idx_violations_issue_date ON public.violations USING btree (issue_date);
CREATE INDEX idx_violations_notice_number ON public.violations USING btree (notice_number);
CREATE INDEX idx_violations_state ON public.violations USING btree (state);
CREATE INDEX idx_violations_tag ON public.violations USING btree (tag);
COMMENT ON TABLE public.violations IS 'Parking violations table linked to companies';

-- Column comments

COMMENT ON COLUMN public.violations.id IS 'Unique violation identifier (UUID)';
COMMENT ON COLUMN public.violations.company_id IS 'Foreign key reference to companies table';
COMMENT ON COLUMN public.violations.citation_number IS 'Citation number from the issuing agency';
COMMENT ON COLUMN public.violations.notice_number IS 'Notice number from the issuing agency';
COMMENT ON COLUMN public.violations.provider IS 'Provider identifier (integer)';
COMMENT ON COLUMN public.violations.agency IS 'Agency that issued the violation';
COMMENT ON COLUMN public.violations.address IS 'Address where the violation occurred';
COMMENT ON COLUMN public.violations.tag IS 'License plate number';
COMMENT ON COLUMN public.violations.state IS 'State code where violation occurred';
COMMENT ON COLUMN public.violations.issue_date IS 'Date when the violation was issued';
COMMENT ON COLUMN public.violations.start_date IS 'Start date for the violation period';
COMMENT ON COLUMN public.violations.end_date IS 'End date for the violation period';
COMMENT ON COLUMN public.violations.amount IS 'Fine amount';
COMMENT ON COLUMN public.violations.currency IS 'Currency code (ISO 4217)';
COMMENT ON COLUMN public.violations.payment_status IS 'Payment status (integer code)';
COMMENT ON COLUMN public.violations.fine_type IS 'Type of fine (integer code)';
COMMENT ON COLUMN public.violations.note IS 'Additional notes about the violation';
COMMENT ON COLUMN public.violations.link IS 'Link to violation details or payment page';
COMMENT ON COLUMN public.violations.is_active IS 'Whether the violation record is active';

-- Table Triggers

create trigger update_violations_updated_at before
update
    on
    public.violations for each row execute function update_violations_updated_at();

-- Permissions

ALTER TABLE public.violations OWNER TO alex;
GRANT ALL ON TABLE public.violations TO alex;


-- public.violations_requests definition

-- Drop table

-- DROP TABLE public.violations_requests;

CREATE TABLE public.violations_requests (
	id uuid DEFAULT uuid_generate_v4() NOT NULL, -- Unique identifier for the request record.
	company_id uuid NULL, -- Foreign key to the companies table, indicating which company this request was for (null if request was for specific vehicles).
	vehicle_count int4 DEFAULT 0 NOT NULL, -- Number of vehicles processed in this request.
	requests_count int4 DEFAULT 0 NOT NULL, -- Number of individual violation search requests made.
	finders_count int4 DEFAULT 0 NOT NULL, -- Number of finders used in this request.
	request_datetime timestamptz DEFAULT CURRENT_TIMESTAMP NOT NULL, -- Timestamp when the request was made.
	violations_found int4 DEFAULT 0 NOT NULL, -- Total number of violations found in this request.
	requestor varchar(255) NULL, -- Identifier of who made the request (e.g., user email, API key, system).
	CONSTRAINT violations_requests_pkey PRIMARY KEY (id),
	CONSTRAINT fk_violations_requests_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE SET NULL
);
CREATE INDEX idx_violations_requests_company_id ON public.violations_requests USING btree (company_id);
CREATE INDEX idx_violations_requests_request_datetime ON public.violations_requests USING btree (request_datetime DESC);
COMMENT ON TABLE public.violations_requests IS 'Tracks violation search requests made through the API';

-- Column comments

COMMENT ON COLUMN public.violations_requests.id IS 'Unique identifier for the request record.';
COMMENT ON COLUMN public.violations_requests.company_id IS 'Foreign key to the companies table, indicating which company this request was for (null if request was for specific vehicles).';
COMMENT ON COLUMN public.violations_requests.vehicle_count IS 'Number of vehicles processed in this request.';
COMMENT ON COLUMN public.violations_requests.requests_count IS 'Number of individual violation search requests made.';
COMMENT ON COLUMN public.violations_requests.finders_count IS 'Number of finders used in this request.';
COMMENT ON COLUMN public.violations_requests.request_datetime IS 'Timestamp when the request was made.';
COMMENT ON COLUMN public.violations_requests.violations_found IS 'Total number of violations found in this request.';
COMMENT ON COLUMN public.violations_requests.requestor IS 'Identifier of who made the request (e.g., user email, API key, system).';

-- Permissions

ALTER TABLE public.violations_requests OWNER TO alex;
GRANT ALL ON TABLE public.violations_requests TO alex;


-- public.webhook_events definition

-- Drop table

-- DROP TABLE public.webhook_events;

CREATE TABLE public.webhook_events (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	stripe_event_id varchar(255) NOT NULL,
	event_type varchar(100) NOT NULL,
	connected_account_id varchar(255) NULL,
	booking_id uuid NULL,
	payload jsonb NULL,
	processed bool DEFAULT false NULL,
	processed_at timestamp NULL,
	error_message text NULL,
	retry_count int4 DEFAULT 0 NULL,
	created_at timestamp DEFAULT now() NULL,
	company_id uuid NULL, -- Company associated with this webhook event
	processed_by varchar(100) NULL, -- Service/handler that processed this event
	next_retry_at timestamptz NULL, -- Scheduled time for next retry attempt
	CONSTRAINT webhook_events_pkey PRIMARY KEY (id),
	CONSTRAINT webhook_events_stripe_event_id_key UNIQUE (stripe_event_id),
	CONSTRAINT fk_webhook_events_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
);
CREATE INDEX idx_webhooks_account ON public.webhook_events USING btree (connected_account_id);
CREATE INDEX idx_webhooks_booking ON public.webhook_events USING btree (booking_id);
CREATE INDEX idx_webhooks_company_id ON public.webhook_events USING btree (company_id);
CREATE INDEX idx_webhooks_next_retry ON public.webhook_events USING btree (next_retry_at) WHERE (processed = false);
CREATE INDEX idx_webhooks_processed ON public.webhook_events USING btree (processed);
CREATE INDEX idx_webhooks_type ON public.webhook_events USING btree (event_type);

-- Column comments

COMMENT ON COLUMN public.webhook_events.company_id IS 'Company associated with this webhook event';
COMMENT ON COLUMN public.webhook_events.processed_by IS 'Service/handler that processed this event';
COMMENT ON COLUMN public.webhook_events.next_retry_at IS 'Scheduled time for next retry attempt';

-- Permissions

ALTER TABLE public.webhook_events OWNER TO alex;
GRANT ALL ON TABLE public.webhook_events TO alex;


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
	CONSTRAINT fk_additional_services_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
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


-- public.auto_insurance_cards definition

-- Drop table

-- DROP TABLE public.auto_insurance_cards;

CREATE TABLE public.auto_insurance_cards (
	id serial4 NOT NULL,
	customer_id uuid NOT NULL,
	insurance_company varchar(255) NULL,
	policy_number varchar(100) NULL,
	named_insured varchar(255) NULL,
	vehicle_make varchar(100) NULL,
	vehicle_model varchar(100) NULL,
	vehicle_year varchar(4) NULL,
	vin varchar(17) NULL,
	effective_date date NULL,
	expiration_date date NOT NULL,
	agent_name varchar(255) NULL,
	agent_phone varchar(20) NULL,
	front_image_url text NOT NULL,
	back_image_url text NULL,
	ocr_raw_text text NULL,
	ocr_confidence numeric(5, 2) NULL,
	ocr_processed_at timestamp NULL,
	is_verified bool DEFAULT false NULL,
	is_expired bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp NULL,
	created_by uuid NULL,
	CONSTRAINT auto_insurance_cards_pkey PRIMARY KEY (id),
	CONSTRAINT fk_insurance_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id) ON DELETE CASCADE
);
CREATE INDEX idx_insurance_customer ON public.auto_insurance_cards USING btree (customer_id);
CREATE INDEX idx_insurance_expiration ON public.auto_insurance_cards USING btree (expiration_date);
CREATE INDEX idx_insurance_is_expired ON public.auto_insurance_cards USING btree (is_expired);
CREATE INDEX idx_insurance_policy ON public.auto_insurance_cards USING btree (policy_number);

-- Table Triggers

create trigger trigger_update_insurance_timestamp before
update
    on
    public.auto_insurance_cards for each row execute function update_insurance_updated_at();
create trigger trigger_update_insurance_expired before
insert
    or
update
    on
    public.auto_insurance_cards for each row execute function update_insurance_expired_status();

-- Permissions

ALTER TABLE public.auto_insurance_cards OWNER TO alex;
GRANT ALL ON TABLE public.auto_insurance_cards TO alex;


-- public.booking_tokens definition

-- Drop table

-- DROP TABLE public.booking_tokens;

CREATE TABLE public.booking_tokens (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
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
	CONSTRAINT booking_tokens_pkey PRIMARY KEY (id),
	CONSTRAINT booking_tokens_token_key UNIQUE (token),
	CONSTRAINT fk_booking_tokens_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
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
	payment_intent_id varchar(255) NULL,
	setup_intent_id varchar(255) NULL,
	payment_method_id varchar(255) NULL,
	security_deposit_amount numeric(10, 2) NULL,
	security_deposit_status varchar(50) DEFAULT 'pending'::character varying NULL,
	security_deposit_charged_amount numeric(10, 2) NULL,
	payment_status varchar(50) DEFAULT 'pending'::character varying NULL,
	stripe_customer_id varchar(255) NULL,
	security_deposit numeric(10, 2) DEFAULT 1000 NOT NULL,
	stripe_transfer_id varchar(255) NULL, -- Stripe Transfer ID to connected account
	platform_fee_amount numeric(10, 2) DEFAULT 0 NULL, -- Platform fee charged for this booking
	net_amount numeric(10, 2) NULL, -- Net amount after platform fees
	security_deposit_payment_intent_id varchar(255) NULL, -- Stripe Payment Intent ID for security deposit
	security_deposit_authorized_at timestamptz NULL, -- When security deposit was authorized
	security_deposit_captured_at timestamptz NULL, -- When security deposit was captured
	security_deposit_released_at timestamptz NULL, -- When security deposit hold was released
	security_deposit_refunded_at timestamptz NULL, -- When captured security deposit was refunded
	security_deposit_capture_reason text NULL, -- Reason for capturing security deposit (damages, fees, etc)
	currency varchar(3) DEFAULT 'USD'::character varying NULL, -- Currency code (ISO 4217) for the booking
	stripe_payment_intent_id varchar(255) NULL, -- Stripe Payment Intent ID for the booking
	pickup_time varchar(5) DEFAULT '10:00'::character varying NULL,
	return_time varchar(5) DEFAULT '22:00'::character varying NULL,
	additional_services_json text NULL, -- JSON array of selected additional services at booking time
	CONSTRAINT bookings_pkey PRIMARY KEY (id),
	CONSTRAINT chk_booking_status CHECK (((status)::text = ANY ((ARRAY['Pending'::character varying, 'Confirmed'::character varying, 'PickedUp'::character varying, 'Returned'::character varying, 'Cancelled'::character varying, 'NoShow'::character varying, 'Active'::character varying, 'Completed'::character varying])::text[]))),
	CONSTRAINT reservations_reservation_number_key UNIQUE (booking_number),
	CONSTRAINT fk_bookings_company FOREIGN KEY (company_id) REFERENCES public.companies(id),
	CONSTRAINT fk_bookings_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id),
	CONSTRAINT fk_bookings_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id)
);
CREATE INDEX idx_bookings_alt_booking_number ON public.bookings USING btree (alt_booking_number);
CREATE INDEX idx_bookings_has_services ON public.bookings USING btree (((additional_services_json IS NOT NULL)));
CREATE INDEX idx_bookings_payment_intent ON public.bookings USING btree (payment_intent_id);
CREATE INDEX idx_bookings_platform_fee ON public.bookings USING btree (platform_fee_amount);
CREATE INDEX idx_bookings_security_deposit_intent ON public.bookings USING btree (security_deposit_payment_intent_id);
CREATE INDEX idx_bookings_setup_intent ON public.bookings USING btree (setup_intent_id);
CREATE INDEX idx_bookings_status ON public.bookings USING btree (status);
CREATE INDEX idx_bookings_stripe_customer ON public.bookings USING btree (stripe_customer_id);
CREATE INDEX idx_bookings_stripe_transfer ON public.bookings USING btree (stripe_transfer_id);
CREATE INDEX idx_reservations_company ON public.bookings USING btree (company_id);
CREATE INDEX idx_reservations_customer ON public.bookings USING btree (customer_id);
CREATE INDEX idx_reservations_dates ON public.bookings USING btree (pickup_date, return_date);
CREATE INDEX idx_reservations_vehicle ON public.bookings USING btree (vehicle_id);
COMMENT ON TABLE public.bookings IS 'Renamed from reservations - stores vehicle booking information';

-- Column comments

COMMENT ON COLUMN public.bookings.alt_booking_number IS 'Alternative booking number for external reference or integration with other systems';
COMMENT ON COLUMN public.bookings.stripe_transfer_id IS 'Stripe Transfer ID to connected account';
COMMENT ON COLUMN public.bookings.platform_fee_amount IS 'Platform fee charged for this booking';
COMMENT ON COLUMN public.bookings.net_amount IS 'Net amount after platform fees';
COMMENT ON COLUMN public.bookings.security_deposit_payment_intent_id IS 'Stripe Payment Intent ID for security deposit';
COMMENT ON COLUMN public.bookings.security_deposit_authorized_at IS 'When security deposit was authorized';
COMMENT ON COLUMN public.bookings.security_deposit_captured_at IS 'When security deposit was captured';
COMMENT ON COLUMN public.bookings.security_deposit_released_at IS 'When security deposit hold was released';
COMMENT ON COLUMN public.bookings.security_deposit_refunded_at IS 'When captured security deposit was refunded';
COMMENT ON COLUMN public.bookings.security_deposit_capture_reason IS 'Reason for capturing security deposit (damages, fees, etc)';
COMMENT ON COLUMN public.bookings.currency IS 'Currency code (ISO 4217) for the booking';
COMMENT ON COLUMN public.bookings.stripe_payment_intent_id IS 'Stripe Payment Intent ID for the booking';
COMMENT ON COLUMN public.bookings.additional_services_json IS 'JSON array of selected additional services at booking time';

-- Table Triggers

create trigger update_reservations_updated_at before
update
    on
    public.bookings for each row execute function update_updated_at_column();
create trigger trigger_calculate_platform_fee before
insert
    or
update
    of total_amount,
    company_id on
    public.bookings for each row execute function calculate_platform_fee();

-- Permissions

ALTER TABLE public.bookings OWNER TO alex;
GRANT ALL ON TABLE public.bookings TO alex;


-- public.company_services definition

-- Drop table

-- DROP TABLE public.company_services;

CREATE TABLE public.company_services (
	company_id uuid NOT NULL, -- Reference to rental company
	additional_service_id uuid NOT NULL, -- Reference to additional service
	is_active bool DEFAULT true NOT NULL, -- Whether this service is currently active for this company
	created_at timestamptz DEFAULT now() NOT NULL, -- When this service was added to the company
	price numeric(10, 2) NULL, -- Custom price for this service at this company (if NULL, uses price from additional_services table)
	is_mandatory bool NULL, -- Whether this service is mandatory for this company (if NULL, uses is_mandatory from additional_services table)
	CONSTRAINT company_services_pkey PRIMARY KEY (company_id, additional_service_id),
	CONSTRAINT fk_company_services_company FOREIGN KEY (company_id) REFERENCES public.companies(id),
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
COMMENT ON COLUMN public.company_services.price IS 'Custom price for this service at this company (if NULL, uses price from additional_services table)';
COMMENT ON COLUMN public.company_services.is_mandatory IS 'Whether this service is mandatory for this company (if NULL, uses is_mandatory from additional_services table)';

-- Permissions

ALTER TABLE public.company_services OWNER TO alex;
GRANT ALL ON TABLE public.company_services TO alex;


-- public.customer_licenses definition

-- Drop table

-- DROP TABLE public.customer_licenses;

CREATE TABLE public.customer_licenses (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	customer_id uuid NOT NULL,
	license_number varchar(50) NOT NULL,
	state_issued varchar(2) NOT NULL,
	country_issued varchar(2) DEFAULT 'US'::character varying NULL,
	first_name varchar(100) NULL,
	last_name varchar(100) NULL,
	sex varchar(1) NULL,
	height varchar(20) NULL,
	eye_color varchar(20) NULL,
	middle_name varchar(100) NULL,
	issue_date date NULL,
	expiration_date date NOT NULL,
	license_address varchar(255) NULL,
	license_city varchar(100) NULL,
	license_state varchar(100) NULL,
	license_postal_code varchar(20) NULL,
	license_country varchar(100) NULL,
	restriction_code varchar(50) NULL,
	endorsements varchar(100) NULL,
	raw_barcode_data text NULL,
	is_verified bool DEFAULT true NULL,
	verification_date timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	verification_method varchar(50) DEFAULT 'license_scan'::character varying NULL,
	notes text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	created_by uuid NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_by uuid NULL,
	front_image_url text NULL, -- Full URL to front license image in Azure Blob Storage
	back_image_url text NULL, -- Full URL to back license image in Azure Blob Storage
	CONSTRAINT customer_licenses_customer_id_key UNIQUE (customer_id),
	CONSTRAINT customer_licenses_pkey PRIMARY KEY (id),
	CONSTRAINT fk_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id) ON DELETE CASCADE
);
CREATE INDEX idx_customer_licenses_customer_id ON public.customer_licenses USING btree (customer_id);
CREATE INDEX idx_customer_licenses_expiration_date ON public.customer_licenses USING btree (expiration_date);
CREATE INDEX idx_customer_licenses_license_number ON public.customer_licenses USING btree (license_number);
CREATE INDEX idx_customer_licenses_state_issued ON public.customer_licenses USING btree (state_issued);
CREATE INDEX idx_customer_licenses_names ON public.customer_licenses USING btree (last_name, first_name);

-- Column comments

COMMENT ON COLUMN public.customer_licenses.first_name IS 'First name from driver license scan';
COMMENT ON COLUMN public.customer_licenses.last_name IS 'Last name from driver license scan';
COMMENT ON COLUMN public.customer_licenses.middle_name IS 'Middle name from driver license scan';
COMMENT ON COLUMN public.customer_licenses.front_image_url IS 'Full URL to front license image in Azure Blob Storage';
COMMENT ON COLUMN public.customer_licenses.back_image_url IS 'Full URL to back license image in Azure Blob Storage';

-- Table Triggers

create trigger trigger_customer_licenses_updated_at before
update
    on
    public.customer_licenses for each row execute function update_customer_licenses_updated_at();

-- Permissions

ALTER TABLE public.customer_licenses OWNER TO alex;
GRANT ALL ON TABLE public.customer_licenses TO alex;


-- public.customer_payment_methods definition

-- Drop table

-- DROP TABLE public.customer_payment_methods;

CREATE TABLE public.customer_payment_methods (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	customer_id uuid NOT NULL,
	stripe_payment_method_id varchar(255) NOT NULL,
	card_brand varchar(50) NULL,
	card_last4 varchar(4) NULL,
	card_exp_month int4 NULL,
	card_exp_year int4 NULL,
	is_default bool DEFAULT false NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT customer_payment_methods_pkey PRIMARY KEY (id),
	CONSTRAINT customer_payment_methods_stripe_payment_method_id_key UNIQUE (stripe_payment_method_id),
	CONSTRAINT fk_customer_payment_methods_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id)
);
CREATE INDEX idx_customer_payment_methods_customer ON public.customer_payment_methods USING btree (customer_id);

-- Permissions

ALTER TABLE public.customer_payment_methods OWNER TO alex;
GRANT ALL ON TABLE public.customer_payment_methods TO alex;


-- public.dispute_records definition

-- Drop table

-- DROP TABLE public.dispute_records;

CREATE TABLE public.dispute_records (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	stripe_dispute_id varchar(255) NOT NULL,
	booking_id uuid NULL,
	charge_id varchar(255) NOT NULL,
	amount numeric(10, 2) NOT NULL,
	reason varchar(100) NULL,
	status varchar(50) NULL,
	evidence_due_by timestamp NULL,
	evidence_submitted bool DEFAULT false NULL,
	evidence_submitted_at timestamp NULL,
	evidence_details jsonb NULL,
	is_security_deposit_dispute bool DEFAULT false NULL,
	outcome varchar(50) NULL,
	created_at timestamp DEFAULT now() NULL,
	updated_at timestamp NULL,
	closed_at timestamp NULL,
	CONSTRAINT dispute_records_pkey PRIMARY KEY (id),
	CONSTRAINT dispute_records_stripe_dispute_id_key UNIQUE (stripe_dispute_id),
	CONSTRAINT dispute_records_booking_id_fkey FOREIGN KEY (booking_id) REFERENCES public.bookings(id) ON DELETE CASCADE
);
CREATE INDEX idx_disputes_booking ON public.dispute_records USING btree (booking_id);
CREATE INDEX idx_disputes_evidence_due ON public.dispute_records USING btree (evidence_due_by);
CREATE INDEX idx_disputes_status ON public.dispute_records USING btree (status);

-- Permissions

ALTER TABLE public.dispute_records OWNER TO alex;
GRANT ALL ON TABLE public.dispute_records TO alex;


-- public.email_notifications definition

-- Drop table

-- DROP TABLE public.email_notifications;

CREATE TABLE public.email_notifications (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	booking_token_id uuid NULL,
	customer_email varchar(255) NOT NULL,
	notification_type varchar(50) NOT NULL,
	subject varchar(255) NOT NULL,
	body text NOT NULL,
	status varchar(50) DEFAULT 'pending'::character varying NULL,
	sent_at timestamp NULL,
	error_message text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT email_notifications_pkey PRIMARY KEY (id),
	CONSTRAINT fk_email_notifications_booking_token FOREIGN KEY (booking_token_id) REFERENCES public.booking_tokens(id)
);
CREATE INDEX idx_email_notifications_booking_token ON public.email_notifications USING btree (booking_token_id);
CREATE INDEX idx_email_notifications_customer ON public.email_notifications USING btree (customer_email);
CREATE INDEX idx_email_notifications_status ON public.email_notifications USING btree (status);
CREATE INDEX idx_email_notifications_type ON public.email_notifications USING btree (notification_type);

-- Permissions

ALTER TABLE public.email_notifications OWNER TO alex;
GRANT ALL ON TABLE public.email_notifications TO alex;


-- public.external_vehicles definition

-- Drop table

-- DROP TABLE public.external_vehicles;

CREATE TABLE public.external_vehicles (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	vehicle_id uuid NOT NULL,
	external_company_vehicle_id uuid NOT NULL,
	is_primary bool DEFAULT true NOT NULL,
	linked_at timestamptz DEFAULT now() NOT NULL,
	linked_by uuid NULL,
	notes text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT external_vehicles_pkey PRIMARY KEY (id),
	CONSTRAINT uq_external_vehicles_ext_vehicle UNIQUE (external_company_vehicle_id),
	CONSTRAINT external_vehicles_external_company_vehicle_id_fkey FOREIGN KEY (external_company_vehicle_id) REFERENCES public.external_company_vehicles(id) ON DELETE CASCADE,
	CONSTRAINT external_vehicles_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_external_vehicles_ext_vehicle ON public.external_vehicles USING btree (external_company_vehicle_id);
CREATE INDEX idx_external_vehicles_vehicle ON public.external_vehicles USING btree (vehicle_id);

-- Table Triggers

create trigger tr_external_vehicles_updated before
update
    on
    public.external_vehicles for each row execute function update_external_updated_at();

-- Permissions

ALTER TABLE public.external_vehicles OWNER TO alex;
GRANT ALL ON TABLE public.external_vehicles TO alex;


-- public.instagram_conversations definition

-- Drop table

-- DROP TABLE public.instagram_conversations;

CREATE TABLE public.instagram_conversations (
	id serial4 NOT NULL,
	company_id uuid NOT NULL,
	instagram_user_id varchar(100) NOT NULL,
	instagram_username varchar(100) NULL,
	state int4 DEFAULT 0 NOT NULL,
	pickup_date timestamp NULL,
	return_date timestamp NULL,
	pickup_location varchar(200) NULL,
	selected_model_id uuid NULL,
	booking_id uuid NULL,
	"language" varchar(10) DEFAULT 'en'::character varying NULL,
	created_at timestamp DEFAULT now() NOT NULL,
	last_activity_at timestamp DEFAULT now() NOT NULL,
	expires_at timestamp DEFAULT (now() + '24:00:00'::interval) NOT NULL,
	CONSTRAINT instagram_conversations_pkey PRIMARY KEY (id),
	CONSTRAINT instagram_conversations_booking_id_fkey FOREIGN KEY (booking_id) REFERENCES public.bookings(id) ON DELETE SET NULL,
	CONSTRAINT instagram_conversations_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE,
	CONSTRAINT instagram_conversations_selected_model_id_fkey FOREIGN KEY (selected_model_id) REFERENCES public.models(id) ON DELETE SET NULL
);
CREATE INDEX idx_instagram_conversations_company_user ON public.instagram_conversations USING btree (company_id, instagram_user_id);
CREATE INDEX idx_instagram_conversations_expires ON public.instagram_conversations USING btree (expires_at);
COMMENT ON TABLE public.instagram_conversations IS 'Stores Instagram DM conversation sessions for AI-powered booking assistant';

-- Permissions

ALTER TABLE public.instagram_conversations OWNER TO alex;
GRANT ALL ON TABLE public.instagram_conversations TO alex;


-- public.instagram_messages definition

-- Drop table

-- DROP TABLE public.instagram_messages;

CREATE TABLE public.instagram_messages (
	id bigserial NOT NULL,
	conversation_id int4 NOT NULL,
	instagram_message_id varchar(100) NULL,
	sender int4 DEFAULT 0 NOT NULL,
	"content" text NOT NULL,
	message_type varchar(50) DEFAULT 'text'::character varying NULL,
	quick_reply_payload varchar(500) NULL,
	"timestamp" timestamp DEFAULT now() NOT NULL,
	delivered bool NULL,
	delivery_error text NULL,
	CONSTRAINT instagram_messages_pkey PRIMARY KEY (id),
	CONSTRAINT instagram_messages_conversation_id_fkey FOREIGN KEY (conversation_id) REFERENCES public.instagram_conversations(id) ON DELETE CASCADE
);
CREATE INDEX idx_instagram_messages_conversation ON public.instagram_messages USING btree (conversation_id);
CREATE INDEX idx_instagram_messages_ig_id ON public.instagram_messages USING btree (instagram_message_id);
COMMENT ON TABLE public.instagram_messages IS 'Stores individual messages in Instagram DM conversations';

-- Permissions

ALTER TABLE public.instagram_messages OWNER TO alex;
GRANT ALL ON TABLE public.instagram_messages TO alex;


-- public.refund_records definition

-- Drop table

-- DROP TABLE public.refund_records;

CREATE TABLE public.refund_records (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	booking_id uuid NULL,
	stripe_refund_id varchar(255) NOT NULL,
	amount numeric(10, 2) NOT NULL,
	refund_type varchar(50) NULL,
	reason text NULL,
	status varchar(50) NULL,
	processed_by uuid NULL,
	created_at timestamp DEFAULT now() NULL,
	CONSTRAINT refund_records_pkey PRIMARY KEY (id),
	CONSTRAINT refund_records_stripe_refund_id_key UNIQUE (stripe_refund_id),
	CONSTRAINT refund_records_booking_id_fkey FOREIGN KEY (booking_id) REFERENCES public.bookings(id) ON DELETE CASCADE,
	CONSTRAINT refund_records_processed_by_fkey FOREIGN KEY (processed_by) REFERENCES public.customers(id)
);
CREATE INDEX idx_refunds_booking ON public.refund_records USING btree (booking_id);
CREATE INDEX idx_refunds_type ON public.refund_records USING btree (refund_type);

-- Permissions

ALTER TABLE public.refund_records OWNER TO alex;
GRANT ALL ON TABLE public.refund_records TO alex;


-- public.rental_agreements definition

-- Drop table

-- DROP TABLE public.rental_agreements;

CREATE TABLE public.rental_agreements (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	booking_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	vehicle_id uuid NOT NULL,
	agreement_number varchar(50) NOT NULL,
	"language" varchar(20) DEFAULT 'en'::character varying NOT NULL,
	customer_name varchar(255) NOT NULL,
	customer_email varchar(255) NOT NULL,
	customer_phone varchar(50) NULL,
	customer_address text NULL,
	driver_license_number varchar(100) NULL,
	driver_license_state varchar(100) NULL,
	vehicle_name varchar(255) NOT NULL,
	vehicle_plate varchar(50) NULL,
	pickup_date timestamptz NOT NULL,
	pickup_location varchar(255) NULL,
	return_date timestamptz NOT NULL,
	return_location varchar(255) NULL,
	rental_amount numeric(10, 2) NOT NULL,
	deposit_amount numeric(10, 2) DEFAULT 0 NOT NULL,
	currency varchar(10) DEFAULT 'USD'::character varying NOT NULL,
	signature_image text NOT NULL, -- Base64 encoded PNG of customer signature
	signature_hash varchar(128) NOT NULL, -- SHA-256 hash of signature for integrity verification
	terms_accepted_at timestamptz NOT NULL,
	non_refundable_accepted_at timestamptz NOT NULL,
	damage_policy_accepted_at timestamptz NOT NULL,
	card_authorization_accepted_at timestamptz NOT NULL,
	terms_text text NOT NULL,
	non_refundable_text text NOT NULL, -- Full text of non-refundable policy customer agreed to
	damage_policy_text text NOT NULL, -- Full text of damage policy customer agreed to
	card_authorization_text text NOT NULL, -- Full text of card authorization customer agreed to
	signed_at timestamptz NOT NULL,
	ip_address varchar(45) NULL,
	user_agent text NULL,
	timezone varchar(100) NULL,
	device_info jsonb NULL,
	geo_latitude numeric(10, 8) NULL,
	geo_longitude numeric(11, 8) NULL,
	geo_accuracy numeric(10, 2) NULL,
	pdf_url text NULL,
	pdf_generated_at timestamptz NULL,
	status varchar(50) DEFAULT 'active'::character varying NOT NULL,
	voided_at timestamptz NULL,
	voided_reason text NULL,
	superseded_by_id uuid NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	additional_services_json text NULL, -- JSON snapshot of additional services selected at booking time
	CONSTRAINT rental_agreements_pkey PRIMARY KEY (id),
	CONSTRAINT rental_agreements_booking_id_fkey FOREIGN KEY (booking_id) REFERENCES public.bookings(id),
	CONSTRAINT rental_agreements_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id),
	CONSTRAINT rental_agreements_customer_id_fkey FOREIGN KEY (customer_id) REFERENCES public.customers(id),
	CONSTRAINT rental_agreements_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id)
);
CREATE INDEX idx_rental_agreements_booking_id ON public.rental_agreements USING btree (booking_id);
CREATE INDEX idx_rental_agreements_company_id ON public.rental_agreements USING btree (company_id);
CREATE INDEX idx_rental_agreements_customer_email ON public.rental_agreements USING btree (customer_email);
CREATE INDEX idx_rental_agreements_customer_id ON public.rental_agreements USING btree (customer_id);
CREATE INDEX idx_rental_agreements_driver_license ON public.rental_agreements USING btree (driver_license_number) WHERE (driver_license_number IS NOT NULL);
CREATE UNIQUE INDEX idx_rental_agreements_number ON public.rental_agreements USING btree (company_id, agreement_number);
CREATE INDEX idx_rental_agreements_pickup_date ON public.rental_agreements USING btree (pickup_date);
CREATE INDEX idx_rental_agreements_signature_hash ON public.rental_agreements USING btree (signature_hash);
CREATE INDEX idx_rental_agreements_signed_at ON public.rental_agreements USING btree (signed_at DESC);
CREATE INDEX idx_rental_agreements_status ON public.rental_agreements USING btree (status) WHERE ((status)::text = 'active'::text);
COMMENT ON TABLE public.rental_agreements IS 'Stores signed rental agreements with electronic signatures for chargeback protection';

-- Column comments

COMMENT ON COLUMN public.rental_agreements.signature_image IS 'Base64 encoded PNG of customer signature';
COMMENT ON COLUMN public.rental_agreements.signature_hash IS 'SHA-256 hash of signature for integrity verification';
COMMENT ON COLUMN public.rental_agreements.non_refundable_text IS 'Full text of non-refundable policy customer agreed to';
COMMENT ON COLUMN public.rental_agreements.damage_policy_text IS 'Full text of damage policy customer agreed to';
COMMENT ON COLUMN public.rental_agreements.card_authorization_text IS 'Full text of card authorization customer agreed to';
COMMENT ON COLUMN public.rental_agreements.additional_services_json IS 'JSON snapshot of additional services selected at booking time';

-- Table Triggers

create trigger trg_rental_agreements_updated_at before
update
    on
    public.rental_agreements for each row execute function update_rental_agreements_updated_at();

-- Permissions

ALTER TABLE public.rental_agreements OWNER TO alex;
GRANT ALL ON TABLE public.rental_agreements TO alex;


-- public.rentals definition

-- Drop table

-- DROP TABLE public.rentals;

CREATE TABLE public.rentals (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	booking_id uuid NOT NULL,
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
	CONSTRAINT rentals_pkey PRIMARY KEY (id),
	CONSTRAINT fk_rentals_booking FOREIGN KEY (booking_id) REFERENCES public.bookings(id) ON DELETE CASCADE,
	CONSTRAINT fk_rentals_company FOREIGN KEY (company_id) REFERENCES public.companies(id),
	CONSTRAINT fk_rentals_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id),
	CONSTRAINT fk_rentals_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id)
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
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	rental_id uuid NOT NULL,
	customer_id uuid NOT NULL,
	company_id uuid NOT NULL,
	vehicle_id uuid NOT NULL,
	rating int4 NULL,
	"comment" text NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT reviews_pkey PRIMARY KEY (id),
	CONSTRAINT reviews_rating_check CHECK (((rating >= 1) AND (rating <= 5))),
	CONSTRAINT fk_reviews_company FOREIGN KEY (company_id) REFERENCES public.companies(id),
	CONSTRAINT fk_reviews_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id),
	CONSTRAINT fk_reviews_rental FOREIGN KEY (rental_id) REFERENCES public.rentals(id),
	CONSTRAINT fk_reviews_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id)
);

-- Permissions

ALTER TABLE public.reviews OWNER TO alex;
GRANT ALL ON TABLE public.reviews TO alex;


-- public.scheduled_posts definition

-- Drop table

-- DROP TABLE public.scheduled_posts;

CREATE TABLE public.scheduled_posts (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	vehicle_id uuid NULL,
	vehicle_ids _uuid NULL,
	post_type int4 DEFAULT 0 NOT NULL,
	platform int4 DEFAULT 1 NOT NULL,
	caption text NULL,
	scheduled_for timestamptz NOT NULL,
	include_price bool DEFAULT false NOT NULL,
	daily_rate numeric(10, 2) NULL,
	currency varchar(3) NULL,
	custom_hashtags _text NULL,
	status int4 DEFAULT 0 NOT NULL,
	error_message text NULL,
	post_id varchar(100) NULL,
	permalink text NULL,
	published_at timestamptz NULL,
	retry_count int4 DEFAULT 0 NOT NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT scheduled_posts_pkey PRIMARY KEY (id),
	CONSTRAINT scheduled_posts_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE,
	CONSTRAINT scheduled_posts_vehicle_id_fkey FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE SET NULL
);
CREATE INDEX idx_scheduled_posts_company_id ON public.scheduled_posts USING btree (company_id);
CREATE INDEX idx_scheduled_posts_pending ON public.scheduled_posts USING btree (status, scheduled_for) WHERE (status = 0);
CREATE INDEX idx_scheduled_posts_scheduled_for ON public.scheduled_posts USING btree (scheduled_for);
CREATE INDEX idx_scheduled_posts_status ON public.scheduled_posts USING btree (status);
COMMENT ON TABLE public.scheduled_posts IS 'Stores scheduled social media posts';

-- Table Triggers

create trigger update_scheduled_posts_updated_at before
update
    on
    public.scheduled_posts for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.scheduled_posts OWNER TO alex;
GRANT ALL ON TABLE public.scheduled_posts TO alex;


-- public.stripe_transfers definition

-- Drop table

-- DROP TABLE public.stripe_transfers;

CREATE TABLE public.stripe_transfers (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	booking_id uuid NOT NULL,
	company_id uuid NOT NULL,
	stripe_transfer_id varchar(255) NOT NULL, -- Stripe Transfer ID (tr_xxx)
	stripe_payment_intent_id varchar(255) NULL,
	amount numeric(10, 2) NOT NULL,
	currency varchar(3) DEFAULT 'USD'::character varying NULL,
	platform_fee numeric(10, 2) NOT NULL, -- Platform commission amount kept by platform
	net_amount numeric(10, 2) NOT NULL, -- Amount transferred to connected account
	destination_account_id varchar(255) NOT NULL, -- Connected Account ID receiving transfer
	status varchar(50) DEFAULT 'pending'::character varying NULL, -- pending, paid, failed, canceled, reversed
	failure_code varchar(100) NULL,
	failure_message text NULL,
	transferred_at timestamptz NULL,
	reversed_at timestamptz NULL,
	reversal_id varchar(255) NULL, -- Stripe Reversal ID if transfer was reversed
	metadata jsonb NULL,
	created_at timestamptz DEFAULT now() NULL,
	updated_at timestamptz DEFAULT now() NULL,
	CONSTRAINT stripe_transfers_pkey PRIMARY KEY (id),
	CONSTRAINT stripe_transfers_stripe_transfer_id_key UNIQUE (stripe_transfer_id),
	CONSTRAINT fk_transfers_booking FOREIGN KEY (booking_id) REFERENCES public.bookings(id) ON DELETE CASCADE,
	CONSTRAINT fk_transfers_company FOREIGN KEY (company_id) REFERENCES public.companies(id)
);
CREATE INDEX idx_transfers_booking ON public.stripe_transfers USING btree (booking_id);
CREATE INDEX idx_transfers_company ON public.stripe_transfers USING btree (company_id);
CREATE INDEX idx_transfers_destination ON public.stripe_transfers USING btree (destination_account_id);
CREATE INDEX idx_transfers_status ON public.stripe_transfers USING btree (status);
CREATE INDEX idx_transfers_stripe_id ON public.stripe_transfers USING btree (stripe_transfer_id);
CREATE INDEX idx_transfers_transferred_at ON public.stripe_transfers USING btree (transferred_at);
COMMENT ON TABLE public.stripe_transfers IS 'Records of transfers from platform to connected accounts';

-- Column comments

COMMENT ON COLUMN public.stripe_transfers.stripe_transfer_id IS 'Stripe Transfer ID (tr_xxx)';
COMMENT ON COLUMN public.stripe_transfers.platform_fee IS 'Platform commission amount kept by platform';
COMMENT ON COLUMN public.stripe_transfers.net_amount IS 'Amount transferred to connected account';
COMMENT ON COLUMN public.stripe_transfers.destination_account_id IS 'Connected Account ID receiving transfer';
COMMENT ON COLUMN public.stripe_transfers.status IS 'pending, paid, failed, canceled, reversed';
COMMENT ON COLUMN public.stripe_transfers.reversal_id IS 'Stripe Reversal ID if transfer was reversed';

-- Table Triggers

create trigger trigger_transfers_updated_at before
update
    on
    public.stripe_transfers for each row execute function update_transfers_updated_at();

-- Permissions

ALTER TABLE public.stripe_transfers OWNER TO alex;
GRANT ALL ON TABLE public.stripe_transfers TO alex;


-- public.tracking_devices definition

-- Drop table

-- DROP TABLE public.tracking_devices;

CREATE TABLE public.tracking_devices (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	vehicle_id uuid NOT NULL,
	serial varchar(50) NOT NULL, -- Datatrack device serial number (unique identifier)
	device_name varchar(100) NULL,
	imei varchar(20) NULL,
	sim_number varchar(20) NULL,
	firmware_version varchar(20) NULL,
	is_active bool DEFAULT true NOT NULL,
	installed_at timestamp NULL,
	last_communication_at timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT tracking_devices_pkey PRIMARY KEY (id),
	CONSTRAINT tracking_devices_serial_key UNIQUE (serial),
	CONSTRAINT tracking_devices_vehicle_key UNIQUE (vehicle_id),
	CONSTRAINT fk_tracking_devices_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_tracking_devices_active ON public.tracking_devices USING btree (is_active) WHERE (is_active = true);
CREATE INDEX idx_tracking_devices_serial ON public.tracking_devices USING btree (serial);
CREATE INDEX idx_tracking_devices_vehicle ON public.tracking_devices USING btree (vehicle_id);
COMMENT ON TABLE public.tracking_devices IS 'GPS tracking devices mapped to vehicles';

-- Column comments

COMMENT ON COLUMN public.tracking_devices.serial IS 'Datatrack device serial number (unique identifier)';

-- Table Triggers

create trigger update_tracking_devices_updated_at before
update
    on
    public.tracking_devices for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.tracking_devices OWNER TO alex;
GRANT ALL ON TABLE public.tracking_devices TO alex;


-- public.vehicle_events definition

-- Drop table

-- DROP TABLE public.vehicle_events;

CREATE TABLE public.vehicle_events (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	vehicle_id uuid NOT NULL,
	device_serial varchar(50) NOT NULL,
	event_type varchar(50) NOT NULL,
	event_code int2 NULL,
	severity varchar(20) DEFAULT 'info'::character varying NULL,
	latitude numeric(10, 7) NULL,
	longitude numeric(10, 7) NULL,
	address varchar(500) NULL,
	event_data jsonb NULL,
	event_time timestamp NOT NULL,
	received_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	acknowledged_at timestamp NULL,
	acknowledged_by uuid NULL,
	CONSTRAINT chk_event_severity CHECK (((severity)::text = ANY ((ARRAY['info'::character varying, 'warning'::character varying, 'alert'::character varying, 'critical'::character varying])::text[]))),
	CONSTRAINT vehicle_events_pkey PRIMARY KEY (id),
	CONSTRAINT fk_vehicle_events_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_vehicle_events_severity ON public.vehicle_events USING btree (severity) WHERE ((severity)::text = ANY ((ARRAY['warning'::character varying, 'alert'::character varying, 'critical'::character varying])::text[]));
CREATE INDEX idx_vehicle_events_time ON public.vehicle_events USING btree (event_time DESC);
CREATE INDEX idx_vehicle_events_type ON public.vehicle_events USING btree (event_type);
CREATE INDEX idx_vehicle_events_unacknowledged ON public.vehicle_events USING btree (acknowledged_at) WHERE (acknowledged_at IS NULL);
CREATE INDEX idx_vehicle_events_vehicle_time ON public.vehicle_events USING btree (vehicle_id, event_time DESC);
COMMENT ON TABLE public.vehicle_events IS 'Significant vehicle events from tracking devices';

-- Permissions

ALTER TABLE public.vehicle_events OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_events TO alex;


-- public.vehicle_geofences definition

-- Drop table

-- DROP TABLE public.vehicle_geofences;

CREATE TABLE public.vehicle_geofences (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	vehicle_id uuid NOT NULL,
	geofence_id uuid NOT NULL,
	is_inside bool DEFAULT false NULL,
	last_enter_time timestamp NULL,
	last_exit_time timestamp NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT vehicle_geofences_pkey PRIMARY KEY (id),
	CONSTRAINT vehicle_geofences_unique UNIQUE (vehicle_id, geofence_id),
	CONSTRAINT fk_vehicle_geofences_geofence FOREIGN KEY (geofence_id) REFERENCES public.geofences(id) ON DELETE CASCADE,
	CONSTRAINT fk_vehicle_geofences_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_vehicle_geofences_geofence ON public.vehicle_geofences USING btree (geofence_id);
CREATE INDEX idx_vehicle_geofences_vehicle ON public.vehicle_geofences USING btree (vehicle_id);

-- Permissions

ALTER TABLE public.vehicle_geofences OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_geofences TO alex;


-- public.vehicle_locations definition

-- Drop table

-- DROP TABLE public.vehicle_locations;

CREATE TABLE public.vehicle_locations (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	vehicle_id uuid NOT NULL,
	device_serial varchar(50) NOT NULL,
	latitude numeric(10, 7) NOT NULL,
	longitude numeric(10, 7) NOT NULL,
	altitude numeric(8, 2) NULL,
	heading int2 NULL,
	speed_kmh int2 DEFAULT 0 NULL,
	odometer_meters int8 NULL,
	location_type_id int2 NOT NULL, -- Datatrack location type: 2=ign off, 3=stopped, 4=ign on, 5=moving, 24=starter disabled, 25=starter enabled
	gps_quality int2 DEFAULT 0 NULL,
	voltage_mv int4 NULL,
	ignition_on bool DEFAULT false NULL,
	starter_disabled bool DEFAULT false NULL,
	device_timestamp timestamp NOT NULL,
	received_at timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	CONSTRAINT vehicle_locations_pkey PRIMARY KEY (id),
	CONSTRAINT fk_vehicle_locations_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_vehicle_locations_device_time ON public.vehicle_locations USING btree (device_serial, device_timestamp DESC);
CREATE INDEX idx_vehicle_locations_timestamp ON public.vehicle_locations USING btree (device_timestamp DESC);
CREATE INDEX idx_vehicle_locations_type ON public.vehicle_locations USING btree (location_type_id);
CREATE INDEX idx_vehicle_locations_vehicle_time ON public.vehicle_locations USING btree (vehicle_id, device_timestamp DESC);
COMMENT ON TABLE public.vehicle_locations IS 'Historical location data from GPS tracking devices';

-- Column comments

COMMENT ON COLUMN public.vehicle_locations.location_type_id IS 'Datatrack location type: 2=ign off, 3=stopped, 4=ign on, 5=moving, 24=starter disabled, 25=starter enabled';

-- Permissions

ALTER TABLE public.vehicle_locations OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_locations TO alex;


-- public.vehicle_social_posts definition

-- Drop table

-- DROP TABLE public.vehicle_social_posts;

CREATE TABLE public.vehicle_social_posts (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL, -- Reference to the rental company
	vehicle_id uuid NULL, -- Reference to the vehicle
	platform varchar(50) NOT NULL, -- Social platform (Facebook or Instagram)
	post_id varchar(100) NOT NULL, -- ID of the post on the social platform
	permalink varchar(500) NULL, -- Permanent link to the post
	caption text NULL, -- Caption used when posting
	image_url varchar(1000) NULL, -- Image URL used when posting
	daily_rate numeric(10, 2) NULL, -- Daily rate at time of posting
	is_active bool DEFAULT true NOT NULL, -- Whether this post record is active
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	vehicle_model_id uuid NULL,
	CONSTRAINT vehicle_social_posts_pkey PRIMARY KEY (id),
	CONSTRAINT fk_vehicle_social_posts_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE,
	CONSTRAINT fk_vehicle_social_posts_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE SET NULL,
	CONSTRAINT fk_vehicle_social_posts_vehicle_model FOREIGN KEY (vehicle_model_id) REFERENCES public.vehicle_model(id) ON DELETE SET NULL
);
CREATE INDEX idx_vehicle_social_posts_company_id ON public.vehicle_social_posts USING btree (company_id);
CREATE INDEX idx_vehicle_social_posts_company_model ON public.vehicle_social_posts USING btree (company_id, vehicle_model_id);
CREATE INDEX idx_vehicle_social_posts_company_platform ON public.vehicle_social_posts USING btree (company_id, platform) WHERE (is_active = true);
CREATE INDEX idx_vehicle_social_posts_company_vehicle ON public.vehicle_social_posts USING btree (company_id, vehicle_id);
CREATE INDEX idx_vehicle_social_posts_model_platform_active ON public.vehicle_social_posts USING btree (vehicle_model_id, platform) WHERE (is_active = true);
CREATE INDEX idx_vehicle_social_posts_platform ON public.vehicle_social_posts USING btree (platform);
CREATE UNIQUE INDEX idx_vehicle_social_posts_unique_active ON public.vehicle_social_posts USING btree (vehicle_id, platform) WHERE (is_active = true);
CREATE INDEX idx_vehicle_social_posts_vehicle_id ON public.vehicle_social_posts USING btree (vehicle_id);
CREATE INDEX idx_vehicle_social_posts_vehicle_model_id ON public.vehicle_social_posts USING btree (vehicle_model_id);
CREATE INDEX idx_vehicle_social_posts_vehicle_platform ON public.vehicle_social_posts USING btree (vehicle_id, platform) WHERE (is_active = true);
COMMENT ON TABLE public.vehicle_social_posts IS 'Tracks vehicles posted to Facebook/Instagram';

-- Column comments

COMMENT ON COLUMN public.vehicle_social_posts.company_id IS 'Reference to the rental company';
COMMENT ON COLUMN public.vehicle_social_posts.vehicle_id IS 'Reference to the vehicle';
COMMENT ON COLUMN public.vehicle_social_posts.platform IS 'Social platform (Facebook or Instagram)';
COMMENT ON COLUMN public.vehicle_social_posts.post_id IS 'ID of the post on the social platform';
COMMENT ON COLUMN public.vehicle_social_posts.permalink IS 'Permanent link to the post';
COMMENT ON COLUMN public.vehicle_social_posts.caption IS 'Caption used when posting';
COMMENT ON COLUMN public.vehicle_social_posts.image_url IS 'Image URL used when posting';
COMMENT ON COLUMN public.vehicle_social_posts.daily_rate IS 'Daily rate at time of posting';
COMMENT ON COLUMN public.vehicle_social_posts.is_active IS 'Whether this post record is active';

-- Table Triggers

create trigger trigger_vehicle_social_posts_updated_at before
update
    on
    public.vehicle_social_posts for each row execute function update_vehicle_social_posts_updated_at();

-- Permissions

ALTER TABLE public.vehicle_social_posts OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_social_posts TO alex;
GRANT ALL ON TABLE public.vehicle_social_posts TO azure_pg_admin;


-- public.vehicle_tracking_status definition

-- Drop table

-- DROP TABLE public.vehicle_tracking_status;

CREATE TABLE public.vehicle_tracking_status (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	vehicle_id uuid NOT NULL,
	device_serial varchar(50) NOT NULL,
	latitude numeric(10, 7) NOT NULL,
	longitude numeric(10, 7) NOT NULL,
	address varchar(500) NULL,
	speed_kmh int2 DEFAULT 0 NULL,
	heading int2 NULL,
	location_type_id int2 NOT NULL,
	voltage_mv int4 NULL,
	odometer_meters int8 NULL,
	is_moving bool DEFAULT false NULL,
	ignition_on bool DEFAULT false NULL,
	starter_disabled bool DEFAULT false NULL,
	device_timestamp timestamp NOT NULL,
	last_updated timestamp DEFAULT CURRENT_TIMESTAMP NOT NULL,
	CONSTRAINT vehicle_tracking_status_pkey PRIMARY KEY (id),
	CONSTRAINT vehicle_tracking_status_vehicle_key UNIQUE (vehicle_id),
	CONSTRAINT fk_vehicle_tracking_status_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_vehicle_tracking_status_disabled ON public.vehicle_tracking_status USING btree (starter_disabled) WHERE (starter_disabled = true);
CREATE INDEX idx_vehicle_tracking_status_moving ON public.vehicle_tracking_status USING btree (is_moving) WHERE (is_moving = true);
COMMENT ON TABLE public.vehicle_tracking_status IS 'Current tracking status for each vehicle (latest position)';

-- Permissions

ALTER TABLE public.vehicle_tracking_status OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_tracking_status TO alex;


-- public.vehicle_trips definition

-- Drop table

-- DROP TABLE public.vehicle_trips;

CREATE TABLE public.vehicle_trips (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	vehicle_id uuid NOT NULL,
	device_serial varchar(50) NOT NULL,
	start_time timestamp NOT NULL,
	end_time timestamp NULL,
	start_latitude numeric(10, 7) NOT NULL,
	start_longitude numeric(10, 7) NOT NULL,
	start_address varchar(500) NULL,
	end_latitude numeric(10, 7) NULL,
	end_longitude numeric(10, 7) NULL,
	end_address varchar(500) NULL,
	distance_meters int4 DEFAULT 0 NULL,
	max_speed_kmh int2 DEFAULT 0 NULL,
	avg_speed_kmh int2 DEFAULT 0 NULL,
	idle_duration_seconds int4 DEFAULT 0 NULL,
	start_odometer_meters int8 NULL,
	end_odometer_meters int8 NULL,
	status varchar(20) DEFAULT 'in_progress'::character varying NULL,
	created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
	CONSTRAINT chk_trip_status CHECK (((status)::text = ANY ((ARRAY['in_progress'::character varying, 'completed'::character varying, 'interrupted'::character varying])::text[]))),
	CONSTRAINT vehicle_trips_pkey PRIMARY KEY (id),
	CONSTRAINT fk_vehicle_trips_vehicle FOREIGN KEY (vehicle_id) REFERENCES public.vehicles(id) ON DELETE CASCADE
);
CREATE INDEX idx_vehicle_trips_date ON public.vehicle_trips USING btree (start_time DESC);
CREATE INDEX idx_vehicle_trips_status ON public.vehicle_trips USING btree (status) WHERE ((status)::text = 'in_progress'::text);
CREATE INDEX idx_vehicle_trips_vehicle_time ON public.vehicle_trips USING btree (vehicle_id, start_time DESC);
COMMENT ON TABLE public.vehicle_trips IS 'Individual vehicle trips from ignition on to ignition off';

-- Table Triggers

create trigger update_vehicle_trips_updated_at before
update
    on
    public.vehicle_trips for each row execute function update_updated_at_column();

-- Permissions

ALTER TABLE public.vehicle_trips OWNER TO alex;
GRANT ALL ON TABLE public.vehicle_trips TO alex;


-- public.agreement_disputes definition

-- Drop table

-- DROP TABLE public.agreement_disputes;

CREATE TABLE public.agreement_disputes (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	agreement_id uuid NOT NULL,
	dispute_record_id uuid NULL,
	stripe_dispute_id varchar(255) NOT NULL,
	stripe_payment_intent_id varchar(255) NULL,
	stripe_charge_id varchar(255) NULL,
	dispute_reason varchar(100) NULL,
	dispute_amount numeric(10, 2) NOT NULL,
	dispute_currency varchar(10) DEFAULT 'USD'::character varying NOT NULL,
	status varchar(50) DEFAULT 'open'::character varying NOT NULL,
	evidence_submitted_at timestamptz NULL,
	evidence_due_by timestamptz NULL,
	evidence_pdf_url text NULL,
	resolved_at timestamptz NULL,
	resolution_reason text NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	updated_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT agreement_disputes_pkey PRIMARY KEY (id),
	CONSTRAINT agreement_disputes_agreement_id_fkey FOREIGN KEY (agreement_id) REFERENCES public.rental_agreements(id) ON DELETE CASCADE,
	CONSTRAINT agreement_disputes_dispute_record_id_fkey FOREIGN KEY (dispute_record_id) REFERENCES public.dispute_records(id) ON DELETE SET NULL
);
CREATE INDEX idx_agreement_disputes_agreement_id ON public.agreement_disputes USING btree (agreement_id);
CREATE INDEX idx_agreement_disputes_evidence_due ON public.agreement_disputes USING btree (evidence_due_by) WHERE ((status)::text = 'open'::text);
CREATE INDEX idx_agreement_disputes_status ON public.agreement_disputes USING btree (status) WHERE ((status)::text = 'open'::text);
CREATE UNIQUE INDEX idx_agreement_disputes_stripe_id ON public.agreement_disputes USING btree (stripe_dispute_id);
COMMENT ON TABLE public.agreement_disputes IS 'Tracks disputes specifically related to rental agreements';

-- Table Triggers

create trigger trg_agreement_disputes_updated_at before
update
    on
    public.agreement_disputes for each row execute function update_agreement_disputes_updated_at();

-- Permissions

ALTER TABLE public.agreement_disputes OWNER TO alex;
GRANT ALL ON TABLE public.agreement_disputes TO alex;


-- public.booking_confirmations definition

-- Drop table

-- DROP TABLE public.booking_confirmations;

CREATE TABLE public.booking_confirmations (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
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
	CONSTRAINT booking_confirmations_pkey PRIMARY KEY (id),
	CONSTRAINT booking_confirmations_reservation_id_fkey FOREIGN KEY (reservation_id) REFERENCES public.bookings(id) ON DELETE SET NULL,
	CONSTRAINT fk_booking_confirmations_booking_token FOREIGN KEY (booking_token_id) REFERENCES public.booking_tokens(id)
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


-- public.dispute_evidence_files definition

-- Drop table

-- DROP TABLE public.dispute_evidence_files;

CREATE TABLE public.dispute_evidence_files (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	dispute_id uuid NULL,
	stripe_file_id varchar(255) NULL,
	file_type varchar(50) NULL,
	file_name varchar(500) NULL,
	file_url text NULL,
	uploaded_at timestamp DEFAULT now() NULL,
	uploaded_by uuid NULL,
	CONSTRAINT dispute_evidence_files_pkey PRIMARY KEY (id),
	CONSTRAINT dispute_evidence_files_dispute_id_fkey FOREIGN KEY (dispute_id) REFERENCES public.dispute_records(id) ON DELETE CASCADE
);
CREATE INDEX idx_evidence_files_dispute ON public.dispute_evidence_files USING btree (dispute_id);

-- Permissions

ALTER TABLE public.dispute_evidence_files OWNER TO alex;
GRANT ALL ON TABLE public.dispute_evidence_files TO alex;


-- public.payments definition

-- Drop table

-- DROP TABLE public.payments;

CREATE TABLE public.payments (
	id uuid DEFAULT uuid_generate_v4() NOT NULL,
	booking_id uuid NULL,
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
	security_deposit_amount numeric(10, 2) DEFAULT 0.00 NULL,
	security_deposit_status varchar(50) NULL,
	security_deposit_payment_intent_id varchar(255) NULL,
	security_deposit_charge_id varchar(255) NULL,
	security_deposit_authorized_at timestamp NULL,
	security_deposit_captured_at timestamp NULL,
	security_deposit_released_at timestamp NULL,
	security_deposit_captured_amount numeric(10, 2) DEFAULT 0.00 NULL,
	security_deposit_capture_reason text NULL,
	destination_account_id varchar(255) NULL, -- Connected Account ID receiving the funds
	platform_fee_amount numeric(10, 2) DEFAULT 0 NULL, -- Platform commission amount
	transfer_group varchar(255) NULL, -- Groups related payments together (booking + deposit)
	on_behalf_of varchar(255) NULL, -- Connected Account ID for "on behalf of" charges
	stripe_transfer_id varchar(255) NULL, -- Stripe Transfer ID if funds were transferred
	updated_at timestamp NULL,
	refund_amount numeric(10, 2) NULL,
	refund_date timestamp NULL,
	CONSTRAINT payments_pkey PRIMARY KEY (id),
	CONSTRAINT payments_stripe_payment_intent_id_key UNIQUE (stripe_payment_intent_id),
	CONSTRAINT fk_payments_company FOREIGN KEY (company_id) REFERENCES public.companies(id),
	CONSTRAINT fk_payments_customer FOREIGN KEY (customer_id) REFERENCES public.customers(id),
	CONSTRAINT fk_payments_rental FOREIGN KEY (rental_id) REFERENCES public.rentals(id),
	CONSTRAINT payments_reservation_id_fkey FOREIGN KEY (booking_id) REFERENCES public.bookings(id) ON DELETE SET NULL
);
CREATE INDEX idx_payments_company ON public.payments USING btree (company_id);
CREATE INDEX idx_payments_customer ON public.payments USING btree (customer_id);
CREATE INDEX idx_payments_destination ON public.payments USING btree (destination_account_id);
CREATE INDEX idx_payments_refund_date ON public.payments USING btree (refund_date);
CREATE INDEX idx_payments_reservation ON public.payments USING btree (booking_id);
CREATE INDEX idx_payments_status ON public.payments USING btree (status);
CREATE INDEX idx_payments_stripe_intent ON public.payments USING btree (stripe_payment_intent_id);
CREATE INDEX idx_payments_stripe_transfer ON public.payments USING btree (stripe_transfer_id);
CREATE INDEX idx_payments_transfer_group ON public.payments USING btree (transfer_group);
CREATE INDEX idx_payments_updated_at ON public.payments USING btree (updated_at);

-- Column comments

COMMENT ON COLUMN public.payments.destination_account_id IS 'Connected Account ID receiving the funds';
COMMENT ON COLUMN public.payments.platform_fee_amount IS 'Platform commission amount';
COMMENT ON COLUMN public.payments.transfer_group IS 'Groups related payments together (booking + deposit)';
COMMENT ON COLUMN public.payments.on_behalf_of IS 'Connected Account ID for "on behalf of" charges';
COMMENT ON COLUMN public.payments.stripe_transfer_id IS 'Stripe Transfer ID if funds were transferred';

-- Permissions

ALTER TABLE public.payments OWNER TO alex;
GRANT ALL ON TABLE public.payments TO alex;


-- public.rental_agreement_audit_log definition

-- Drop table

-- DROP TABLE public.rental_agreement_audit_log;

CREATE TABLE public.rental_agreement_audit_log (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	agreement_id uuid NOT NULL,
	"action" varchar(50) NOT NULL,
	performed_by varchar(255) NULL,
	performed_at timestamptz DEFAULT now() NOT NULL,
	ip_address varchar(45) NULL,
	user_agent text NULL,
	details jsonb NULL,
	created_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT rental_agreement_audit_log_pkey PRIMARY KEY (id),
	CONSTRAINT rental_agreement_audit_log_agreement_id_fkey FOREIGN KEY (agreement_id) REFERENCES public.rental_agreements(id) ON DELETE CASCADE
);
CREATE INDEX idx_agreement_audit_log_action ON public.rental_agreement_audit_log USING btree (action);
CREATE INDEX idx_agreement_audit_log_agreement_id ON public.rental_agreement_audit_log USING btree (agreement_id);
CREATE INDEX idx_agreement_audit_log_performed_at ON public.rental_agreement_audit_log USING btree (performed_at DESC);
COMMENT ON TABLE public.rental_agreement_audit_log IS 'Audit trail for all agreement actions';

-- Permissions

ALTER TABLE public.rental_agreement_audit_log OWNER TO alex;
GRANT ALL ON TABLE public.rental_agreement_audit_log TO alex;


-- public.social_post_analytics definition

-- Drop table

-- DROP TABLE public.social_post_analytics;

CREATE TABLE public.social_post_analytics (
	id uuid DEFAULT gen_random_uuid() NOT NULL,
	company_id uuid NOT NULL,
	social_post_id uuid NOT NULL,
	post_id varchar(100) NOT NULL,
	platform int4 NOT NULL,
	impressions int4 DEFAULT 0 NOT NULL,
	reach int4 DEFAULT 0 NOT NULL,
	engagement int4 DEFAULT 0 NOT NULL,
	likes int4 DEFAULT 0 NOT NULL,
	"comments" int4 DEFAULT 0 NOT NULL,
	shares int4 DEFAULT 0 NOT NULL,
	saves int4 DEFAULT 0 NOT NULL,
	clicks int4 DEFAULT 0 NOT NULL,
	profile_visits int4 DEFAULT 0 NOT NULL,
	recorded_at timestamptz DEFAULT now() NOT NULL,
	CONSTRAINT social_post_analytics_pkey PRIMARY KEY (id),
	CONSTRAINT social_post_analytics_company_id_fkey FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE,
	CONSTRAINT social_post_analytics_social_post_id_fkey FOREIGN KEY (social_post_id) REFERENCES public.vehicle_social_posts(id) ON DELETE CASCADE
);
CREATE INDEX idx_social_post_analytics_company_id ON public.social_post_analytics USING btree (company_id);
CREATE INDEX idx_social_post_analytics_recorded_at ON public.social_post_analytics USING btree (recorded_at);
CREATE INDEX idx_social_post_analytics_social_post_id ON public.social_post_analytics USING btree (social_post_id);
COMMENT ON TABLE public.social_post_analytics IS 'Tracks performance metrics for social posts';

-- Permissions

ALTER TABLE public.social_post_analytics OWNER TO alex;
GRANT ALL ON TABLE public.social_post_analytics TO alex;


-- public.v_active_agreements source

CREATE OR REPLACE VIEW public.v_active_agreements
AS SELECT ra.id,
    ra.agreement_number,
    ra.company_id,
    ra.booking_id,
    ra.customer_name,
    ra.customer_email,
    ra.vehicle_name,
    ra.vehicle_plate,
    ra.pickup_date,
    ra.return_date,
    ra.rental_amount,
    ra.deposit_amount,
    ra.currency,
    ra.language,
    ra.signed_at,
    ra.pdf_url,
    ra.created_at,
    c.company_name,
    b.booking_number
   FROM rental_agreements ra
     JOIN companies c ON ra.company_id = c.id
     JOIN bookings b ON ra.booking_id = b.id
  WHERE ra.status::text = 'active'::text;

COMMENT ON VIEW public.v_active_agreements IS 'Active rental agreements with company and booking info';

-- Permissions

ALTER TABLE public.v_active_agreements OWNER TO alex;
GRANT ALL ON TABLE public.v_active_agreements TO alex;


-- public.v_agreements_pending_disputes source

CREATE OR REPLACE VIEW public.v_agreements_pending_disputes
AS SELECT ra.id AS agreement_id,
    ra.agreement_number,
    ra.customer_name,
    ra.customer_email,
    ra.rental_amount + ra.deposit_amount AS total_amount,
    ra.signed_at,
    ra.pdf_url,
    ad.stripe_dispute_id,
    ad.dispute_reason,
    ad.dispute_amount,
    ad.status AS dispute_status,
    ad.evidence_due_by,
    c.company_name
   FROM rental_agreements ra
     JOIN agreement_disputes ad ON ra.id = ad.agreement_id
     JOIN companies c ON ra.company_id = c.id
  WHERE ad.status::text = 'open'::text
  ORDER BY ad.evidence_due_by;

COMMENT ON VIEW public.v_agreements_pending_disputes IS 'Agreements with open disputes needing evidence';

-- Permissions

ALTER TABLE public.v_agreements_pending_disputes OWNER TO alex;
GRANT ALL ON TABLE public.v_agreements_pending_disputes TO alex;


-- public.v_company_financials_summary source

CREATE OR REPLACE VIEW public.v_company_financials_summary
AS SELECT c.id AS company_id,
    c.company_name,
    c.platform_fee_percentage,
    count(DISTINCT b.id) AS total_bookings,
    COALESCE(sum(b.total_amount), 0::numeric) AS total_revenue,
    COALESCE(sum(b.platform_fee_amount), 0::numeric) AS total_platform_fees,
    COALESCE(sum(b.net_amount), 0::numeric) AS total_net_revenue,
    count(DISTINCT
        CASE
            WHEN st.status::text = 'paid'::text THEN st.id
            ELSE NULL::uuid
        END) AS completed_transfers,
    COALESCE(sum(
        CASE
            WHEN st.status::text = 'paid'::text THEN st.net_amount
            ELSE 0::numeric
        END), 0::numeric) AS transferred_amount,
    COALESCE(sum(
        CASE
            WHEN st.status::text = 'pending'::text THEN st.net_amount
            ELSE 0::numeric
        END), 0::numeric) AS pending_transfer_amount,
    ( SELECT count(*) AS count
           FROM stripe_payout_records spr
          WHERE spr.company_id = c.id AND spr.status::text = 'paid'::text) AS completed_payouts,
    ( SELECT COALESCE(sum(spr.amount), 0::numeric) AS "coalesce"
           FROM stripe_payout_records spr
          WHERE spr.company_id = c.id AND spr.status::text = 'paid'::text) AS total_payout_amount
   FROM companies c
     LEFT JOIN bookings b ON c.id = b.company_id AND (b.status::text = ANY (ARRAY['Confirmed'::character varying, 'PickedUp'::character varying, 'Returned'::character varying]::text[]))
     LEFT JOIN stripe_transfers st ON b.id = st.booking_id
  GROUP BY c.id, c.company_name, c.platform_fee_percentage
  ORDER BY (COALESCE(sum(b.total_amount), 0::numeric)) DESC;

COMMENT ON VIEW public.v_company_financials_summary IS 'Financial summary per company including transfers and payouts';

-- Permissions

ALTER TABLE public.v_company_financials_summary OWNER TO alex;
GRANT ALL ON TABLE public.v_company_financials_summary TO alex;


-- public.v_company_meta_status source

CREATE OR REPLACE VIEW public.v_company_meta_status
AS SELECT c.id AS company_id,
    c.company_name,
    c.subdomain,
        CASE
            WHEN cmc.id IS NOT NULL THEN true
            ELSE false
        END AS is_connected,
    cmc.status,
    cmc.page_id,
    cmc.page_name,
    cmc.instagram_account_id,
    cmc.instagram_username,
    cmc.catalog_id,
    cmc.pixel_id,
    cmc.token_expires_at,
        CASE
            WHEN cmc.token_expires_at IS NULL THEN NULL::text
            WHEN cmc.token_expires_at > now() THEN 'valid'::text
            ELSE 'expired'::text
        END AS token_status,
    cmc.created_at AS connected_at,
    cmc.last_token_refresh
   FROM companies c
     LEFT JOIN company_meta_credentials cmc ON c.id = cmc.company_id
  WHERE c.is_active = true;

-- Permissions

ALTER TABLE public.v_company_meta_status OWNER TO alex;
GRANT ALL ON TABLE public.v_company_meta_status TO alex;


-- public.v_customers_complete source

CREATE OR REPLACE VIEW public.v_customers_complete
AS SELECT c.id AS customer_id,
    c.company_id,
    c.email,
    c.phone,
    c.first_name,
    c.last_name,
    c.date_of_birth,
    c.address,
    c.city,
    c.state,
    c.country,
    c.postal_code,
    c.is_active,
    c.role,
    c.created_at AS customer_since,
    cl.id AS license_id,
    cl.license_number,
    cl.state_issued,
    cl.middle_name,
    cl.sex,
    cl.height,
    cl.eye_color,
    cl.issue_date,
    cl.expiration_date,
    cl.license_address,
    cl.license_city,
    cl.license_state,
    cl.is_verified AS license_verified,
    EXTRACT(year FROM age(CURRENT_DATE::timestamp with time zone, c.date_of_birth::timestamp with time zone)) AS age,
        CASE
            WHEN cl.expiration_date IS NULL THEN 'No License'::text
            WHEN cl.expiration_date < CURRENT_DATE THEN 'Expired'::text
            WHEN cl.expiration_date < (CURRENT_DATE + '30 days'::interval) THEN 'Expiring Soon'::text
            ELSE 'Valid'::text
        END AS license_status,
    cl.expiration_date - CURRENT_DATE AS days_until_expiration
   FROM customers c
     LEFT JOIN customer_licenses cl ON c.id = cl.customer_id;

-- Permissions

ALTER TABLE public.v_customers_complete OWNER TO alex;
GRANT ALL ON TABLE public.v_customers_complete TO alex;


-- public.v_customers_without_license source

CREATE OR REPLACE VIEW public.v_customers_without_license
AS SELECT c.id AS customer_id,
    c.company_id,
    c.email,
    c.phone,
    c.first_name,
    c.last_name,
    c.date_of_birth,
    c.created_at
   FROM customers c
     LEFT JOIN customer_licenses cl ON c.id = cl.customer_id
  WHERE cl.id IS NULL AND c.is_active = true AND c.role::text = 'customer'::text;

-- Permissions

ALTER TABLE public.v_customers_without_license OWNER TO alex;
GRANT ALL ON TABLE public.v_customers_without_license TO alex;


-- public.v_expired_licenses source

CREATE OR REPLACE VIEW public.v_expired_licenses
AS SELECT c.company_id,
    c.id AS customer_id,
    c.email,
    c.phone,
    c.first_name,
    c.last_name,
    cl.license_number,
    cl.state_issued,
    cl.expiration_date,
    CURRENT_DATE - cl.expiration_date AS days_expired
   FROM customers c
     JOIN customer_licenses cl ON c.id = cl.customer_id
  WHERE cl.expiration_date < CURRENT_DATE AND c.is_active = true
  ORDER BY cl.expiration_date;

-- Permissions

ALTER TABLE public.v_expired_licenses OWNER TO alex;
GRANT ALL ON TABLE public.v_expired_licenses TO alex;


-- public.v_external_vehicles_full source

CREATE OR REPLACE VIEW public.v_external_vehicles_full
AS SELECT ev.id AS link_id,
    ev.vehicle_id,
    ev.is_primary,
    ev.linked_at,
    ecv.id AS external_vehicle_id,
    ecv.external_id,
    ecv.name AS external_name,
    ecv.vin AS external_vin,
    ecv.license_plate AS external_plate,
    ecv.make,
    ecv.model,
    ecv.year,
    ecv.color,
    ecv.last_synced_at,
    ec.id AS external_company_id,
    ec.company_name
   FROM external_vehicles ev
     JOIN external_company_vehicles ecv ON ev.external_company_vehicle_id = ecv.id
     JOIN external_companies ec ON ecv.external_company_id = ec.id;

-- Permissions

ALTER TABLE public.v_external_vehicles_full OWNER TO alex;
GRANT ALL ON TABLE public.v_external_vehicles_full TO alex;


-- public.v_pending_transfers source

CREATE OR REPLACE VIEW public.v_pending_transfers
AS SELECT st.id AS transfer_id,
    st.company_id,
    st.booking_id,
    st.amount,
    st.status,
    st.stripe_transfer_id,
    st.created_at,
    st.updated_at,
    sc.stripe_account_id,
    c.company_name,
    c.email
   FROM stripe_transfers st
     JOIN companies c ON st.company_id = c.id
     LEFT JOIN stripe_company sc ON sc.company_id = c.id AND sc.settings_id = c.stripe_settings_id
  WHERE st.status::text = ANY (ARRAY['pending'::character varying, 'processing'::character varying, 'in_transit'::character varying]::text[]);

-- Permissions

ALTER TABLE public.v_pending_transfers OWNER TO alex;
GRANT ALL ON TABLE public.v_pending_transfers TO alex;


-- public.v_recent_scans source

CREATE OR REPLACE VIEW public.v_recent_scans
AS SELECT ls.scan_date,
    ls.scan_source,
    c.id AS customer_id,
    c.email,
    c.first_name,
    c.last_name,
    cl.license_number,
    ls.validation_passed,
    ls.was_expired,
    ls.age_at_scan
   FROM license_scans ls
     JOIN customers c ON ls.customer_id = c.id
     LEFT JOIN customer_licenses cl ON ls.customer_license_id = cl.id
  ORDER BY ls.scan_date DESC;

-- Permissions

ALTER TABLE public.v_recent_scans OWNER TO alex;
GRANT ALL ON TABLE public.v_recent_scans TO alex;


-- public.v_recent_vehicle_events source

CREATE OR REPLACE VIEW public.v_recent_vehicle_events
AS SELECT ve.id,
    v.license_plate,
    v.vin,
    ve.event_type,
    ve.severity,
    ve.latitude,
    ve.longitude,
    ve.address,
    ve.event_data,
    ve.event_time,
    ve.acknowledged_at
   FROM vehicle_events ve
     JOIN vehicles v ON v.id = ve.vehicle_id
  WHERE ve.event_time > (CURRENT_TIMESTAMP - '24:00:00'::interval)
  ORDER BY ve.event_time DESC;

-- Permissions

ALTER TABLE public.v_recent_vehicle_events OWNER TO alex;
GRANT ALL ON TABLE public.v_recent_vehicle_events TO alex;


-- public.v_recent_webhook_events source

CREATE OR REPLACE VIEW public.v_recent_webhook_events
AS SELECT we.id AS webhook_event_id,
    we.company_id,
    we.booking_id,
    we.stripe_event_id,
    we.event_type,
    we.processed,
    we.created_at,
    sc.stripe_account_id,
    c.company_name,
    c.email
   FROM webhook_events we
     JOIN companies c ON we.company_id = c.id
     LEFT JOIN stripe_company sc ON sc.company_id = c.id AND sc.settings_id = c.stripe_settings_id
  WHERE we.created_at >= (CURRENT_DATE - '30 days'::interval)
  ORDER BY we.created_at DESC;

-- Permissions

ALTER TABLE public.v_recent_webhook_events OWNER TO alex;
GRANT ALL ON TABLE public.v_recent_webhook_events TO alex;


-- public.v_security_deposits_status source

CREATE OR REPLACE VIEW public.v_security_deposits_status
AS SELECT b.id AS booking_id,
    b.booking_number,
    b.security_deposit,
    b.security_deposit_status,
    b.security_deposit_charged_amount,
    b.security_deposit_authorized_at,
    b.security_deposit_captured_at,
    b.security_deposit_released_at,
    b.security_deposit_refunded_at,
    b.security_deposit_capture_reason,
    b.security_deposit_payment_intent_id,
    c.company_name,
    (cust.first_name::text || ' '::text) || cust.last_name::text AS customer_name,
    cust.email AS customer_email,
    b.status AS booking_status,
        CASE
            WHEN b.security_deposit_released_at IS NOT NULL THEN 'Released'::text
            WHEN b.security_deposit_refunded_at IS NOT NULL THEN 'Refunded'::text
            WHEN b.security_deposit_captured_at IS NOT NULL THEN 'Captured'::text
            WHEN b.security_deposit_authorized_at IS NOT NULL THEN 'Authorized'::text
            ELSE 'Pending'::text
        END AS deposit_status
   FROM bookings b
     JOIN companies c ON b.company_id = c.id
     JOIN customers cust ON b.customer_id = cust.id
  WHERE b.security_deposit > 0::numeric
  ORDER BY b.created_at DESC;

COMMENT ON VIEW public.v_security_deposits_status IS 'Overview of security deposit statuses across bookings';

-- Permissions

ALTER TABLE public.v_security_deposits_status OWNER TO alex;
GRANT ALL ON TABLE public.v_security_deposits_status TO alex;


-- public.v_stripe_accounts_status source

CREATE OR REPLACE VIEW public.v_stripe_accounts_status
AS SELECT c.id AS company_id,
    c.company_name,
    c.email,
    c.subdomain,
    c.stripe_settings_id,
    sc.stripe_account_id,
    c.stripe_account_type,
    c.stripe_charges_enabled,
    c.stripe_payouts_enabled,
    c.stripe_details_submitted,
    c.stripe_onboarding_completed,
    c.stripe_last_sync_at,
    c.stripe_requirements_past_due,
    c.created_at,
    c.updated_at
   FROM companies c
     LEFT JOIN stripe_company sc ON sc.company_id = c.id AND sc.settings_id = c.stripe_settings_id;

-- Permissions

ALTER TABLE public.v_stripe_accounts_status OWNER TO alex;
GRANT ALL ON TABLE public.v_stripe_accounts_status TO alex;


-- public.v_unlinked_external_vehicles source

CREATE OR REPLACE VIEW public.v_unlinked_external_vehicles
AS SELECT ecv.id,
    ecv.external_company_id,
    ecv.external_id,
    ecv.name,
    ecv.vin,
    ecv.license_plate,
    ecv.make,
    ecv.model,
    ecv.year,
    ecv.color,
    ecv.notes,
    ecv.raw_data,
    ecv.is_active,
    ecv.last_synced_at,
    ecv.created_at,
    ecv.updated_at,
    ec.company_name
   FROM external_company_vehicles ecv
     JOIN external_companies ec ON ecv.external_company_id = ec.id
  WHERE NOT (ecv.id IN ( SELECT external_vehicles.external_company_vehicle_id
           FROM external_vehicles)) AND ecv.is_active = true;

-- Permissions

ALTER TABLE public.v_unlinked_external_vehicles OWNER TO alex;
GRANT ALL ON TABLE public.v_unlinked_external_vehicles TO alex;


-- public.v_vehicle_social_summary source

CREATE OR REPLACE VIEW public.v_vehicle_social_summary
AS SELECT v.id AS vehicle_id,
    v.company_id,
    m.make,
    m.model,
    m.year,
    v.license_plate,
    v.image_url,
    vsp_fb.post_id AS facebook_post_id,
    vsp_fb.permalink AS facebook_permalink,
    vsp_fb.updated_at AS facebook_updated_at,
    vsp_ig.post_id AS instagram_post_id,
    vsp_ig.permalink AS instagram_permalink,
    vsp_ig.updated_at AS instagram_updated_at
   FROM vehicles v
     JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
     JOIN models m ON vm.model_id = m.id
     LEFT JOIN vehicle_social_posts vsp_fb ON v.id = vsp_fb.vehicle_id AND vsp_fb.platform::text = 'Facebook'::text AND vsp_fb.is_active = true
     LEFT JOIN vehicle_social_posts vsp_ig ON v.id = vsp_ig.vehicle_id AND vsp_ig.platform::text = 'Instagram'::text AND vsp_ig.is_active = true
  WHERE v.status::text = 'Available'::text;

-- Permissions

ALTER TABLE public.v_vehicle_social_summary OWNER TO alex;
GRANT ALL ON TABLE public.v_vehicle_social_summary TO alex;


-- public.v_vehicle_tracking_summary source

CREATE OR REPLACE VIEW public.v_vehicle_tracking_summary
AS SELECT v.id AS vehicle_id,
    v.license_plate,
    v.vin,
    v.color AS vehicle_color,
    v.status AS rental_status,
    td.serial AS device_serial,
    td.device_name,
    td.is_active AS device_active,
    vts.latitude,
    vts.longitude,
    vts.address,
    vts.speed_kmh,
    vts.is_moving,
    vts.ignition_on,
    vts.starter_disabled,
    vts.voltage_mv,
    vts.device_timestamp AS last_location_time,
    vts.last_updated,
    EXTRACT(epoch FROM CURRENT_TIMESTAMP - vts.device_timestamp::timestamp with time zone) / 60::numeric AS minutes_since_update
   FROM vehicles v
     LEFT JOIN tracking_devices td ON td.vehicle_id = v.id AND td.is_active = true
     LEFT JOIN vehicle_tracking_status vts ON vts.vehicle_id = v.id;

COMMENT ON VIEW public.v_vehicle_tracking_summary IS 'Summary view of all vehicles with their current tracking status';

-- Permissions

ALTER TABLE public.v_vehicle_tracking_summary OWNER TO alex;
GRANT ALL ON TABLE public.v_vehicle_tracking_summary TO alex;


-- public.v_vehicles_with_tracker source

CREATE OR REPLACE VIEW public.v_vehicles_with_tracker
AS SELECT v.id AS vehicle_id,
    v.license_plate,
    v.vin,
    v.color,
    v.status,
    ecv.external_id AS tracker_serial,
    ecv.name AS tracker_name,
    ec.company_name AS tracker_company,
    ev.linked_at AS tracker_linked_at
   FROM vehicles v
     LEFT JOIN external_vehicles ev ON v.id = ev.vehicle_id AND ev.is_primary = true
     LEFT JOIN external_company_vehicles ecv ON ev.external_company_vehicle_id = ecv.id
     LEFT JOIN external_companies ec ON ecv.external_company_id = ec.id;

-- Permissions

ALTER TABLE public.v_vehicles_with_tracker OWNER TO alex;
GRANT ALL ON TABLE public.v_vehicles_with_tracker TO alex;



-- DROP FUNCTION public.calculate_platform_fee();

CREATE OR REPLACE FUNCTION public.calculate_platform_fee()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_company_fee_percentage DECIMAL(5,2);
BEGIN
    -- Only calculate if total_amount changed or is new
    IF (TG_OP = 'INSERT' OR NEW.total_amount != OLD.total_amount) THEN
        -- Get company fee percentage
        SELECT COALESCE(platform_fee_percentage, 10.00) 
        INTO v_company_fee_percentage
        FROM companies
        WHERE id = NEW.company_id;
        
        -- Calculate platform fee and net amount
        NEW.platform_fee_amount := ROUND(NEW.total_amount * (v_company_fee_percentage / 100), 2);
        NEW.net_amount := NEW.total_amount - NEW.platform_fee_amount;
    END IF;
    
    RETURN NEW;
END;
$function$
;

COMMENT ON FUNCTION public.calculate_platform_fee() IS 'Auto-calculates platform fee and net amount on bookings';

-- Permissions

ALTER FUNCTION public.calculate_platform_fee() OWNER TO alex;
GRANT ALL ON FUNCTION public.calculate_platform_fee() TO public;
GRANT ALL ON FUNCTION public.calculate_platform_fee() TO alex;

-- DROP FUNCTION public.check_insurance_validity(uuid);

CREATE OR REPLACE FUNCTION public.check_insurance_validity(p_customer_id uuid)
 RETURNS TABLE(is_valid boolean, expiration_date date, days_remaining integer, insurance_id integer)
 LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN QUERY
    SELECT 
        (aic.expiration_date >= CURRENT_DATE) as is_valid,
        aic.expiration_date,
        (aic.expiration_date - CURRENT_DATE)::INTEGER as days_remaining,
        aic.id as insurance_id
    FROM auto_insurance_cards aic
    WHERE aic.customer_id = p_customer_id
    ORDER BY aic.created_at DESC
    LIMIT 1;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.check_insurance_validity(uuid) OWNER TO alex;
GRANT ALL ON FUNCTION public.check_insurance_validity(uuid) TO alex;

-- DROP FUNCTION public.cleanup_expired_refresh_tokens();

CREATE OR REPLACE FUNCTION public.cleanup_expired_refresh_tokens()
 RETURNS integer
 LANGUAGE plpgsql
AS $function$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM refresh_tokens 
    WHERE expires_at < NOW() - INTERVAL '7 days'
       OR (revoked_at IS NOT NULL AND revoked_at < NOW() - INTERVAL '7 days');
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$function$
;

COMMENT ON FUNCTION public.cleanup_expired_refresh_tokens() IS 'Removes expired and revoked refresh tokens older than 7 days. Call periodically.';

-- Permissions

ALTER FUNCTION public.cleanup_expired_refresh_tokens() OWNER TO alex;
GRANT ALL ON FUNCTION public.cleanup_expired_refresh_tokens() TO alex;

-- DROP FUNCTION public.cleanup_old_locations(int4);

CREATE OR REPLACE FUNCTION public.cleanup_old_locations(p_days_to_keep integer DEFAULT 90)
 RETURNS integer
 LANGUAGE plpgsql
AS $function$
DECLARE
    deleted_count int;
BEGIN
    DELETE FROM public.vehicle_locations
    WHERE device_timestamp < CURRENT_TIMESTAMP - (p_days_to_keep || ' days')::interval;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$function$
;

COMMENT ON FUNCTION public.cleanup_old_locations(int4) IS 'Delete location records older than specified days';

-- Permissions

ALTER FUNCTION public.cleanup_old_locations(int4) OWNER TO alex;
GRANT ALL ON FUNCTION public.cleanup_old_locations(int4) TO alex;

-- DROP FUNCTION public.create_rental_agreement(uuid, uuid, uuid, uuid, varchar, varchar, varchar, varchar, text, varchar, varchar, varchar, varchar, timestamptz, varchar, timestamptz, varchar, numeric, numeric, varchar, text, varchar, text, text, text, text, varchar, text, varchar);

CREATE OR REPLACE FUNCTION public.create_rental_agreement(p_company_id uuid, p_booking_id uuid, p_customer_id uuid, p_vehicle_id uuid, p_language character varying, p_customer_name character varying, p_customer_email character varying, p_customer_phone character varying, p_customer_address text, p_driver_license_number character varying, p_driver_license_state character varying, p_vehicle_name character varying, p_vehicle_plate character varying, p_pickup_date timestamp with time zone, p_pickup_location character varying, p_return_date timestamp with time zone, p_return_location character varying, p_rental_amount numeric, p_deposit_amount numeric, p_currency character varying, p_signature_image text, p_signature_hash character varying, p_terms_text text, p_non_refundable_text text, p_damage_policy_text text, p_card_authorization_text text, p_ip_address character varying, p_user_agent text, p_timezone character varying)
 RETURNS uuid
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_agreement_id UUID;
    v_agreement_number VARCHAR(50);
    v_signed_at TIMESTAMPTZ;
BEGIN
    v_agreement_number := generate_agreement_number(p_company_id);
    v_signed_at := NOW();
    
    INSERT INTO public.rental_agreements (
        company_id,
        booking_id,
        customer_id,
        vehicle_id,
        agreement_number,
        language,
        customer_name,
        customer_email,
        customer_phone,
        customer_address,
        driver_license_number,
        driver_license_state,
        vehicle_name,
        vehicle_plate,
        pickup_date,
        pickup_location,
        return_date,
        return_location,
        rental_amount,
        deposit_amount,
        currency,
        signature_image,
        signature_hash,
        terms_accepted_at,
        non_refundable_accepted_at,
        damage_policy_accepted_at,
        card_authorization_accepted_at,
        terms_text,
        non_refundable_text,
        damage_policy_text,
        card_authorization_text,
        signed_at,
        ip_address,
        user_agent,
        timezone
    ) VALUES (
        p_company_id,
        p_booking_id,
        p_customer_id,
        p_vehicle_id,
        v_agreement_number,
        COALESCE(p_language, 'en'),
        p_customer_name,
        p_customer_email,
        p_customer_phone,
        p_customer_address,
        p_driver_license_number,
        p_driver_license_state,
        p_vehicle_name,
        p_vehicle_plate,
        p_pickup_date,
        p_pickup_location,
        p_return_date,
        p_return_location,
        p_rental_amount,
        COALESCE(p_deposit_amount, 0),
        COALESCE(p_currency, 'USD'),
        p_signature_image,
        p_signature_hash,
        v_signed_at,
        v_signed_at,
        v_signed_at,
        v_signed_at,
        p_terms_text,
        p_non_refundable_text,
        p_damage_policy_text,
        p_card_authorization_text,
        v_signed_at,
        p_ip_address,
        p_user_agent,
        p_timezone
    )
    RETURNING id INTO v_agreement_id;
    
    -- Log creation
    INSERT INTO public.rental_agreement_audit_log (agreement_id, action, performed_by, ip_address, user_agent)
    VALUES (v_agreement_id, 'created', p_customer_email, p_ip_address, p_user_agent);
    
    RETURN v_agreement_id;
END;
$function$
;

COMMENT ON FUNCTION public.create_rental_agreement(uuid, uuid, uuid, uuid, varchar, varchar, varchar, varchar, text, varchar, varchar, varchar, varchar, timestamptz, varchar, timestamptz, varchar, numeric, numeric, varchar, text, varchar, text, text, text, text, varchar, text, varchar) IS 'Creates a new rental agreement with all required data and audit logging';

-- Permissions

ALTER FUNCTION public.create_rental_agreement(uuid, uuid, uuid, uuid, varchar, varchar, varchar, varchar, text, varchar, varchar, varchar, varchar, timestamptz, varchar, timestamptz, varchar, numeric, numeric, varchar, text, varchar, text, text, text, text, varchar, text, varchar) OWNER TO alex;
GRANT ALL ON FUNCTION public.create_rental_agreement(uuid, uuid, uuid, uuid, varchar, varchar, varchar, varchar, text, varchar, varchar, varchar, varchar, timestamptz, varchar, timestamptz, varchar, numeric, numeric, varchar, text, varchar, text, text, text, text, varchar, text, varchar) TO alex;

-- DROP FUNCTION public.delete_expired_instagram_conversations();

CREATE OR REPLACE FUNCTION public.delete_expired_instagram_conversations()
 RETURNS integer
 LANGUAGE plpgsql
AS $function$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM instagram_conversations 
    WHERE expires_at < NOW() - INTERVAL '7 days';
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.delete_expired_instagram_conversations() OWNER TO alex;
GRANT ALL ON FUNCTION public.delete_expired_instagram_conversations() TO alex;

-- DROP FUNCTION public.delete_test_companies(text);

CREATE OR REPLACE FUNCTION public.delete_test_companies(p_email_prefix text DEFAULT 'test%'::text)
 RETURNS TABLE(step text, deleted_count bigint)
 LANGUAGE plpgsql
AS $function$
DECLARE
  v_company_ids uuid[];
  v_cnt bigint;
BEGIN
  -- собираем компании под удаление
  SELECT array_agg(id)
  INTO v_company_ids
  FROM public.companies
  WHERE email LIKE p_email_prefix;

  IF v_company_ids IS NULL OR array_length(v_company_ids, 1) IS NULL THEN
    step := 'no companies matched';
    deleted_count := 0;
    RETURN NEXT;
    RETURN;
  END IF;

  --------------------------------------------------------------------
  -- ВАЖНО: сначала удаляем то, что ссылается НЕ ТОЛЬКО по company_id,
  -- а по customer_id / booking_id / rental_id и т.п.
  -- (иначе получите 23503 как у вас с payments -> customers)
  --------------------------------------------------------------------

  -- payments обычно ссылаются на customers (и/или bookings/rentals)
  DELETE FROM public.payments p
  USING public.customers cu
  WHERE cu.company_id = ANY (v_company_ids)
    AND p.customer_id = cu.id;
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'payments by customer_id'; deleted_count := v_cnt; RETURN NEXT;

  -- webhook_events (если есть ссылки на другие сущности — добавляйте аналогично)
  DELETE FROM public.webhook_events WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'webhook_events'; deleted_count := v_cnt; RETURN NEXT;

  -- dispute/refund analytics
  DELETE FROM public.dispute_analytics WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'dispute_analytics'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.refund_analytics WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'refund_analytics'; deleted_count := v_cnt; RETURN NEXT;

  -- bookings/rentals зависимые таблицы (если есть ещё таблицы, которые ссылаются на bookings/rentals, удаляйте их ДО)
  DELETE FROM public.booking_tokens WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'booking_tokens'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.reviews WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'reviews'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.rental_agreements WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'rental_agreements'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.rentals WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'rentals'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.bookings WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'bookings'; deleted_count := v_cnt; RETURN NEXT;

  -- vehicles и связанные
  DELETE FROM public.vehicle_social_posts WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'vehicle_social_posts'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.vehicles WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'vehicles'; deleted_count := v_cnt; RETURN NEXT;

  -- остальные таблицы, которые прямо по company_id
  DELETE FROM public.additional_services WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'additional_services'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.company_services WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'company_services'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.locations WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'locations'; deleted_count := v_cnt; RETURN NEXT;

  -- license_scans no longer has company_id (personal audit records)
  -- DELETE FROM public.license_scans WHERE company_id = ANY (v_company_ids);
  -- GET DIAGNOSTICS v_cnt = ROW_COUNT;
  -- step := 'license_scans'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.company_location WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'company_location'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.company_meta_credentials WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'company_meta_credentials'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.company_mode WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'company_mode'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.finders_list WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'finders_list'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.geofences WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'geofences'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.scheduled_posts WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'scheduled_posts'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.social_post_templates WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'social_post_templates'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.social_post_analytics WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'social_post_analytics'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.company_auto_post_settings WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'company_auto_post_settings'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.stripe_balance_transactions WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'stripe_balance_transactions'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.stripe_transfers WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'stripe_transfers'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.stripe_payout_records WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'stripe_payout_records'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.stripe_onboarding_sessions WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'stripe_onboarding_sessions'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.stripe_account_capabilities WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'stripe_account_capabilities'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.stripe_company WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'stripe_company'; deleted_count := v_cnt; RETURN NEXT;

  DELETE FROM public.violations WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'violations'; deleted_count := v_cnt; RETURN NEXT;

  -- violations_requests у вас ON DELETE SET NULL, но если хотите “в ноль”, можно удалить:
  DELETE FROM public.violations_requests WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'violations_requests'; deleted_count := v_cnt; RETURN NEXT;

  -- external_companies у вас SET NULL — можно удалить или занулить. Тут удаление строк:
  DELETE FROM public.external_companies WHERE rental_company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'external_companies'; deleted_count := v_cnt; RETURN NEXT;

  --------------------------------------------------------------------
  -- Теперь customers в конце (когда всё, что на них ссылается, уже удалено)
  --------------------------------------------------------------------
  DELETE FROM public.customers WHERE company_id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'customers'; deleted_count := v_cnt; RETURN NEXT;

  --------------------------------------------------------------------
  -- И наконец сами companies
  --------------------------------------------------------------------
  DELETE FROM public.companies WHERE id = ANY (v_company_ids);
  GET DIAGNOSTICS v_cnt = ROW_COUNT;
  step := 'companies'; deleted_count := v_cnt; RETURN NEXT;

END;
$function$
;

-- Permissions

ALTER FUNCTION public.delete_test_companies(text) OWNER TO alex;
GRANT ALL ON FUNCTION public.delete_test_companies(text) TO alex;

-- DROP FUNCTION public.ensure_uppercase_models();

CREATE OR REPLACE FUNCTION public.ensure_uppercase_models()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.make := UPPER(NEW.make);
    NEW.model := UPPER(NEW.model);
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.ensure_uppercase_models() OWNER TO alex;
GRANT ALL ON FUNCTION public.ensure_uppercase_models() TO alex;

-- DROP FUNCTION public.generate_agreement_number(uuid);

CREATE OR REPLACE FUNCTION public.generate_agreement_number(p_company_id uuid)
 RETURNS character varying
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_year INT;
    v_sequence INT;
    v_number VARCHAR(50);
BEGIN
    v_year := EXTRACT(YEAR FROM NOW());
    
    -- Get next sequence for this company and year
    SELECT COALESCE(MAX(
        CAST(SUBSTRING(agreement_number FROM 'AGR-\d{4}-(\d+)') AS INT)
    ), 0) + 1
    INTO v_sequence
    FROM public.rental_agreements
    WHERE company_id = p_company_id
    AND agreement_number LIKE 'AGR-' || v_year || '-%';
    
    v_number := 'AGR-' || v_year || '-' || LPAD(v_sequence::TEXT, 6, '0');
    
    RETURN v_number;
END;
$function$
;

COMMENT ON FUNCTION public.generate_agreement_number(uuid) IS 'Generates sequential agreement number per company per year';

-- Permissions

ALTER FUNCTION public.generate_agreement_number(uuid) OWNER TO alex;
GRANT ALL ON FUNCTION public.generate_agreement_number(uuid) TO alex;

-- DROP FUNCTION public.get_available_vehicles_by_company(uuid, timestamp, timestamp, uuid);

CREATE OR REPLACE FUNCTION public.get_available_vehicles_by_company(p_company_id uuid, p_pickup_datetime timestamp without time zone, p_return_datetime timestamp without time zone, p_location_id uuid DEFAULT NULL::uuid)
 RETURNS TABLE(model_id uuid, make character varying, model character varying, fuel_type character varying, transmission character varying, seats integer, category_id uuid, category_name character varying, min_daily_rate numeric, max_daily_rate numeric, avg_daily_rate numeric, available_count bigint, total_available_vehicles bigint, all_vehicles_count bigint, years_available text, available_colors text[], available_locations text[], sample_image_url text, model_features text[])
 LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN QUERY
    WITH booked_vehicles AS (
        SELECT DISTINCT b.vehicle_id
        FROM bookings b
        WHERE b.company_id = p_company_id
          AND b.status NOT IN ('Cancelled', 'Completed', 'NoShow')
          AND NOT (
              p_return_datetime <= (b.pickup_date + COALESCE(b.pickup_time, '10:00')::TIME)
              OR
              p_pickup_datetime >= (b.return_date + COALESCE(b.return_time, '22:00')::TIME)
          )
    ),
    available_vehicles AS (
        SELECT 
            v.id AS vehicle_id,
            vm.model_id,
            m.make::VARCHAR,
            m.model::VARCHAR,
            m.fuel_type::VARCHAR,
            m.transmission::VARCHAR,
            m.seats::INTEGER,
            m.year,
            COALESCE(vm.daily_rate, 0) AS daily_rate,
            m.features,
            m.category_id,
            c.category_name::VARCHAR,
            v.color::TEXT,
            v.location_id,
            v.image_url::TEXT,
            l.location_name::TEXT
        FROM vehicles v
        JOIN vehicle_model vm ON v.vehicle_model_id = vm.id
        JOIN models m ON vm.model_id = m.id
        LEFT JOIN vehicle_categories c ON m.category_id = c.id
        LEFT JOIN locations l ON v.location_id = l.id
        WHERE v.company_id = p_company_id
          AND v.status = 'Available'
          AND (p_location_id IS NULL OR v.location_id = p_location_id)
          AND v.id NOT IN (SELECT vehicle_id FROM booked_vehicles)
    )
    SELECT 
        av.model_id,
        av.make::VARCHAR,
        av.model::VARCHAR,
        av.fuel_type::VARCHAR,
        av.transmission::VARCHAR,
        av.seats::INTEGER,
        av.category_id,
        MAX(av.category_name)::VARCHAR AS category_name,
        MIN(av.daily_rate)::DECIMAL AS min_daily_rate,
        MAX(av.daily_rate)::DECIMAL AS max_daily_rate,
        AVG(av.daily_rate)::DECIMAL AS avg_daily_rate,
        COUNT(DISTINCT av.vehicle_id)::BIGINT AS available_count,
        COUNT(DISTINCT av.vehicle_id)::BIGINT AS total_available_vehicles,
        (SELECT COUNT(*) 
         FROM vehicles v2 
         JOIN vehicle_model vm2 ON v2.vehicle_model_id = vm2.id 
         WHERE vm2.model_id = av.model_id 
           AND v2.company_id = p_company_id
           AND (p_location_id IS NULL OR v2.location_id = p_location_id)
        )::BIGINT AS all_vehicles_count,
        STRING_AGG(DISTINCT av.year::TEXT, ', ' ORDER BY av.year::TEXT)::TEXT AS years_available,
        (ARRAY_AGG(DISTINCT av.color) FILTER (WHERE av.color IS NOT NULL))::TEXT[] AS available_colors,
        (ARRAY_AGG(DISTINCT av.location_name) FILTER (WHERE av.location_name IS NOT NULL))::TEXT[] AS available_locations,
        MAX(av.image_url)::TEXT AS sample_image_url,
        (SELECT features::TEXT[] FROM models WHERE id = av.model_id LIMIT 1) AS model_features
    FROM available_vehicles av
    GROUP BY av.model_id, av.make, av.model, av.fuel_type, av.transmission, av.seats, av.category_id
    ORDER BY av.make, av.model;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.get_available_vehicles_by_company(uuid, timestamp, timestamp, uuid) OWNER TO alex;
GRANT ALL ON FUNCTION public.get_available_vehicles_by_company(uuid, timestamp, timestamp, uuid) TO alex;

-- DROP FUNCTION public.get_company_stripe_status(uuid);

CREATE OR REPLACE FUNCTION public.get_company_stripe_status(p_company_id uuid)
 RETURNS TABLE(can_accept_payments boolean, can_receive_payouts boolean, is_onboarding_complete boolean, has_past_due_requirements boolean, account_status character varying, requirements_count integer)
 LANGUAGE plpgsql
AS $function$
BEGIN
    RETURN QUERY
    SELECT 
        c.stripe_charges_enabled,
        c.stripe_payouts_enabled,
        c.stripe_onboarding_completed,
        (c.stripe_requirements_past_due IS NOT NULL AND 
         array_length(c.stripe_requirements_past_due, 1) > 0) as has_past_due,
        CASE 
            WHEN c.stripe_account_id IS NULL THEN 'not_started'::VARCHAR(50)
            WHEN NOT c.stripe_onboarding_completed THEN 'onboarding'::VARCHAR(50)
            WHEN c.stripe_requirements_past_due IS NOT NULL 
                AND array_length(c.stripe_requirements_past_due, 1) > 0 THEN 'past_due'::VARCHAR(50)
            WHEN NOT c.stripe_charges_enabled OR NOT c.stripe_payouts_enabled THEN 'restricted'::VARCHAR(50)
            ELSE 'active'::VARCHAR(50)
        END as status,
        COALESCE(array_length(c.stripe_requirements_currently_due, 1), 0) +
        COALESCE(array_length(c.stripe_requirements_past_due, 1), 0) as req_count
    FROM companies c
    WHERE c.id = p_company_id;
END;
$function$
;

COMMENT ON FUNCTION public.get_company_stripe_status(uuid) IS 'Returns comprehensive Stripe status for a company';

-- Permissions

ALTER FUNCTION public.get_company_stripe_status(uuid) OWNER TO alex;
GRANT ALL ON FUNCTION public.get_company_stripe_status(uuid) TO public;
GRANT ALL ON FUNCTION public.get_company_stripe_status(uuid) TO alex;

-- DROP FUNCTION public.is_license_already_used(uuid, varchar, varchar, uuid);

CREATE OR REPLACE FUNCTION public.is_license_already_used(p_company_id uuid, p_license_number character varying, p_state_issued character varying, p_exclude_customer_id uuid DEFAULT NULL::uuid)
 RETURNS boolean
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_count INTEGER;
BEGIN
    SELECT COUNT(*) INTO v_count
    FROM customer_licenses
    WHERE company_id = p_company_id
      AND license_number = p_license_number
      AND state_issued = p_state_issued
      AND (p_exclude_customer_id IS NULL OR customer_id != p_exclude_customer_id);
    
    RETURN v_count > 0;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.is_license_already_used(uuid, varchar, varchar, uuid) OWNER TO alex;
GRANT ALL ON FUNCTION public.is_license_already_used(uuid, varchar, varchar, uuid) TO alex;

-- DROP FUNCTION public.log_stripe_transfer(uuid, uuid, varchar, varchar, numeric, numeric, varchar, varchar);

CREATE OR REPLACE FUNCTION public.log_stripe_transfer(p_booking_id uuid, p_company_id uuid, p_stripe_transfer_id character varying, p_stripe_payment_intent_id character varying, p_amount numeric, p_platform_fee numeric, p_destination_account_id character varying, p_currency character varying DEFAULT 'USD'::character varying)
 RETURNS uuid
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_transfer_id UUID;
BEGIN
    INSERT INTO stripe_transfers (
        booking_id,
        company_id,
        stripe_transfer_id,
        stripe_payment_intent_id,
        amount,
        currency,
        platform_fee,
        net_amount,
        destination_account_id,
        status
    ) VALUES (
        p_booking_id,
        p_company_id,
        p_stripe_transfer_id,
        p_stripe_payment_intent_id,
        p_amount,
        p_currency,
        p_platform_fee,
        p_amount - p_platform_fee,
        p_destination_account_id,
        'pending'
    )
    RETURNING id INTO v_transfer_id;
    
    -- Update booking with transfer ID
    UPDATE bookings
    SET stripe_transfer_id = p_stripe_transfer_id
    WHERE id = p_booking_id;
    
    RETURN v_transfer_id;
END;
$function$
;

COMMENT ON FUNCTION public.log_stripe_transfer(uuid, uuid, varchar, varchar, numeric, numeric, varchar, varchar) IS 'Creates transfer record and updates booking';

-- Permissions

ALTER FUNCTION public.log_stripe_transfer(uuid, uuid, varchar, varchar, numeric, numeric, varchar, varchar) OWNER TO alex;
GRANT ALL ON FUNCTION public.log_stripe_transfer(uuid, uuid, varchar, varchar, numeric, numeric, varchar, varchar) TO public;
GRANT ALL ON FUNCTION public.log_stripe_transfer(uuid, uuid, varchar, varchar, numeric, numeric, varchar, varchar) TO alex;

-- DROP FUNCTION public.set_settings_updated_at();

CREATE OR REPLACE FUNCTION public.set_settings_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.set_settings_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.set_settings_updated_at() TO alex;

-- DROP FUNCTION public.trg_set_currencies_updated_at();

CREATE OR REPLACE FUNCTION public.trg_set_currencies_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at := NOW();
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.trg_set_currencies_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.trg_set_currencies_updated_at() TO alex;

-- DROP FUNCTION public.update_agreement_disputes_updated_at();

CREATE OR REPLACE FUNCTION public.update_agreement_disputes_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.update_agreement_disputes_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_agreement_disputes_updated_at() TO alex;

-- DROP FUNCTION public.update_capabilities_updated_at();

CREATE OR REPLACE FUNCTION public.update_capabilities_updated_at()
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

ALTER FUNCTION public.update_capabilities_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_capabilities_updated_at() TO alex;

-- DROP FUNCTION public.update_company_meta_credentials_updated_at();

CREATE OR REPLACE FUNCTION public.update_company_meta_credentials_updated_at()
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

ALTER FUNCTION public.update_company_meta_credentials_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_company_meta_credentials_updated_at() TO alex;

-- DROP FUNCTION public.update_customer_licenses_updated_at();

CREATE OR REPLACE FUNCTION public.update_customer_licenses_updated_at()
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

ALTER FUNCTION public.update_customer_licenses_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_customer_licenses_updated_at() TO alex;

-- DROP FUNCTION public.update_external_updated_at();

CREATE OR REPLACE FUNCTION public.update_external_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.update_external_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_external_updated_at() TO alex;

-- DROP FUNCTION public.update_finders_list_updated_at();

CREATE OR REPLACE FUNCTION public.update_finders_list_updated_at()
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

ALTER FUNCTION public.update_finders_list_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_finders_list_updated_at() TO alex;

-- DROP FUNCTION public.update_insurance_expired_status();

CREATE OR REPLACE FUNCTION public.update_insurance_expired_status()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.is_expired = (NEW.expiration_date < CURRENT_DATE);
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.update_insurance_expired_status() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_insurance_expired_status() TO alex;

-- DROP FUNCTION public.update_insurance_updated_at();

CREATE OR REPLACE FUNCTION public.update_insurance_updated_at()
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

ALTER FUNCTION public.update_insurance_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_insurance_updated_at() TO alex;

-- DROP FUNCTION public.update_onboarding_sessions_updated_at();

CREATE OR REPLACE FUNCTION public.update_onboarding_sessions_updated_at()
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

ALTER FUNCTION public.update_onboarding_sessions_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_onboarding_sessions_updated_at() TO alex;

-- DROP FUNCTION public.update_payouts_updated_at();

CREATE OR REPLACE FUNCTION public.update_payouts_updated_at()
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

ALTER FUNCTION public.update_payouts_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_payouts_updated_at() TO alex;

-- DROP FUNCTION public.update_rental_agreements_updated_at();

CREATE OR REPLACE FUNCTION public.update_rental_agreements_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.update_rental_agreements_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_rental_agreements_updated_at() TO alex;

-- DROP FUNCTION public.update_stripe_account_status(varchar, bool, bool, bool, _text, _text, _text, varchar);

CREATE OR REPLACE FUNCTION public.update_stripe_account_status(p_stripe_account_id character varying, p_charges_enabled boolean, p_payouts_enabled boolean, p_details_submitted boolean, p_requirements_currently_due text[] DEFAULT NULL::text[], p_requirements_eventually_due text[] DEFAULT NULL::text[], p_requirements_past_due text[] DEFAULT NULL::text[], p_disabled_reason character varying DEFAULT NULL::character varying)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
BEGIN
    UPDATE companies
    SET 
        stripe_charges_enabled = p_charges_enabled,
        stripe_payouts_enabled = p_payouts_enabled,
        stripe_details_submitted = p_details_submitted,
        stripe_onboarding_completed = (p_charges_enabled AND p_payouts_enabled AND p_details_submitted),
        stripe_requirements_currently_due = p_requirements_currently_due,
        stripe_requirements_eventually_due = p_requirements_eventually_due,
        stripe_requirements_past_due = p_requirements_past_due,
        stripe_requirements_disabled_reason = p_disabled_reason,
        stripe_last_sync_at = NOW(),
        updated_at = NOW()
    WHERE stripe_account_id = p_stripe_account_id;
    
    IF NOT FOUND THEN
        RAISE NOTICE 'No company found with stripe_account_id: %', p_stripe_account_id;
    END IF;
END;
$function$
;

COMMENT ON FUNCTION public.update_stripe_account_status(varchar, bool, bool, bool, _text, _text, _text, varchar) IS 'Updates company Stripe account status from webhook data';

-- Permissions

ALTER FUNCTION public.update_stripe_account_status(varchar, bool, bool, bool, _text, _text, _text, varchar) OWNER TO alex;
GRANT ALL ON FUNCTION public.update_stripe_account_status(varchar, bool, bool, bool, _text, _text, _text, varchar) TO public;
GRANT ALL ON FUNCTION public.update_stripe_account_status(varchar, bool, bool, bool, _text, _text, _text, varchar) TO alex;

-- DROP FUNCTION public.update_transfers_updated_at();

CREATE OR REPLACE FUNCTION public.update_transfers_updated_at()
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

ALTER FUNCTION public.update_transfers_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_transfers_updated_at() TO alex;

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

-- DROP FUNCTION public.update_vehicle_social_posts_updated_at();

CREATE OR REPLACE FUNCTION public.update_vehicle_social_posts_updated_at()
 RETURNS trigger
 LANGUAGE plpgsql
AS $function$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.update_vehicle_social_posts_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_vehicle_social_posts_updated_at() TO alex;

-- DROP FUNCTION public.update_violations_updated_at();

CREATE OR REPLACE FUNCTION public.update_violations_updated_at()
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

ALTER FUNCTION public.update_violations_updated_at() OWNER TO alex;
GRANT ALL ON FUNCTION public.update_violations_updated_at() TO alex;

-- DROP FUNCTION public.upsert_customer_license_with_sync(uuid, uuid, varchar, varchar, varchar, varchar, varchar, varchar, varchar, date, date, varchar, varchar, varchar, varchar, varchar, varchar, varchar, text, varchar, varchar, date, bool, uuid);

CREATE OR REPLACE FUNCTION public.upsert_customer_license_with_sync(p_customer_id uuid, p_company_id uuid, p_license_number character varying, p_state_issued character varying, p_country_issued character varying, p_middle_name character varying, p_sex character varying, p_height character varying, p_eye_color character varying, p_issue_date date, p_expiration_date date, p_license_address character varying, p_license_city character varying, p_license_state character varying, p_license_postal_code character varying, p_license_country character varying, p_restriction_code character varying, p_endorsements character varying, p_raw_barcode_data text, p_first_name character varying, p_last_name character varying, p_date_of_birth date, p_sync_customer_data boolean DEFAULT true, p_created_by uuid DEFAULT NULL::uuid)
 RETURNS TABLE(license_id uuid, fields_updated text[])
 LANGUAGE plpgsql
AS $function$
DECLARE
    v_license_id UUID;
    v_updated_fields TEXT[] := ARRAY[]::TEXT[];
BEGIN
    INSERT INTO customer_licenses (
        customer_id, company_id,
        license_number, state_issued, country_issued,
        middle_name, sex, height, eye_color,
        issue_date, expiration_date,
        license_address, license_city, license_state, license_postal_code, license_country,
        restriction_code, endorsements, raw_barcode_data,
        is_verified, verification_date, verification_method, created_by
    ) VALUES (
        p_customer_id, p_company_id,
        p_license_number, p_state_issued, COALESCE(p_country_issued, 'US'),
        p_middle_name, p_sex, p_height, p_eye_color,
        p_issue_date, p_expiration_date,
        p_license_address, p_license_city, p_license_state, p_license_postal_code, p_license_country,
        p_restriction_code, p_endorsements, p_raw_barcode_data,
        true, CURRENT_TIMESTAMP, 'license_scan', p_created_by
    )
    ON CONFLICT (customer_id) 
    DO UPDATE SET
        license_number = EXCLUDED.license_number,
        state_issued = EXCLUDED.state_issued,
        country_issued = EXCLUDED.country_issued,
        middle_name = EXCLUDED.middle_name,
        sex = EXCLUDED.sex,
        height = EXCLUDED.height,
        eye_color = EXCLUDED.eye_color,
        issue_date = EXCLUDED.issue_date,
        expiration_date = EXCLUDED.expiration_date,
        license_address = EXCLUDED.license_address,
        license_city = EXCLUDED.license_city,
        license_state = EXCLUDED.license_state,
        license_postal_code = EXCLUDED.license_postal_code,
        license_country = EXCLUDED.license_country,
        restriction_code = EXCLUDED.restriction_code,
        endorsements = EXCLUDED.endorsements,
        raw_barcode_data = EXCLUDED.raw_barcode_data,
        is_verified = true,
        verification_date = CURRENT_TIMESTAMP,
        updated_at = CURRENT_TIMESTAMP,
        updated_by = p_created_by
    RETURNING id INTO v_license_id;
    
    IF p_sync_customer_data THEN
        UPDATE customers SET
            first_name = CASE WHEN first_name IS NULL OR first_name = '' THEN p_first_name ELSE first_name END,
            last_name = CASE WHEN last_name IS NULL OR last_name = '' THEN p_last_name ELSE last_name END,
            date_of_birth = CASE WHEN date_of_birth IS NULL THEN p_date_of_birth ELSE date_of_birth END,
            updated_at = CURRENT_TIMESTAMP
        WHERE id = p_customer_id
        RETURNING 
            CASE WHEN first_name = p_first_name THEN 'first_name' END,
            CASE WHEN last_name = p_last_name THEN 'last_name' END,
            CASE WHEN date_of_birth = p_date_of_birth THEN 'date_of_birth' END
        INTO v_updated_fields;
        
        v_updated_fields := ARRAY(SELECT unnest(v_updated_fields) WHERE unnest IS NOT NULL);
    END IF;
    
    RETURN QUERY SELECT v_license_id, v_updated_fields;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.upsert_customer_license_with_sync(uuid, uuid, varchar, varchar, varchar, varchar, varchar, varchar, varchar, date, date, varchar, varchar, varchar, varchar, varchar, varchar, varchar, text, varchar, varchar, date, bool, uuid) OWNER TO alex;
GRANT ALL ON FUNCTION public.upsert_customer_license_with_sync(uuid, uuid, varchar, varchar, varchar, varchar, varchar, varchar, varchar, date, date, varchar, varchar, varchar, varchar, varchar, varchar, varchar, text, varchar, varchar, date, bool, uuid) TO alex;

-- DROP FUNCTION public.upsert_vehicle_tracking_status(uuid, varchar, numeric, numeric, int2, int2, int2, int4, int8, timestamp);

CREATE OR REPLACE FUNCTION public.upsert_vehicle_tracking_status(p_vehicle_id uuid, p_device_serial character varying, p_latitude numeric, p_longitude numeric, p_speed_kmh smallint, p_heading smallint, p_location_type_id smallint, p_voltage_mv integer, p_odometer_meters bigint, p_device_timestamp timestamp without time zone)
 RETURNS void
 LANGUAGE plpgsql
AS $function$
BEGIN
    INSERT INTO public.vehicle_tracking_status (
        vehicle_id, device_serial, latitude, longitude, speed_kmh, heading,
        location_type_id, voltage_mv, odometer_meters, is_moving, ignition_on,
        starter_disabled, device_timestamp, last_updated
    ) VALUES (
        p_vehicle_id, p_device_serial, p_latitude, p_longitude, p_speed_kmh, p_heading,
        p_location_type_id, p_voltage_mv, p_odometer_meters,
        p_location_type_id = 5,  -- is_moving
        p_location_type_id IN (4, 5),  -- ignition_on
        p_location_type_id = 24,  -- starter_disabled
        p_device_timestamp, CURRENT_TIMESTAMP
    )
    ON CONFLICT (vehicle_id) DO UPDATE SET
        device_serial = EXCLUDED.device_serial,
        latitude = EXCLUDED.latitude,
        longitude = EXCLUDED.longitude,
        speed_kmh = EXCLUDED.speed_kmh,
        heading = EXCLUDED.heading,
        location_type_id = EXCLUDED.location_type_id,
        voltage_mv = EXCLUDED.voltage_mv,
        odometer_meters = EXCLUDED.odometer_meters,
        is_moving = EXCLUDED.is_moving,
        ignition_on = EXCLUDED.ignition_on,
        starter_disabled = EXCLUDED.starter_disabled,
        device_timestamp = EXCLUDED.device_timestamp,
        last_updated = CURRENT_TIMESTAMP
    WHERE vehicle_tracking_status.device_timestamp < EXCLUDED.device_timestamp;
END;
$function$
;

-- Permissions

ALTER FUNCTION public.upsert_vehicle_tracking_status(uuid, varchar, numeric, numeric, int2, int2, int2, int4, int8, timestamp) OWNER TO alex;
GRANT ALL ON FUNCTION public.upsert_vehicle_tracking_status(uuid, varchar, numeric, numeric, int2, int2, int2, int4, int8, timestamp) TO alex;

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