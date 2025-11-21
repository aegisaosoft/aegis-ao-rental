using Azure;
using Azure.Communication.Email;
using CarRental.Api.Data;
using CarRental.Api.Models;
using Microsoft.Extensions.Caching.Memory;

namespace CarRental.Api.Services;

/// <summary>
/// Multi-tenant email service implementation using Azure Communication Services
/// Integrates with database settings for configuration
/// </summary>
public class MultiTenantEmailService
{
    private EmailClient? _emailClient;
    private readonly ISettingsService _settingsService;
    private readonly ITenantBrandingService _brandingService;
    private readonly EmailLocalizationService _localizationService;
    private readonly IEmailTemplateService _templateService;
    private readonly ILogger<MultiTenantEmailService> _logger;
    private readonly IMemoryCache _cache;
    private string? _defaultFromEmail;
    private string? _defaultAzureDomain;

    public MultiTenantEmailService(
        ISettingsService settingsService,
        ITenantBrandingService brandingService,
        EmailLocalizationService localizationService,
        IEmailTemplateService templateService,
        ILogger<MultiTenantEmailService> logger,
        IMemoryCache cache)
    {
        _settingsService = settingsService;
        _brandingService = brandingService;
        _localizationService = localizationService;
        _templateService = templateService;
        _logger = logger;
        _cache = cache;
    }

    /// <summary>
    /// Clear the cached email client to force reinitialization
    /// Useful when domain verification status changes
    /// </summary>
    public void ClearEmailClientCache()
    {
        _emailClient = null;
        _defaultFromEmail = null;
        _logger.LogInformation("Email client cache cleared. Client will be reinitialized on next use.");
    }

