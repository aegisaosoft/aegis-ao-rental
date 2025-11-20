# Multi-Tenant Multi-Language Email System Integration Guide

## Overview

The email system has been integrated with:
- **Multi-tenant support**: Each company gets unique branding (logo, colors, contact info)
- **Multi-language support**: Emails are sent in the company's configured language (English, Spanish, Portuguese, French, German)
- **Azure Communication Services**: Email delivery via Azure (configured from database settings)

## Configuration

### 1. Database Settings

Configure the following settings in the `settings` table:

```sql
-- Azure Communication Services Connection String
INSERT INTO settings (key, value, created_at, updated_at) 
VALUES ('azure.communication.connectionString', 'endpoint=https://your-resource.communication.azure.com/;accesskey=YOUR_KEY', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();

-- Default From Email Address
INSERT INTO settings (key, value, created_at, updated_at) 
VALUES ('azure.communication.fromEmail', 'noreply@mail.aegis-rental.com', NOW(), NOW())
ON CONFLICT (key) DO UPDATE SET value = EXCLUDED.value, updated_at = NOW();
```

### 2. Company Branding

Branding is automatically loaded from the `companies` table:
- `company_name` → Company name in emails
- `primary_color` → Primary brand color (hex)
- `secondary_color` → Secondary brand color (hex)
- `logo_url` → Company logo URL
- `email` → Support email
- `website` → Company website
- `motto` → Footer text
- `language` → Email language (en, es, pt, fr, de)

### 3. Usage Example

```csharp
// Inject MultiTenantEmailService
private readonly MultiTenantEmailService _emailService;

// Send invitation email
await _emailService.SendInvitationEmailAsync(
    companyId: companyId,
    toEmail: "customer@example.com",
    invitationUrl: "https://example.com/register?email=...",
    language: EmailLanguage.English
);

// Send generic email
await _emailService.SendEmailAsync(
    companyId: companyId,
    toEmail: "customer@example.com",
    subject: "Subject",
    htmlContent: "<html>...</html>",
    plainTextContent: "Plain text",
    language: EmailLanguage.Spanish
);
```

## Features

### Multi-Tenant Branding
- Each company's emails use their own:
  - Logo
  - Brand colors
  - Company name
  - Support contact information
  - Footer text

### Multi-Language Support
- Supported languages: English, Spanish, Portuguese, French, German
- Language is determined from `company.language` field
- Falls back to English if translation not available
- All email templates are localized

### Azure Communication Services
- Connection string stored securely in database settings
- Supports custom FROM email addresses per tenant
- Automatic retry and error handling
- Comprehensive logging

## Files Created

1. `Models/EmailLanguage.cs` - Language enum and code mappings
2. `Models/TenantBranding.cs` - Branding model
3. `Services/EmailLocalizationService.cs` - Translation service
4. `Services/ITenantBrandingService.cs` - Branding service interface
5. `Services/TenantBrandingService.cs` - Branding service (uses Company model)
6. `Services/MultiTenantEmailService.cs` - Main email service with Azure integration

## Integration Points

### CustomersController
- Updated to use `MultiTenantEmailService` for invitation emails
- Automatically detects company language
- Uses company branding

### Program.cs
- Registered new services:
  - `EmailLocalizationService` (singleton)
  - `ITenantBrandingService` → `TenantBrandingService`
  - `MultiTenantEmailService`

## Next Steps

1. **Configure Azure Communication Services**:
   - Get connection string from Azure portal
   - Add to database settings: `azure.communication.connectionString`
   - Add default FROM email: `azure.communication.fromEmail`

2. **Update Company Branding**:
   - Set `primary_color` and `secondary_color` in companies table
   - Upload logos and set `logo_url`
   - Set `language` field for each company

3. **Test Email Sending**:
   - Create a test customer without password
   - Verify invitation email is sent with correct branding and language

## Notes

- The system uses the existing `Company` model for branding (no separate TenantBrandings table needed)
- Branding is cached for 24 hours for performance
- Language defaults to English if not specified
- All email operations are logged for debugging

