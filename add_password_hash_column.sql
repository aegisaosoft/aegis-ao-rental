--
--
-- Copyright (c) 2025 Alexander Orlov.
-- 34 Middletown Ave Atlantic Highlands NJ 07716
-- 
-- THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
-- Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
-- SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
-- WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
-- Alexander Orlov.
-- 
--  Author: Alexander Orlov
-- 
--
-- Migration: Add password_hash column to customers table
-- This script adds the password_hash column if it doesn't exist

-- Add password_hash column to customers table
ALTER TABLE customers ADD COLUMN IF NOT EXISTS password_hash VARCHAR(500);

-- Add comment to document the column
COMMENT ON COLUMN customers.password_hash IS 'BCrypt password hash stored from C# BCrypt.Net library';
