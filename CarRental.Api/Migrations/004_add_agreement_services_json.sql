-- Migration: Add additional_services_json column to rental_agreements
-- This column stores the additional services snapshot as JSON for PDF generation

ALTER TABLE rental_agreements 
ADD COLUMN IF NOT EXISTS additional_services_json TEXT;

-- Add comment
COMMENT ON COLUMN rental_agreements.additional_services_json IS 'JSON snapshot of additional services selected at booking time';
