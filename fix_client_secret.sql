-- Fix Azure Client Secret in the settings table
-- 
-- IMPORTANT: The client secret must be the SECRET VALUE, not the Secret ID!
-- 
-- When you create a client secret in Azure AD, you get:
--   1. Secret ID (GUID like: bb833e6b-8b63-4a87-b5bd-1935d25712ad) - DO NOT USE THIS
--   2. Secret Value (long random string like: abc~DEF123ghi-JKL456mno-PQR789stu) - USE THIS
--
-- To get the correct client secret value:
--   1. Go to Azure Portal -> Azure Active Directory -> App registrations
--   2. Find your app (Client ID: ebfa979f-7c84-4b30-90d5-ea8b53fb5866)
--   3. Go to "Certificates & secrets"
--   4. If the secret value is not visible (it's only shown once when created), you need to:
--      - Create a NEW client secret
--      - Copy the VALUE immediately (it won't be shown again!)
--      - Use that value below
--
-- OR use Azure CLI:
--   az ad app credential reset --id ebfa979f-7c84-4b30-90d5-ea8b53fb5866 --append
--   (This will create a new secret and show the value)

-- Update the client secret with the actual SECRET VALUE
UPDATE public.settings 
SET value = 'zPe8Q~COVt6Bsz8e2Nx1SMYYynGJw3Yz.Emy_bLn', updated_at = NOW()
WHERE key = 'azure.clientSecret';

-- Verify the update (value should be a long random string, not a GUID)
SELECT key, 
       CASE 
         WHEN LENGTH(value) > 30 THEN 'Value looks correct (long string)'
         ELSE 'WARNING: Value may be incorrect (too short or looks like GUID)'
       END as validation,
       LENGTH(value) as value_length,
       updated_at 
FROM public.settings 
WHERE key = 'azure.clientSecret';

