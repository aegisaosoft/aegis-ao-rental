-- Create a test customer for login testing
-- Run this AFTER running the add_customer_roles.sql migration

-- Insert a test customer with admin role
-- Password: "password123" (BCrypt hash)
INSERT INTO customers (
    customer_id,
    email,
    first_name,
    last_name,
    phone,
    password_hash,
    role,
    is_active,
    is_verified,
    created_at,
    updated_at
) VALUES (
    uuid_generate_v4(),
    'admin@test.com',
    'Test',
    'Admin',
    '+1234567890',
    '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', -- password: "password"
    'admin',
    true,
    true,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
) ON CONFLICT (email) DO NOTHING;

-- Insert a test customer with worker role (associated with first company)
INSERT INTO customers (
    customer_id,
    email,
    first_name,
    last_name,
    phone,
    password_hash,
    role,
    company_id,
    is_active,
    is_verified,
    created_at,
    updated_at
) VALUES (
    uuid_generate_v4(),
    'worker@test.com',
    'Test',
    'Worker',
    '+1234567891',
    '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', -- password: "password"
    'worker',
    (SELECT company_id FROM rental_companies LIMIT 1), -- Associate with first company
    true,
    true,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
) ON CONFLICT (email) DO NOTHING;

-- Insert a regular customer
INSERT INTO customers (
    customer_id,
    email,
    first_name,
    last_name,
    phone,
    password_hash,
    role,
    is_active,
    is_verified,
    created_at,
    updated_at
) VALUES (
    uuid_generate_v4(),
    'customer@test.com',
    'Test',
    'Customer',
    '+1234567892',
    '$2a$11$92IXUNpkjO0rOQ5byMi.Ye4oKoEa3Ro9llC/.og/at2.uheWG/igi', -- password: "password"
    'customer',
    true,
    true,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
) ON CONFLICT (email) DO NOTHING;
