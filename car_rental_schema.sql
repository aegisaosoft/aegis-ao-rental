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
-- Car Rental Database Schema with Multi-Company Support and Stripe Integration
-- PostgreSQL Database
-- This script creates or updates the database schema

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create or update function for updated_at column
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ language 'plpgsql';

-- =====================================================
-- RENTAL COMPANIES
-- =====================================================

CREATE TABLE IF NOT EXISTS rental_companies (
    company_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_name VARCHAR(255) NOT NULL,
    email VARCHAR(255) NOT NULL UNIQUE,
    phone VARCHAR(50),
    address TEXT,
    city VARCHAR(100),
    state VARCHAR(100),
    country VARCHAR(100),
    postal_code VARCHAR(20),
    stripe_account_id VARCHAR(255) UNIQUE, -- For Stripe Connect
    tax_id VARCHAR(100),
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- CUSTOMERS
-- =====================================================

CREATE TABLE IF NOT EXISTS customers (
    customer_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    email VARCHAR(255) NOT NULL UNIQUE,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    phone VARCHAR(50),
    password_hash VARCHAR(500), -- BCrypt password hash (max length 500 for safety)
    role VARCHAR(50) DEFAULT 'customer', -- customer, worker, admin, mainadmin
    is_active BOOLEAN DEFAULT true,
    last_login TIMESTAMP,
    company_id UUID REFERENCES rental_companies(company_id) ON DELETE SET NULL,
    date_of_birth DATE,
    drivers_license_number VARCHAR(100),
    drivers_license_state VARCHAR(50),
    drivers_license_expiry DATE,
    address TEXT,
    city VARCHAR(100),
    state VARCHAR(100),
    country VARCHAR(100),
    postal_code VARCHAR(20),
    stripe_customer_id VARCHAR(255) UNIQUE, -- Stripe customer ID
    is_verified BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT valid_customer_role CHECK (role IN ('customer', 'worker', 'admin', 'mainadmin'))
);

-- =====================================================
-- VEHICLES
-- =====================================================

CREATE TABLE IF NOT EXISTS vehicle_categories (
    category_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    category_name VARCHAR(100) NOT NULL,
    description TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS vehicles (
    vehicle_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    category_id UUID REFERENCES vehicle_categories(category_id),
    make VARCHAR(100) NOT NULL,
    model VARCHAR(100) NOT NULL,
    year INTEGER NOT NULL,
    color VARCHAR(50),
    license_plate VARCHAR(50) UNIQUE NOT NULL,
    vin VARCHAR(17) UNIQUE,
    mileage INTEGER DEFAULT 0,
    fuel_type VARCHAR(50), -- gasoline, diesel, electric, hybrid
    transmission VARCHAR(50), -- automatic, manual
    seats INTEGER,
    daily_rate DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'available', -- available, rented, maintenance, retired
    state VARCHAR(2), -- US state code (2 characters)
    location VARCHAR(255), -- Current location/branch
    image_url TEXT,
    features TEXT[], -- Array of features: ['GPS', 'Bluetooth', 'Backup Camera']
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- RESERVATIONS
-- =====================================================

CREATE TABLE IF NOT EXISTS reservations (
    reservation_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
    vehicle_id UUID NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE RESTRICT,
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    reservation_number VARCHAR(50) UNIQUE NOT NULL,
    pickup_date TIMESTAMP NOT NULL,
    return_date TIMESTAMP NOT NULL,
    pickup_location VARCHAR(255),
    return_location VARCHAR(255),
    daily_rate DECIMAL(10, 2) NOT NULL,
    total_days INTEGER NOT NULL,
    subtotal DECIMAL(10, 2) NOT NULL,
    tax_amount DECIMAL(10, 2) DEFAULT 0,
    insurance_amount DECIMAL(10, 2) DEFAULT 0,
    additional_fees DECIMAL(10, 2) DEFAULT 0,
    total_amount DECIMAL(10, 2) NOT NULL,
    status VARCHAR(50) DEFAULT 'pending', -- pending, confirmed, active, completed, cancelled
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- RENTALS (Active Rental Tracking)
-- =====================================================

CREATE TABLE IF NOT EXISTS rentals (
    rental_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID NOT NULL REFERENCES reservations(reservation_id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
    vehicle_id UUID NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE RESTRICT,
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    actual_pickup_date TIMESTAMP NOT NULL,
    expected_return_date TIMESTAMP NOT NULL,
    actual_return_date TIMESTAMP,
    pickup_mileage INTEGER,
    return_mileage INTEGER,
    fuel_level_pickup VARCHAR(50), -- full, 3/4, 1/2, 1/4, empty
    fuel_level_return VARCHAR(50),
    damage_notes_pickup TEXT,
    damage_notes_return TEXT,
    additional_charges DECIMAL(10, 2) DEFAULT 0,
    status VARCHAR(50) DEFAULT 'active', -- active, completed, overdue
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- PAYMENTS (Stripe Integration)
-- =====================================================

CREATE TABLE IF NOT EXISTS payments (
    payment_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    reservation_id UUID REFERENCES reservations(reservation_id) ON DELETE SET NULL,
    rental_id UUID REFERENCES rentals(rental_id) ON DELETE SET NULL,
    customer_id UUID NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    amount DECIMAL(10, 2) NOT NULL,
    currency VARCHAR(10) DEFAULT 'USD',
    payment_type VARCHAR(50) NOT NULL, -- reservation_deposit, full_payment, additional_charges, refund
    payment_method VARCHAR(50), -- card, bank_transfer, cash
    stripe_payment_intent_id VARCHAR(255) UNIQUE,
    stripe_charge_id VARCHAR(255),
    stripe_payment_method_id VARCHAR(255),
    status VARCHAR(50) DEFAULT 'pending', -- pending, processing, succeeded, failed, refunded
    failure_reason TEXT,
    processed_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- PAYMENT METHODS (Saved Cards)
-- =====================================================

CREATE TABLE IF NOT EXISTS customer_payment_methods (
    payment_method_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    customer_id UUID NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
    stripe_payment_method_id VARCHAR(255) UNIQUE NOT NULL,
    card_brand VARCHAR(50), -- visa, mastercard, amex, etc.
    card_last4 VARCHAR(4),
    card_exp_month INTEGER,
    card_exp_year INTEGER,
    is_default BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- REVIEWS
-- =====================================================

CREATE TABLE IF NOT EXISTS reviews (
    review_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    rental_id UUID NOT NULL REFERENCES rentals(rental_id) ON DELETE CASCADE,
    customer_id UUID NOT NULL REFERENCES customers(customer_id) ON DELETE CASCADE,
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    vehicle_id UUID NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE CASCADE,
    rating INTEGER CHECK (rating >= 1 AND rating <= 5),
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- INDEXES FOR PERFORMANCE
-- =====================================================

CREATE INDEX IF NOT EXISTS idx_vehicles_company ON vehicles(company_id);
CREATE INDEX IF NOT EXISTS idx_vehicles_status ON vehicles(status);
CREATE INDEX IF NOT EXISTS idx_vehicles_category ON vehicles(category_id);

CREATE INDEX IF NOT EXISTS idx_reservations_customer ON reservations(customer_id);
CREATE INDEX IF NOT EXISTS idx_reservations_vehicle ON reservations(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_reservations_company ON reservations(company_id);
CREATE INDEX IF NOT EXISTS idx_reservations_dates ON reservations(pickup_date, return_date);
CREATE INDEX IF NOT EXISTS idx_reservations_status ON reservations(status);

CREATE INDEX IF NOT EXISTS idx_rentals_customer ON rentals(customer_id);
CREATE INDEX IF NOT EXISTS idx_rentals_vehicle ON rentals(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_rentals_status ON rentals(status);
CREATE INDEX IF NOT EXISTS idx_rentals_dates ON rentals(actual_pickup_date, expected_return_date);

CREATE INDEX IF NOT EXISTS idx_payments_customer ON payments(customer_id);
CREATE INDEX IF NOT EXISTS idx_payments_company ON payments(company_id);
CREATE INDEX IF NOT EXISTS idx_payments_reservation ON payments(reservation_id);
CREATE INDEX IF NOT EXISTS idx_payments_status ON payments(status);
CREATE INDEX IF NOT EXISTS idx_payments_stripe_intent ON payments(stripe_payment_intent_id);

CREATE INDEX IF NOT EXISTS idx_customer_payment_methods_customer ON customer_payment_methods(customer_id);

CREATE INDEX IF NOT EXISTS idx_customers_role ON customers(role);
CREATE INDEX IF NOT EXISTS idx_customers_is_active ON customers(is_active);
CREATE INDEX IF NOT EXISTS idx_customers_company_id ON customers(company_id);

-- =====================================================
-- TRIGGERS FOR UPDATED_AT
-- =====================================================

CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS update_rental_companies_updated_at ON rental_companies;
CREATE TRIGGER update_rental_companies_updated_at BEFORE UPDATE ON rental_companies
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_customers_updated_at ON customers;
CREATE TRIGGER update_customers_updated_at BEFORE UPDATE ON customers
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_vehicles_updated_at ON vehicles;
CREATE TRIGGER update_vehicles_updated_at BEFORE UPDATE ON vehicles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_reservations_updated_at ON reservations;
CREATE TRIGGER update_reservations_updated_at BEFORE UPDATE ON reservations
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_rentals_updated_at ON rentals;
CREATE TRIGGER update_rentals_updated_at BEFORE UPDATE ON rentals
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- USEFUL VIEWS
-- =====================================================

-- Drop existing views if they exist (will be recreated at the end)
DROP VIEW IF EXISTS available_vehicles;
DROP VIEW IF EXISTS reservation_details;

-- =====================================================
-- SAMPLE DATA
-- =====================================================

-- Insert sample vehicle categories (only if they don't exist)
INSERT INTO vehicle_categories (category_name, description) 
SELECT * FROM (VALUES
('Economy', 'Small, fuel-efficient vehicles perfect for budget-conscious travelers'),
('Compact', 'Comfortable vehicles for city driving'),
('Mid-Size', 'Spacious sedans with more room and comfort'),
('Full-Size', 'Large vehicles with maximum comfort and space'),
('SUV', 'Sport Utility Vehicles for families and groups'),
('Luxury', 'Premium vehicles with advanced features'),
('Van', 'Passenger vans for large groups')
) AS v(category_name, description)
WHERE NOT EXISTS (SELECT 1 FROM vehicle_categories WHERE category_name = v.category_name);

-- Insert sample rental companies (only if they don't exist)
INSERT INTO rental_companies (company_name, email, phone, address, city, state, country, postal_code)
SELECT * FROM (VALUES
('Premium Rentals Inc', 'info@premiumrentals.com', '+1-555-0100', '123 Main St', 'New York', 'NY', 'USA', '10001'),
('Budget Cars LLC', 'contact@budgetcars.com', '+1-555-0200', '456 Oak Ave', 'Los Angeles', 'CA', 'USA', '90001')
) AS v(company_name, email, phone, address, city, state, country, postal_code)
WHERE NOT EXISTS (SELECT 1 FROM rental_companies WHERE email = v.email);

-- Note: Actual Stripe IDs would be added when companies connect their Stripe accounts

-- =====================================================
-- BOOKING TOKENS (Token-based Booking System)
-- =====================================================

CREATE TABLE IF NOT EXISTS booking_tokens (
    token_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    company_id UUID NOT NULL REFERENCES rental_companies(company_id) ON DELETE CASCADE,
    customer_email VARCHAR(255) NOT NULL,
    vehicle_id UUID NOT NULL REFERENCES vehicles(vehicle_id) ON DELETE RESTRICT,
    token VARCHAR(255) UNIQUE NOT NULL,
    booking_data JSONB NOT NULL, -- Contains pickup/return dates, pricing, etc.
    expires_at TIMESTAMP NOT NULL,
    is_used BOOLEAN DEFAULT false,
    used_at TIMESTAMP,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- EMAIL NOTIFICATIONS
-- =====================================================

CREATE TABLE IF NOT EXISTS email_notifications (
    notification_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    booking_token_id UUID REFERENCES booking_tokens(token_id) ON DELETE CASCADE,
    customer_email VARCHAR(255) NOT NULL,
    notification_type VARCHAR(50) NOT NULL, -- booking_link, payment_success, booking_confirmation
    subject VARCHAR(255) NOT NULL,
    body TEXT NOT NULL,
    status VARCHAR(50) DEFAULT 'pending', -- pending, sent, failed, delivered
    sent_at TIMESTAMP,
    error_message TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- BOOKING CONFIRMATIONS
-- =====================================================

CREATE TABLE IF NOT EXISTS booking_confirmations (
    confirmation_id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    booking_token_id UUID NOT NULL REFERENCES booking_tokens(token_id) ON DELETE CASCADE,
    reservation_id UUID REFERENCES reservations(reservation_id) ON DELETE SET NULL,
    customer_email VARCHAR(255) NOT NULL,
    confirmation_number VARCHAR(50) UNIQUE NOT NULL,
    booking_details JSONB NOT NULL,
    payment_status VARCHAR(50) NOT NULL, -- pending, completed, failed, refunded
    stripe_payment_intent_id VARCHAR(255),
    confirmation_sent BOOLEAN DEFAULT false,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- =====================================================
-- ADDITIONAL INDEXES FOR BOOKING SYSTEM
-- =====================================================

CREATE INDEX IF NOT EXISTS idx_booking_tokens_company ON booking_tokens(company_id);
CREATE INDEX IF NOT EXISTS idx_booking_tokens_customer ON booking_tokens(customer_email);
CREATE INDEX IF NOT EXISTS idx_booking_tokens_vehicle ON booking_tokens(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_booking_tokens_token ON booking_tokens(token);
CREATE INDEX IF NOT EXISTS idx_booking_tokens_expires ON booking_tokens(expires_at);
CREATE INDEX IF NOT EXISTS idx_booking_tokens_used ON booking_tokens(is_used);

CREATE INDEX IF NOT EXISTS idx_email_notifications_customer ON email_notifications(customer_email);
CREATE INDEX IF NOT EXISTS idx_email_notifications_type ON email_notifications(notification_type);
CREATE INDEX IF NOT EXISTS idx_email_notifications_status ON email_notifications(status);
CREATE INDEX IF NOT EXISTS idx_email_notifications_booking_token ON email_notifications(booking_token_id);

CREATE INDEX IF NOT EXISTS idx_booking_confirmations_token ON booking_confirmations(booking_token_id);
CREATE INDEX IF NOT EXISTS idx_booking_confirmations_customer ON booking_confirmations(customer_email);
CREATE INDEX IF NOT EXISTS idx_booking_confirmations_confirmation_number ON booking_confirmations(confirmation_number);
CREATE INDEX IF NOT EXISTS idx_booking_confirmations_payment_status ON booking_confirmations(payment_status);

-- =====================================================
-- ADDITIONAL TRIGGERS FOR BOOKING SYSTEM
-- =====================================================

DROP TRIGGER IF EXISTS update_booking_tokens_updated_at ON booking_tokens;
CREATE TRIGGER update_booking_tokens_updated_at BEFORE UPDATE ON booking_tokens
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

DROP TRIGGER IF EXISTS update_booking_confirmations_updated_at ON booking_confirmations;
CREATE TRIGGER update_booking_confirmations_updated_at BEFORE UPDATE ON booking_confirmations
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- =====================================================
-- DROP AND RECREATE VIEWS TO INCLUDE NEW COLUMNS
-- =====================================================

-- Drop existing views
DROP VIEW IF EXISTS available_vehicles;
DROP VIEW IF EXISTS reservation_details;

-- Recreate view for available vehicles by date range
CREATE OR REPLACE VIEW available_vehicles AS
SELECT 
    v.*,
    rc.company_name,
    vc.category_name
FROM vehicles v
JOIN rental_companies rc ON v.company_id = rc.company_id
LEFT JOIN vehicle_categories vc ON v.category_id = vc.category_id
WHERE v.status = 'available' 
    AND v.is_active = true
    AND rc.is_active = true;

-- Recreate view for reservation details
CREATE OR REPLACE VIEW reservation_details AS
SELECT 
    r.*,
    c.first_name || ' ' || c.last_name AS customer_name,
    c.email AS customer_email,
    c.phone AS customer_phone,
    v.make || ' ' || v.model || ' (' || v.year || ')' AS vehicle_name,
    v.license_plate,
    rc.company_name,
    rc.email AS company_email
FROM reservations r
JOIN customers c ON r.customer_id = c.customer_id
JOIN vehicles v ON r.vehicle_id = v.vehicle_id
JOIN rental_companies rc ON r.company_id = rc.company_id;
