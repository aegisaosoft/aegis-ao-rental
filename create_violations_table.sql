-- Create violations table for tracking parking violations, traffic violations, etc.
-- This table stores violations associated with bookings, customers, vehicles, and companies

CREATE TABLE IF NOT EXISTS violations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL,
    booking_id UUID,
    customer_id UUID,
    vehicle_id UUID,
    violation_number VARCHAR(50) NOT NULL,
    violation_date TIMESTAMP NOT NULL,
    type VARCHAR(100) NOT NULL,
    description VARCHAR(1000),
    amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    status VARCHAR(50) NOT NULL DEFAULT 'pending',
    notes VARCHAR(500),
    paid_date TIMESTAMP,
    due_date TIMESTAMP,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign key constraints
    CONSTRAINT fk_violations_company 
        FOREIGN KEY (company_id) 
        REFERENCES companies(id) 
        ON DELETE CASCADE,
    
    CONSTRAINT fk_violations_booking 
        FOREIGN KEY (booking_id) 
        REFERENCES bookings(id) 
        ON DELETE SET NULL,
    
    CONSTRAINT fk_violations_customer 
        FOREIGN KEY (customer_id) 
        REFERENCES customers(id) 
        ON DELETE SET NULL,
    
    CONSTRAINT fk_violations_vehicle 
        FOREIGN KEY (vehicle_id) 
        REFERENCES vehicles(id) 
        ON DELETE SET NULL,
    
    -- Constraints
    CONSTRAINT chk_violations_status 
        CHECK (status IN ('pending', 'paid', 'overdue', 'cancelled')),
    
    CONSTRAINT chk_violations_amount 
        CHECK (amount >= 0)
);

-- Create indexes for better query performance
CREATE INDEX IF NOT EXISTS idx_violations_company_id ON violations(company_id);
CREATE INDEX IF NOT EXISTS idx_violations_booking_id ON violations(booking_id);
CREATE INDEX IF NOT EXISTS idx_violations_customer_id ON violations(customer_id);
CREATE INDEX IF NOT EXISTS idx_violations_vehicle_id ON violations(vehicle_id);
CREATE INDEX IF NOT EXISTS idx_violations_violation_date ON violations(violation_date);
CREATE INDEX IF NOT EXISTS idx_violations_status ON violations(status);
CREATE INDEX IF NOT EXISTS idx_violations_violation_number ON violations(violation_number);

-- Create a function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_violations_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at
DROP TRIGGER IF EXISTS trigger_update_violations_updated_at ON violations;
CREATE TRIGGER trigger_update_violations_updated_at
    BEFORE UPDATE ON violations
    FOR EACH ROW
    EXECUTE FUNCTION update_violations_updated_at();

-- Add comment to table
COMMENT ON TABLE violations IS 'Stores violations (parking, traffic, etc.) associated with rental bookings, customers, vehicles, and companies';
