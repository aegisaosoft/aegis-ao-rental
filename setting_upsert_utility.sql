-- ============================================================================
-- Quick utility: Add or Update a single setting
-- Usage: Replace KEY_NAME and VALUE below and run
-- ============================================================================

-- UPSERT single setting
INSERT INTO settings (id, key, value, created_at, updated_at)
VALUES (
    gen_random_uuid(),
    'KEY_NAME',        -- Replace with actual key, e.g. 'meta.appId'
    'VALUE',           -- Replace with actual value
    NOW(),
    NOW()
)
ON CONFLICT (key) DO UPDATE SET
    value = EXCLUDED.value,
    updated_at = NOW();

-- ============================================================================
-- Examples:
-- ============================================================================

-- Azure Blob Storage Connection String
-- INSERT INTO settings (id, key, value, created_at, updated_at)
-- VALUES (gen_random_uuid(), 'azure.storage.connectionString', 'DefaultEndpointsProtocol=https;AccountName=...', NOW(), NOW())
-- ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Meta App ID  
-- INSERT INTO settings (id, key, value, created_at, updated_at)
-- VALUES (gen_random_uuid(), 'meta.appId', '123456789012345', NOW(), NOW())
-- ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Meta App Secret
-- INSERT INTO settings (id, key, value, created_at, updated_at)
-- VALUES (gen_random_uuid(), 'meta.appSecret', 'abc123def456...', NOW(), NOW())
-- ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- ============================================================================
-- View all settings (with hidden sensitive values)
-- ============================================================================

SELECT 
    key,
    CASE 
        WHEN key IN ('stripe.secretKey', 'stripe.webhookSecret', 'azure.storage.connectionString', 
                     'meta.appSecret', 'anthropic.apiKey', 'openai.apiKey', 'claude.apiKey',
                     'google.translate.key', 'azure.clientSecret')
        THEN LEFT(value, 10) || '...[HIDDEN]'
        ELSE value
    END as value_preview,
    updated_at
FROM settings
ORDER BY key;

-- ============================================================================
-- Delete a setting
-- ============================================================================

-- DELETE FROM settings WHERE key = 'KEY_NAME';

-- ============================================================================
-- Available setting keys:
-- ============================================================================
-- azure.storage.connectionString  - Azure Blob Storage connection string (encrypted)
-- azure.storage.containerName     - Container name for media files
-- meta.appId                      - Facebook App ID
-- meta.appSecret                  - Facebook App Secret (encrypted)
-- meta.redirectUri                - OAuth callback URL
-- meta.frontendRedirectUrl        - Frontend redirect after OAuth
-- stripe.secretKey                - Stripe secret key (encrypted)
-- stripe.publishableKey           - Stripe publishable key (encrypted)
-- stripe.webhookSecret            - Stripe webhook secret (encrypted)
-- anthropic.apiKey                - Anthropic API key
-- claude.apiKey                   - Claude API key
-- openai.apiKey                   - OpenAI API key
-- google.translate.key            - Google Translate API key
