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

-- Add aegis_users table based on customers table structure
-- This table is a copy of the customers table structure

CREATE TABLE IF NOT EXISTS aegis_users (
    aegis_user_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    userid VARCHAR(255) NOT NULL UNIQUE,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone VARCHAR(50),
    password_hash VARCHAR(500),
    date_of_birth DATE,
    address TEXT,
    city VARCHAR(100),
    state VARCHAR(100),
    country VARCHAR(100),
    postal_code VARCHAR(20),
    stripe_customer_id VARCHAR(255) UNIQUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    role VARCHAR(50) DEFAULT 'agent',
    is_active BOOLEAN DEFAULT true,
    last_login TIMESTAMP,
    CONSTRAINT valid_aegis_user_role CHECK (role IN ('agent', 'admin', 'mainadmin'))
);

-- Add trigger for updated_at column
CREATE TRIGGER update_aegis_users_updated_at
BEFORE UPDATE ON aegis_users
FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

-- Create indexes for faster lookups
CREATE INDEX IF NOT EXISTS idx_aegis_users_is_active ON aegis_users(is_active);
CREATE INDEX IF NOT EXISTS idx_aegis_users_role ON aegis_users(role);

-- Ensure existing constraint allows mainadmin
ALTER TABLE aegis_users
    DROP CONSTRAINT IF EXISTS valid_aegis_user_role;

ALTER TABLE aegis_users
    ADD CONSTRAINT valid_aegis_user_role CHECK (role IN ('agent', 'admin', 'mainadmin'));

