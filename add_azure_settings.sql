-- Add Azure settings to the settings table
-- Note: ClientSecret will be automatically encrypted by SettingsService when accessed.
-- If inserting via SQL directly, the value will be stored in plaintext initially,
-- but will be automatically encrypted on first access via SettingsService.
--
-- IMPORTANT: Replace 'YOUR_SUBSCRIPTION_GUID_HERE' with your actual Azure Subscription GUID.
-- To find your subscription GUID, run one of these commands:
--   Azure CLI: az account show --query id -o tsv
--   PowerShell: (Get-AzSubscription).Id
--   Portal: Go to Subscriptions -> Select your subscription -> Overview -> Subscription ID
-- The subscription ID must be a GUID format: 12345678-1234-1234-1234-123456789012
-- DO NOT use the subscription display name (e.g., "Azure subscription 1")

-- Insert Azure Subscription ID
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.subscriptionId', '2e6ffba7-ad47-4df2-aa4b-274ff0399296', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure Tenant ID
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.tenantId', 'c040556a-1092-41aa-8c81-61dd84df3b2e', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure Client ID
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.clientId', 'ebfa979f-7c84-4b30-90d5-ea8b53fb5866', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure Client Secret
-- 
-- IMPORTANT: Use the SECRET VALUE, not the Secret ID!
-- When you create a client secret in Azure AD, you get:
--   1. Secret ID (GUID) - DO NOT USE THIS
--   2. Secret Value (long random string) - USE THIS
-- The secret value is only shown once when created. If you don't have it, create a new secret.
--
-- NOTE: This value will be automatically encrypted by SettingsService when accessed.
-- The SettingsService has been configured to encrypt 'azure.clientSecret'.
-- If inserting via SQL directly, the value will be stored in plaintext initially,
-- but will be automatically encrypted on first access via SettingsService.
-- For production, consider using the Settings API endpoint to ensure immediate encryption.
--
-- Insert Azure Client Secret (actual secret value)
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.clientSecret', 'zPe8Q~COVt6Bsz8e2Nx1SMYYynGJw3Yz.Emy_bLn', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure Resource Group
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.resourceGroup', 'aegis_rentals', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure ACME Bot Resource Group
-- TODO: Replace 'your-acmebot-resource-group' with your actual ACME Bot resource group name
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.acmeBotResourceGroup', 'your-acmebot-resource-group', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure DNS Zone Name
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.dnsZoneName', 'aegis-rental.com', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure App Service Host
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.appServiceHost', 'aegis-rental-web-gvcxbpccfncfbjh4.canadacentral-01.azurewebsites.net', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure App Service Name
-- TODO: Replace 'your-app-service-name' with your actual App Service name (without .azurewebsites.net)
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.appServiceName', 'your-app-service-name', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure Key Vault Name
-- TODO: Replace 'your-key-vault-name' with your actual Key Vault name (without .vault.azure.net)
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.keyVaultName', 'your-key-vault-name', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Insert Azure Certificate Name (Optional - defaults to 'aegis-rental-wildcard' if not provided)
-- TODO: Replace 'aegis-rental-wildcard' with your actual certificate name in Key Vault
INSERT INTO public.settings (key, value, created_at, updated_at)
VALUES ('azure.certificateName', 'aegis-rental-wildcard', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