    private async Task<EmailClient?> GetEmailClientAsync()
    {
        if (_emailClient != null)
        {
            return _emailClient;
        }

        try
        {
            _logger.LogInformation("GetEmailClientAsync: Attempting to retrieve Azure Communication Services configuration...");
            
            var connectionString = await _settingsService.GetValueAsync("azure.communication.connectionString");
            _defaultFromEmail = await _settingsService.GetValueAsync("azure.communication.fromEmail");

            _logger.LogInformation(
                "GetEmailClientAsync: Connection string present: {HasConnectionString}, From email present: {HasFromEmail}",
                !string.IsNullOrEmpty(connectionString),
                !string.IsNullOrEmpty(_defaultFromEmail));

            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogError(
                    "Azure Communication Services connection string is not configured in database settings. " +
                    "Please set 'azure.communication.connectionString' in the settings table. " +
                    "Emails will not be sent until this is configured.");
                return null;
            }

            if (string.IsNullOrEmpty(_defaultFromEmail))
            {
                _logger.LogError(
                    "Default from email address is not configured in database settings. " +
                    "Please set 'azure.communication.fromEmail' in the settings table. " +
                    "Emails will not be sent until this is configured.");
                return null;
            }

            // Use connection string authentication (simpler, no Azure AD needed)
            if (!string.IsNullOrEmpty(connectionString))
            {
                _logger.LogInformation("Initializing EmailClient with connection string authentication");
                
                _emailClient = new EmailClient(connectionString);
                
                // Extract resource name from connection string for default domain fallback
                _defaultAzureDomain = ExtractDefaultAzureDomain(connectionString);
                
                _logger.LogInformation(
                    "Multi-tenant EmailService initialized with connection string. From email: {FromEmail}, Default Azure domain: {DefaultDomain}",
                    _defaultFromEmail, _defaultAzureDomain);
            }
            
            return _emailClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing Azure Communication Services email client");
            return null;
        }
    }

    /// <summary>
    /// Send email with tenant branding and language support
    /// </summary>
    public async Task<bool> SendEmailAsync(
        Guid companyId,
        string toEmail,
        string subject,
        string htmlContent,
        string? plainTextContent = null,
        EmailLanguage language = EmailLanguage.English)
    {
        string? fromEmail = null;
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("Attempted to send email with empty recipient address for company {CompanyId}", companyId);
                return false;
            }

            var client = await GetEmailClientAsync();
            if (client == null)
            {
                _logger.LogError(
                    "Cannot send email to {Email} for company {CompanyId}: Azure Communication Services is not configured. " +
                    "Please configure 'azure.communication.connectionString' and 'azure.communication.fromEmail' in the settings table.",
                    toEmail, companyId);
                return false;
            }

            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            fromEmail = GetFromEmail(branding);

            var emailContent = new EmailContent(subject)
            {
                Html = htmlContent
            };

            if (!string.IsNullOrEmpty(plainTextContent))
            {
                emailContent.PlainText = plainTextContent;
            }

            var emailMessage = new EmailMessage(fromEmail, toEmail, emailContent);

            _logger.LogInformation(
                "SendEmailAsync: Sending email to {Email} for company {CompanyId} with subject: {Subject} in language {Language}. From: {FromEmail}", 
                toEmail, companyId, subject, language, fromEmail);

            var operation = await client.SendAsync(WaitUntil.Completed, emailMessage);
            var result = operation.Value;

            _logger.LogInformation(
                "SendEmailAsync: Email send operation completed. Status: {Status}",
                result.Status);

            if (result.Status == EmailSendStatus.Succeeded)
            {
                _logger.LogInformation(
                    "SendEmailAsync: Email successfully sent to {Email} for company {CompanyId}. Status: {Status}",
                    toEmail, companyId, result.Status);
                return true;
            }
            else
            {
                _logger.LogWarning(
                    "SendEmailAsync: Email send operation completed but status is {Status} for {Email} company {CompanyId}",
                    result.Status, toEmail, companyId);
                return false;
            }
        }
        catch (RequestFailedException ex)
        {
            if (ex.ErrorCode == "DomainNotLinked")
            {
                // Clear the cached email client to force reinitialization on next attempt
                // This helps if the domain was just verified and needs to be picked up
                ClearEmailClientCache();
                
                var domainForError = fromEmail ?? _defaultFromEmail ?? "unknown";
                _logger.LogError(ex,
                    "Azure Communication Services error: The sender domain '{Domain}' is not LINKED in Azure Communication Services. " +
                    "A verified domain must also be LINKED before it can be used for sending emails. " +
                    "Steps to fix: 1) Go to Azure Portal → Communication Services → Email → Domains, " +
                    "2) Find your verified domain '{Domain}', 3) Click 'Link' or 'Activate' to link the domain for sending. " +
                    "Alternatively, update 'azure.communication.fromEmail' to use the default Azure domain: DoNotReply@aegis-rental-communication.azurecomm.net " +
                    "(works immediately without verification). Email client cache cleared. Error sending email to {Email} for company {CompanyId}. Status: {Status}, ErrorCode: {ErrorCode}",
                    domainForError, domainForError, toEmail, companyId, ex.Status, ex.ErrorCode);
            }
            else if (ex.ErrorCode == "InvalidSenderUserName")
            {
                var emailForError = fromEmail ?? _defaultFromEmail ?? "unknown";
                // Extract username from email (e.g., "noreply" from "noreply@mail.aegis-rental.com")
                var username = emailForError.Contains('@') ? emailForError.Split('@')[0] : "unknown";
                var domain = emailForError.Contains('@') ? emailForError.Split('@')[1] : "unknown";
                
                _logger.LogWarning(ex,
                    "Azure Communication Services error: The sender username '{Username}' is not configured/allowed for domain '{Domain}'. " +
                    "Attempting to retry with default Azure domain. Error sending email to {Email} for company {CompanyId}. Status: {Status}, ErrorCode: {ErrorCode}",
                    username, domain, toEmail, companyId, ex.Status, ex.ErrorCode);
                
                // Retry with default Azure domain if available
                if (!string.IsNullOrEmpty(_defaultAzureDomain) && fromEmail != _defaultAzureDomain)
                {
                    _logger.LogInformation(
                        "Retrying email send with default Azure domain: {DefaultDomain}",
                        _defaultAzureDomain);
                    
                    try
                    {
                        var retryClient = await GetEmailClientAsync();
                        if (retryClient != null)
                        {
                            var retryEmailContent = new EmailContent(subject)
                            {
                                Html = htmlContent
                            };
                            
                            if (!string.IsNullOrEmpty(plainTextContent))
                            {
                                retryEmailContent.PlainText = plainTextContent;
                            }
                            
                            var retryEmailMessage = new EmailMessage(_defaultAzureDomain, toEmail, retryEmailContent);
                            var retryOperation = await retryClient.SendAsync(WaitUntil.Completed, retryEmailMessage);
                            var retryResult = retryOperation.Value;
                            
                            if (retryResult.Status == EmailSendStatus.Succeeded)
                            {
                                _logger.LogInformation(
                                    "Email successfully sent to {Email} using default Azure domain {DefaultDomain} for company {CompanyId}",
                                    toEmail, _defaultAzureDomain, companyId);
                                return true;
                            }
                            else
                            {
                                _logger.LogWarning(
                                    "Email send retry with default domain completed but status is {Status} for {Email} company {CompanyId}",
                                    retryResult.Status, toEmail, companyId);
                            }
                        }
                    }
                    catch (Exception retryEx)
                    {
                        _logger.LogError(retryEx,
                            "Failed to retry email send with default Azure domain {DefaultDomain} to {Email} for company {CompanyId}",
                            _defaultAzureDomain, toEmail, companyId);
                    }
                }
                
                // Clear the cached email client to force reinitialization on next attempt
                ClearEmailClientCache();
                
                _logger.LogError(
                    "Azure Communication Services error: The sender username '{Username}' is not configured/allowed for domain '{Domain}'. " +
                    "In Azure Communication Services, you must configure which email addresses (usernames) are allowed to send from a custom domain. " +
                    "Steps to fix: 1) Go to Azure Portal → Communication Services → Email → Domains, " +
                    "2) Find your domain '{Domain}', 3) Click on it to view details, " +
                    "4) Add '{Username}' to the list of allowed sender addresses, or use a different username that is already configured. " +
                    "Alternatively, update 'azure.communication.fromEmail' to use the default Azure domain: {DefaultDomain} " +
                    "(works immediately without configuration). Email client cache cleared. Error sending email to {Email} for company {CompanyId}.",
                    username, domain, domain, username, _defaultAzureDomain ?? "DoNotReply@<resource-name>.azurecomm.net", toEmail, companyId);
            }
            else
            {
                _logger.LogError(ex,
                    "Azure Communication Services error sending email to {Email} for company {CompanyId}. Status: {Status}, ErrorCode: {ErrorCode}",
                    toEmail, companyId, ex.Status, ex.ErrorCode);
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Unexpected error sending email to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send invitation email (for customer creation)
    /// </summary>
    public async Task<bool> SendInvitationEmailAsync(
        Guid companyId,
        string toEmail,
        string invitationUrl,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("email_verification", language);
            var htmlContent = _templateService.GenerateInvitationTemplate(branding, invitationUrl, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending invitation email to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send invitation email with booking details and password
    /// </summary>
    public async Task<bool> SendInvitationEmailWithBookingDetailsAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string invitationUrl,
        string temporaryPassword,
        string bookingNumber,
        DateTime pickupDate,
        DateTime returnDate,
        string vehicleName,
        string pickupLocation,
        decimal totalAmount,
        string currency,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("booking_invitation", language);
            var htmlContent = _templateService.GenerateInvitationWithBookingTemplate(
                branding, 
                customerName,
                invitationUrl, 
                temporaryPassword,
                bookingNumber,
                pickupDate,
                returnDate,
                vehicleName,
                pickupLocation,
                totalAmount,
                currency,
                language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending invitation email with booking details to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send booking confirmation email
    /// </summary>
    public async Task<bool> SendBookingConfirmationAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string bookingId,
        DateTime pickupDate,
        DateTime returnDate,
        string vehicleName,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("booking_confirmation", language);
            var htmlContent = _templateService.GenerateBookingConfirmationTemplate(
                branding, customerName, bookingId, pickupDate, returnDate, vehicleName, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending booking confirmation to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send pickup reminder email
    /// </summary>
    public async Task<bool> SendPickupReminderAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string bookingId,
        DateTime pickupDate,
        string vehicleName,
        string pickupLocation,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("pickup_reminder", language);
            var htmlContent = _templateService.GeneratePickupReminderTemplate(
                branding, customerName, bookingId, pickupDate, vehicleName, pickupLocation, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending pickup reminder to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send return reminder email
    /// </summary>
    public async Task<bool> SendReturnReminderAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string bookingId,
        DateTime returnDate,
        string vehicleName,
        string returnLocation,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("return_reminder", language);
            var htmlContent = _templateService.GenerateReturnReminderTemplate(
                branding, customerName, bookingId, returnDate, vehicleName, returnLocation, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending return reminder to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send payment confirmation email
    /// </summary>
    public async Task<bool> SendPaymentConfirmationAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string bookingId,
        decimal amount,
        string paymentMethod,
        string currencySymbol,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("payment_confirmation", language);
            var htmlContent = _templateService.GeneratePaymentConfirmationTemplate(
                branding, customerName, bookingId, amount, paymentMethod, currencySymbol, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending payment confirmation to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send invoice email
    /// </summary>
    public async Task<bool> SendInvoiceAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string invoiceId,
        decimal totalAmount,
        string currencySymbol,
        byte[]? invoicePdf = null,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("invoice", language);
            var htmlContent = _templateService.GenerateInvoiceTemplate(
                branding, customerName, invoiceId, totalAmount, currencySymbol, language);

            // Note: PDF attachment would need to be handled separately with Azure Communication Services
            // For now, we'll just send the HTML email
            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending invoice to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send overdue return notification email
    /// </summary>
    public async Task<bool> SendOverdueReturnNotificationAsync(
        Guid companyId,
        string toEmail,
        string customerName,
        string bookingId,
        DateTime expectedReturnDate,
        string vehicleName,
        decimal lateFee,
        string currencySymbol,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("overdue_return", language);
            var htmlContent = _templateService.GenerateOverdueReturnTemplate(
                branding, customerName, bookingId, expectedReturnDate, vehicleName, lateFee, currencySymbol, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending overdue return notification to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    /// <summary>
    /// Send password reset email
    /// </summary>
    public async Task<bool> SendPasswordResetAsync(
        Guid companyId,
        string toEmail,
        string resetUrl,
        EmailLanguage language = EmailLanguage.English)
    {
        try
        {
            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var subject = _localizationService.Get("password_reset", language);
            var htmlContent = _templateService.GeneratePasswordResetTemplate(branding, resetUrl, language);

            return await SendEmailAsync(companyId, toEmail, subject, htmlContent, null, language);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "Error sending password reset to {Email} for company {CompanyId}", 
                toEmail, companyId);
            return false;
        }
    }

    private string GetFromEmail(TenantBranding branding)
    {
        // Use tenant-specific FROM email if configured, otherwise use default
        return !string.IsNullOrEmpty(branding.FromEmail) 
            ? branding.FromEmail 
            : _defaultFromEmail ?? "noreply@mail.aegis-rental.com";
    }

    /// <summary>
    /// Extracts the default Azure Communication Services domain from the connection string
    /// Format: DoNotReply@&lt;resource-name&gt;.azurecomm.net
    /// </summary>
    private string? ExtractDefaultAzureDomain(string connectionString)
    {
        try
        {
            // Connection string format: endpoint=https://<resource-name>.<region>.communication.azure.com/;accesskey=...
            var endpointMatch = System.Text.RegularExpressions.Regex.Match(
                connectionString, 
                @"endpoint=https://([^.]+)\.");
            
            if (endpointMatch.Success && endpointMatch.Groups.Count > 1)
            {
                var resourceName = endpointMatch.Groups[1].Value;
                return $"DoNotReply@{resourceName}.azurecomm.net";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract default Azure domain from connection string");
        }
        
        return null;
    }
}

