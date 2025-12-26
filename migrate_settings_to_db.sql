-- ============================================================================
-- Migration Script: Move settings from appsettings.json to database
-- 
-- IMPORTANT: 
-- 1. Replace placeholder values with actual values from your appsettings.json
-- 2. Values marked with 'ENCRYPTED:' should be encrypted using the EncryptionService
--    before inserting, OR you can insert plain values and the application will
--    auto-encrypt them on first read (see SettingsService.GetValueAsync)
-- 3. Run this script AFTER deploying the updated code
-- ============================================================================

-- ============================================================================
-- Ensure unique constraint exists on 'key' column for ON CONFLICT to work
-- ============================================================================

-- Create unique index if it doesn't exist
CREATE UNIQUE INDEX IF NOT EXISTS settings_key_unique ON settings (key);

-- Alternative: Add unique constraint (comment out if using index above)
-- ALTER TABLE settings ADD CONSTRAINT settings_key_unique UNIQUE (key);

-- ============================================================================
-- Azure Blob Storage Settings
-- ============================================================================

-- Connection String (will be encrypted by app on first read if stored as plaintext)
-- Replace 'YOUR_AZURE_STORAGE_CONNECTION_STRING' with actual value
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'azure.storage.connectionString',
    'YOUR_AZURE_STORAGE_CONNECTION_STRING',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- Container Name (default: vehicle-media)
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'azure.storage.containerName',
    'vehicle-media',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- ============================================================================
-- Meta OAuth Settings (Facebook/Instagram Integration)
-- ============================================================================

-- Meta App ID
-- Replace 'YOUR_META_APP_ID' with actual Facebook App ID
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'meta.appId',
    'YOUR_META_APP_ID',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- Meta App Secret (will be encrypted by app on first read)
-- Replace 'YOUR_META_APP_SECRET' with actual Facebook App Secret
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'meta.appSecret',
    'YOUR_META_APP_SECRET',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- Meta OAuth Redirect URI
-- Replace with your actual callback URL
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'meta.redirectUri',
    'https://aegis-ao-rental.azurewebsites.net/api/meta/oauth/callback',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- Meta Frontend Redirect URL (after OAuth completion)
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'meta.frontendRedirectUrl',
    'https://aegis-ao-rental.com/{subdomain}/admin/integrations',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- ============================================================================
-- Stripe Settings (if not already migrated)
-- ============================================================================

-- Stripe Secret Key (will be encrypted by app on first read)
-- Uncomment and replace if needed
/*
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'stripe.secretKey',
    'YOUR_STRIPE_SECRET_KEY',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- Stripe Publishable Key
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'stripe.publishableKey',
    'YOUR_STRIPE_PUBLISHABLE_KEY',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- Stripe Webhook Secret (will be encrypted by app on first read)
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'stripe.webhookSecret',
    'YOUR_STRIPE_WEBHOOK_SECRET',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();
*/

-- ============================================================================
-- AI Settings (if needed)
-- ============================================================================

/*
-- Anthropic API Key
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'anthropic.apiKey',
    'YOUR_ANTHROPIC_API_KEY',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- OpenAI API Key
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'openai.apiKey',
    'YOUR_OPENAI_API_KEY',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();
*/

-- ============================================================================
-- Google Translate Settings (if needed)
-- ============================================================================

/*
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'google.translate.key',
    'YOUR_GOOGLE_TRANSLATE_API_KEY',
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();
*/

-- ============================================================================
-- Verify inserted settings
-- ============================================================================

SELECT 
    key,
    CASE 
        WHEN key LIKE '%.secret%' OR key LIKE '%.apiKey' OR key LIKE '%connectionString' 
        THEN LEFT(value, 10) || '...[HIDDEN]'
        ELSE value
    END as value_preview,
    created_at,
    updated_at
FROM settings
ORDER BY key;
