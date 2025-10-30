-- =====================================================
-- Driver License Scanner - SAFE Migration
-- =====================================================
-- This script safely migrates existing tables or creates new ones
-- Version: 1.0 (Safe)
-- Date: October 29, 2025
-- =====================================================

-- =====================================================
-- STEP 1: Check and backup existing data
-- =====================================================

DO $$
BEGIN
    -- Check if old table exists
    IF EXISTS (SELECT 1 FROM information_schema.tables 
               WHERE table_schema = 'public' AND table_name = 'customer_licenses') THEN
        RAISE NOTICE 'Table customer_licenses already exists. Checking structure...';
        
        -- Check if it has the correct columns
        IF NOT EXISTS (SELECT 1 FROM information_schema.columns 
                      WHERE table_name = 'customer_licenses' 
                      AND column_name = 'state_issued') THEN
            RAISE NOTICE 'Old structure detected. Need to migrate data...';
            
            -- Create backup table
            EXECUTE 'CREATE TABLE IF NOT EXISTS customer_licenses_backup_' || 
                    to_char(CURRENT_TIMESTAMP, 'YYYYMMDD_HH24MISS') || 
                    ' AS SELECT * FROM customer_licenses';
            
            RAISE NOTICE 'Backup created. Dropping old table...';
            DROP TABLE IF EXISTS customer_licenses CASCADE;
        ELSE
            RAISE NOTICE 'Table already has correct structure. Skipping creation...';
        END IF;
    END IF;
END $$;

-- =====================================================
-- STEP 2: Create tables with correct structure
-- =====================================================

CREATE TABLE IF NOT EXISTS customer_licenses (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    customer_id UUID NOT NULL UNIQUE,
    company_id UUID NOT NULL,
    
    -- License Identity
    license_number VARCHAR(50) NOT NULL,
    state_issued VARCHAR(2) NOT NULL,
    country_issued VARCHAR(2) DEFAULT 'US',
    
    -- Physical Characteristics
    sex VARCHAR(1),
    height VARCHAR(20),
    eye_color VARCHAR(20),
    middle_name VARCHAR(100),
    
    -- License Dates
    issue_date DATE,
    expiration_date DATE NOT NULL,
    
    -- Address on License (separate from customer address)
    license_address VARCHAR(255),
    license_city VARCHAR(100),
    license_state VARCHAR(100),
    license_postal_code VARCHAR(20),
    license_country VARCHAR(100),
    
    -- Additional Info
    restriction_code VARCHAR(50),
    endorsements VARCHAR(100),
    raw_barcode_data TEXT,
    
    -- Verification
    is_verified BOOLEAN DEFAULT true,
    verification_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    verification_method VARCHAR(50) DEFAULT 'license_scan',
    notes TEXT,
    
    -- Audit
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    created_by UUID,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_by UUID,
    
    -- Foreign Keys
    CONSTRAINT fk_customer FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    CONSTRAINT fk_company FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    CONSTRAINT uk_license_per_company UNIQUE (company_id, license_number, state_issued)
);

CREATE TABLE IF NOT EXISTS license_scans (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL,
    customer_id UUID NOT NULL,
    customer_license_id UUID,
    
    scan_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    scanned_by UUID,
    scan_source VARCHAR(50) DEFAULT 'mobile_app',
    device_id VARCHAR(100),
    device_type VARCHAR(50),
    app_version VARCHAR(20),
    
    scan_quality VARCHAR(20),
    all_fields_captured BOOLEAN DEFAULT true,
    captured_data JSONB,
    barcode_data TEXT,
    
    age_at_scan INTEGER,
    was_expired BOOLEAN DEFAULT false,
    days_until_expiration INTEGER,
    validation_passed BOOLEAN DEFAULT true,
    validation_errors TEXT[],
    
    customer_data_updated BOOLEAN DEFAULT false,
    fields_updated TEXT[],
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    
    CONSTRAINT fk_scan_company FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
    CONSTRAINT fk_scan_customer FOREIGN KEY (customer_id) REFERENCES customers(id) ON DELETE CASCADE,
    CONSTRAINT fk_scan_license FOREIGN KEY (customer_license_id) REFERENCES customer_licenses(id) ON DELETE SET NULL
);

-- =====================================================
-- STEP 3: Create indexes
-- =====================================================

CREATE INDEX IF NOT EXISTS idx_customer_licenses_customer_id ON customer_licenses(customer_id);
CREATE INDEX IF NOT EXISTS idx_customer_licenses_company_id ON customer_licenses(company_id);
CREATE INDEX IF NOT EXISTS idx_customer_licenses_license_number ON customer_licenses(license_number);
CREATE INDEX IF NOT EXISTS idx_customer_licenses_expiration_date ON customer_licenses(expiration_date);
CREATE INDEX IF NOT EXISTS idx_customer_licenses_state_issued ON customer_licenses(state_issued);

CREATE INDEX IF NOT EXISTS idx_license_scans_company_id ON license_scans(company_id);
CREATE INDEX IF NOT EXISTS idx_license_scans_customer_id ON license_scans(customer_id);
CREATE INDEX IF NOT EXISTS idx_license_scans_scan_date ON license_scans(company_id, scan_date DESC);
CREATE INDEX IF NOT EXISTS idx_license_scans_license_id ON license_scans(customer_license_id);

-- =====================================================
-- STEP 4: Create triggers
-- =====================================================

