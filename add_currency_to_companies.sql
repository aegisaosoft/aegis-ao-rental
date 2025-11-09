-- Adds a currency column to the companies table and populates it based on country
-- Default currency is USD when no specific match is found.

ALTER TABLE companies
ADD COLUMN IF NOT EXISTS currency VARCHAR(3) NOT NULL DEFAULT 'USD';

UPDATE companies
SET currency = CASE
    WHEN country IS NULL OR TRIM(country) = '' THEN 'USD'
    WHEN LOWER(TRIM(country)) IN ('united states', 'usa', 'us', 'united states of america', 'puerto rico', 'us virgin islands', 'turks and caicos islands', 'british virgin islands', 'anguilla', 'antigua and barbuda', 'dominica', 'grenada', 'saint kitts and nevis', 'saint lucia', 'saint vincent and the grenadines', 'montserrat', 'cuba') THEN
        CASE
            WHEN LOWER(TRIM(country)) = 'cuba' THEN 'CUP'
            WHEN LOWER(TRIM(country)) IN ('anguilla', 'antigua and barbuda', 'dominica', 'grenada', 'saint kitts and nevis', 'saint lucia', 'saint vincent and the grenadines', 'montserrat') THEN 'XCD'
            WHEN LOWER(TRIM(country)) = 'british virgin islands' THEN 'USD'
            WHEN LOWER(TRIM(country)) = 'turks and caicos islands' THEN 'USD'
            WHEN LOWER(TRIM(country)) = 'puerto rico' THEN 'USD'
            WHEN LOWER(TRIM(country)) = 'us virgin islands' THEN 'USD'
            ELSE 'USD'
        END
    WHEN LOWER(TRIM(country)) IN ('canada', 'ca') THEN 'CAD'
    WHEN LOWER(TRIM(country)) IN ('mexico', 'mx') THEN 'MXN'
    WHEN LOWER(TRIM(country)) IN ('bahamas') THEN 'BSD'
    WHEN LOWER(TRIM(country)) IN ('barbados') THEN 'BBD'
    WHEN LOWER(TRIM(country)) IN ('belize') THEN 'BZD'
    WHEN LOWER(TRIM(country)) IN ('bermuda') THEN 'BMD'
    WHEN LOWER(TRIM(country)) IN ('cayman islands') THEN 'KYD'
    WHEN LOWER(TRIM(country)) IN ('costa rica') THEN 'CRC'
    WHEN LOWER(TRIM(country)) IN ('dominican republic') THEN 'DOP'
    WHEN LOWER(TRIM(country)) IN ('el salvador') THEN 'USD'
    WHEN LOWER(TRIM(country)) IN ('greenland') THEN 'DKK'
    WHEN LOWER(TRIM(country)) IN ('guatemala') THEN 'GTQ'
    WHEN LOWER(TRIM(country)) IN ('haiti') THEN 'HTG'
    WHEN LOWER(TRIM(country)) IN ('honduras') THEN 'HNL'
    WHEN LOWER(TRIM(country)) IN ('jamaica') THEN 'JMD'
    WHEN LOWER(TRIM(country)) IN ('nicaragua') THEN 'NIO'
    WHEN LOWER(TRIM(country)) IN ('panama') THEN 'PAB'
    WHEN LOWER(TRIM(country)) IN ('saint pierre and miquelon', 'french guiana') THEN 'EUR'
    WHEN LOWER(TRIM(country)) IN ('trinidad and tobago') THEN 'TTD'
    WHEN LOWER(TRIM(country)) IN ('argentina') THEN 'ARS'
    WHEN LOWER(TRIM(country)) IN ('bolivia') THEN 'BOB'
    WHEN LOWER(TRIM(country)) IN ('brazil') THEN 'BRL'
    WHEN LOWER(TRIM(country)) IN ('chile') THEN 'CLP'
    WHEN LOWER(TRIM(country)) IN ('colombia') THEN 'COP'
    WHEN LOWER(TRIM(country)) IN ('ecuador') THEN 'USD'
    WHEN LOWER(TRIM(country)) IN ('guyana') THEN 'GYD'
    WHEN LOWER(TRIM(country)) IN ('paraguay') THEN 'PYG'
    WHEN LOWER(TRIM(country)) IN ('peru') THEN 'PEN'
    WHEN LOWER(TRIM(country)) IN ('suriname') THEN 'SRD'
    WHEN LOWER(TRIM(country)) IN ('uruguay') THEN 'UYU'
    WHEN LOWER(TRIM(country)) IN ('venezuela') THEN 'VES'
    ELSE 'USD'
END;

ALTER TABLE companies
ALTER COLUMN currency SET DEFAULT 'USD';

CREATE INDEX IF NOT EXISTS idx_companies_currency ON companies (currency);

