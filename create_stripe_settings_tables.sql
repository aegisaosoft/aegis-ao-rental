/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

-- Create stripe_settings table
CREATE TABLE IF NOT EXISTS public.stripe_settings (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    name varchar(20) NOT NULL,
    secret_key text NULL,
    publishable_key text NULL,
    webhook_secret text NULL,
    created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    CONSTRAINT stripe_settings_pkey PRIMARY KEY (id)
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_stripe_settings_name ON public.stripe_settings USING btree (name);

-- Table comments
COMMENT ON TABLE public.stripe_settings IS 'Stripe API settings configuration';

-- Column comments
COMMENT ON COLUMN public.stripe_settings.name IS 'Name identifier for the Stripe settings';
COMMENT ON COLUMN public.stripe_settings.secret_key IS 'Stripe secret key';
COMMENT ON COLUMN public.stripe_settings.publishable_key IS 'Stripe publishable key';
COMMENT ON COLUMN public.stripe_settings.webhook_secret IS 'Stripe webhook secret';

-- Create trigger to update updated_at timestamp
DROP TRIGGER IF EXISTS update_stripe_settings_updated_at ON public.stripe_settings;
CREATE TRIGGER update_stripe_settings_updated_at
BEFORE UPDATE ON public.stripe_settings
FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

-- Create stripe_company table (junction table linking companies to stripe settings)
CREATE TABLE IF NOT EXISTS public.stripe_company (
    id uuid DEFAULT uuid_generate_v4() NOT NULL,
    settings_id uuid NOT NULL,
    company_id uuid NOT NULL,
    stripe_account_id varchar(255) NULL,
    created_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    updated_at timestamp DEFAULT CURRENT_TIMESTAMP NULL,
    CONSTRAINT stripe_company_pkey PRIMARY KEY (id),
    CONSTRAINT fk_stripe_company_settings FOREIGN KEY (settings_id) REFERENCES public.stripe_settings(id) ON DELETE CASCADE,
    CONSTRAINT fk_stripe_company_company FOREIGN KEY (company_id) REFERENCES public.companies(id) ON DELETE CASCADE,
    CONSTRAINT stripe_company_company_unique UNIQUE (company_id)
);

-- Create indexes for better performance
CREATE INDEX IF NOT EXISTS idx_stripe_company_settings ON public.stripe_company USING btree (settings_id);
CREATE INDEX IF NOT EXISTS idx_stripe_company_company ON public.stripe_company USING btree (company_id);

-- Table comments
COMMENT ON TABLE public.stripe_company IS 'Junction table linking companies to their Stripe settings';

-- Column comments
COMMENT ON COLUMN public.stripe_company.settings_id IS 'Reference to stripe_settings table';
COMMENT ON COLUMN public.stripe_company.company_id IS 'Reference to companies table';
COMMENT ON COLUMN public.stripe_company.stripe_account_id IS 'Stripe account ID for the company';

-- Create trigger to update updated_at timestamp
DROP TRIGGER IF EXISTS update_stripe_company_updated_at ON public.stripe_company;
CREATE TRIGGER update_stripe_company_updated_at
BEFORE UPDATE ON public.stripe_company
FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

-- Permissions
ALTER TABLE public.stripe_settings OWNER TO alex;
GRANT ALL ON TABLE public.stripe_settings TO alex;

ALTER TABLE public.stripe_company OWNER TO alex;
GRANT ALL ON TABLE public.stripe_company TO alex;

-- ============================================================================
-- Populate tables with data
-- ============================================================================

-- Insert test record into stripe_settings with parameters from settings table
INSERT INTO public.stripe_settings (name, secret_key, publishable_key, webhook_secret, created_at, updated_at)
SELECT 
    'test' as name,
    (SELECT value FROM public.settings WHERE key = 'stripe.secretKey' LIMIT 1) as secret_key,
    (SELECT value FROM public.settings WHERE key = 'stripe.publishableKey' LIMIT 1) as publishable_key,
    (SELECT value FROM public.settings WHERE key = 'stripe.webhookSecret' LIMIT 1) as webhook_secret,
    CURRENT_TIMESTAMP as created_at,
    CURRENT_TIMESTAMP as updated_at
WHERE NOT EXISTS (
    SELECT 1 FROM public.stripe_settings WHERE name = 'test'
)
ON CONFLICT DO NOTHING;

-- Insert records into stripe_company for each company with stripe_account_id
-- Link them to the test stripe_settings record
INSERT INTO public.stripe_company (settings_id, company_id, stripe_account_id, created_at, updated_at)
SELECT 
    ss.id as settings_id,
    c.id as company_id,
    c.stripe_account_id,
    CURRENT_TIMESTAMP as created_at,
    CURRENT_TIMESTAMP as updated_at
FROM public.companies c
CROSS JOIN public.stripe_settings ss
WHERE ss.name = 'test'
  AND c.stripe_account_id IS NOT NULL
  AND c.stripe_account_id != ''
  AND NOT EXISTS (
    SELECT 1 FROM public.stripe_company sc 
    WHERE sc.company_id = c.id
  )
ON CONFLICT DO NOTHING;

