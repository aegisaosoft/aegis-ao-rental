-- Create finders_list table for storing violation finder configurations per company
-- This table stores which states/provinces are enabled for violation finders

CREATE TABLE IF NOT EXISTS finders_list (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    company_id UUID NOT NULL UNIQUE,
    finders_list JSONB NOT NULL DEFAULT '[]'::jsonb,
    created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    -- Foreign key constraint
    CONSTRAINT fk_finders_list_company 
        FOREIGN KEY (company_id) 
        REFERENCES companies(id) 
        ON DELETE CASCADE
);

-- Create index for better query performance
CREATE INDEX IF NOT EXISTS idx_finders_list_company_id ON finders_list(company_id);

-- Create a function to automatically update updated_at timestamp
CREATE OR REPLACE FUNCTION update_finders_list_updated_at()
RETURNS TRIGGER AS $$
BEGIN
    NEW.updated_at = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Create trigger to automatically update updated_at
DROP TRIGGER IF EXISTS trigger_update_finders_list_updated_at ON finders_list;
CREATE TRIGGER trigger_update_finders_list_updated_at
    BEFORE UPDATE ON finders_list
    FOR EACH ROW
    EXECUTE FUNCTION update_finders_list_updated_at();

-- Add comment to table
COMMENT ON TABLE finders_list IS 'Stores violation finder configurations (enabled states/provinces) for each company';

-- Add comment to columns
COMMENT ON COLUMN finders_list.id IS 'Unique finders list identifier (UUID)';
COMMENT ON COLUMN finders_list.company_id IS 'Foreign key reference to companies table';
COMMENT ON COLUMN finders_list.finders_list IS 'JSON array of state/province codes enabled for violation finders';