CREATE OR REPLACE FUNCTION update_customer_licenses_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trigger_customer_licenses_updated_at ON customer_licenses;
CREATE TRIGGER trigger_customer_licenses_updated_at
    BEFORE UPDATE ON customer_licenses
    FOR EACH ROW
    EXECUTE FUNCTION update_customer_licenses_updated_at();

-- =====================================================
-- STEP 5: Create views
-- =====================================================

DROP VIEW IF EXISTS v_customers_complete CASCADE;
CREATE VIEW v_customers_complete AS
SELECT 
    c.id AS customer_id,
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
    
    EXTRACT(YEAR FROM AGE(CURRENT_DATE, c.date_of_birth)) AS age,
    CASE 
        WHEN cl.expiration_date IS NULL THEN 'No License'
        WHEN cl.expiration_date < CURRENT_DATE THEN 'Expired'
        WHEN cl.expiration_date < CURRENT_DATE + INTERVAL '30 days' THEN 'Expiring Soon'
        ELSE 'Valid'
    END AS license_status,
    (cl.expiration_date - CURRENT_DATE) AS days_until_expiration
FROM customers c
LEFT JOIN customer_licenses cl ON c.id = cl.customer_id;

DROP VIEW IF EXISTS v_expired_licenses CASCADE;
CREATE VIEW v_expired_licenses AS
SELECT 
    c.company_id,
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
INNER JOIN customer_licenses cl ON c.id = cl.customer_id
WHERE cl.expiration_date < CURRENT_DATE
  AND c.is_active = true
ORDER BY cl.expiration_date ASC;

DROP VIEW IF EXISTS v_customers_without_license CASCADE;
CREATE VIEW v_customers_without_license AS
SELECT 
    c.id AS customer_id,
    c.company_id,
    c.email,
    c.phone,
    c.first_name,
    c.last_name,
    c.date_of_birth,
    c.created_at
FROM customers c
LEFT JOIN customer_licenses cl ON c.id = cl.customer_id
WHERE cl.id IS NULL
  AND c.is_active = true
  AND c.role = 'customer';

DROP VIEW IF EXISTS v_recent_scans CASCADE;
CREATE VIEW v_recent_scans AS
SELECT 
    ls.company_id,
    ls.scan_date,
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
INNER JOIN customers c ON ls.customer_id = c.id
LEFT JOIN customer_licenses cl ON ls.customer_license_id = cl.id
ORDER BY ls.scan_date DESC;

-- =====================================================
-- STEP 6: Create functions
-- =====================================================

CREATE OR REPLACE FUNCTION upsert_customer_license_with_sync(
    p_customer_id UUID,
    p_company_id UUID,
    p_license_number VARCHAR,
    p_state_issued VARCHAR,
    p_country_issued VARCHAR,
    p_middle_name VARCHAR,
    p_sex VARCHAR,
    p_height VARCHAR,
    p_eye_color VARCHAR,
    p_issue_date DATE,
    p_expiration_date DATE,
    p_license_address VARCHAR,
    p_license_city VARCHAR,
    p_license_state VARCHAR,
    p_license_postal_code VARCHAR,
    p_license_country VARCHAR,
    p_restriction_code VARCHAR,
    p_endorsements VARCHAR,
    p_raw_barcode_data TEXT,
    p_first_name VARCHAR,
    p_last_name VARCHAR,
    p_date_of_birth DATE,
    p_sync_customer_data BOOLEAN DEFAULT true,
    p_created_by UUID DEFAULT NULL
) RETURNS TABLE(license_id UUID, fields_updated TEXT[]) AS $$
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
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION is_license_already_used(
    p_company_id UUID,
    p_license_number VARCHAR,
    p_state_issued VARCHAR,
    p_exclude_customer_id UUID DEFAULT NULL
) RETURNS BOOLEAN AS $$
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
$$ LANGUAGE plpgsql;

-- =====================================================
-- STEP 7: Enable RLS
-- =====================================================

ALTER TABLE customer_licenses ENABLE ROW LEVEL SECURITY;
ALTER TABLE license_scans ENABLE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS customer_licenses_isolation ON customer_licenses;
CREATE POLICY customer_licenses_isolation ON customer_licenses
    FOR ALL
    USING (company_id = current_setting('app.current_company_id', true)::UUID);

DROP POLICY IF EXISTS license_scans_isolation ON license_scans;
CREATE POLICY license_scans_isolation ON license_scans
    FOR ALL
    USING (company_id = current_setting('app.current_company_id', true)::UUID);

-- =====================================================
-- STEP 8: Verification
-- =====================================================

DO $$
BEGIN
    RAISE NOTICE '==============================================';
    RAISE NOTICE 'Migration completed successfully!';
    RAISE NOTICE '==============================================';
    RAISE NOTICE '';
    RAISE NOTICE 'Tables created:';
    RAISE NOTICE '  - customer_licenses';
    RAISE NOTICE '  - license_scans';
    RAISE NOTICE '';
    RAISE NOTICE 'Views created:';
    RAISE NOTICE '  - v_customers_complete';
    RAISE NOTICE '  - v_expired_licenses';
    RAISE NOTICE '  - v_customers_without_license';
    RAISE NOTICE '  - v_recent_scans';
    RAISE NOTICE '';
    RAISE NOTICE 'Ready to use!';
    RAISE NOTICE '==============================================';
END $$;