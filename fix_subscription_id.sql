-- Fix Azure Subscription ID in the settings table
-- Replace 'YOUR_SUBSCRIPTION_GUID_HERE' with your actual Azure Subscription GUID
-- 
-- To find your subscription GUID, run one of these commands:
--   Azure CLI: az account show --query id -o tsv
--   PowerShell: (Get-AzSubscription).Id
--   Portal: Go to Subscriptions -> Select your subscription -> Overview -> Subscription ID
--
-- The subscription ID must be a GUID format: 12345678-1234-1234-1234-123456789012
-- DO NOT use the subscription display name (e.g., "Azure subscription 1")

-- Update the subscription ID with the actual GUID
UPDATE public.settings 
SET value = '2e6ffba7-ad47-4df2-aa4b-274ff0399296', updated_at = NOW()
WHERE key = 'azure.subscriptionId';

-- Verify the update
SELECT key, value, updated_at 
FROM public.settings 
WHERE key = 'azure.subscriptionId';

