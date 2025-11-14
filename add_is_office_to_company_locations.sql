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

-- Add is_office column to company_location table
ALTER TABLE company_location
ADD COLUMN IF NOT EXISTS is_office bool NOT NULL DEFAULT false;

-- Create index for better performance when filtering by is_office
CREATE INDEX IF NOT EXISTS idx_company_location_is_office ON company_location(is_office);

