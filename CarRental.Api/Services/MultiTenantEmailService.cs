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

    private async Task<EmailClient> GetEmailClientAsync()
    {
        if (_emailClient != null)
        {
            return _emailClient;
        }

        var connectionString = await _settingsService.GetValueAsync("azure.communication.connectionString");
        _defaultFromEmail = await _settingsService.GetValueAsync("azure.communication.fromEmail");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Azure Communication Services connection string is not configured in database settings. Please set 'azure.communication.connectionString'.");
        }

        if (string.IsNullOrEmpty(_defaultFromEmail))
        {
            throw new InvalidOperationException("Default from email address is not configured in database settings. Please set 'azure.communication.fromEmail'.");
        }

        _emailClient = new EmailClient(connectionString);
        _logger.LogInformation("Multi-tenant EmailService initialized with Azure Communication Services");
        
        return _emailClient;
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
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                _logger.LogWarning("Attempted to send email with empty recipient address for company {CompanyId}", companyId);
                return false;
            }

            var branding = await _brandingService.GetTenantBrandingAsync(companyId);
            var fromEmail = GetFromEmail(branding);
            var client = await GetEmailClientAsync();

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
                "Sending email to {Email} for company {CompanyId} with subject: {Subject} in language {Language}", 
                toEmail, companyId, subject, language);

            var operation = await client.SendAsync(WaitUntil.Completed, emailMessage);

            if (operation.Value.Status == EmailSendStatus.Succeeded)
            {
                _logger.LogInformation(
                    "Email successfully sent to {Email} for company {CompanyId}. Status: {Status}",
                    toEmail, companyId, operation.Value.Status);
                return true;
            }
            else
            {
                _logger.LogWarning(
                    "Email send operation completed but status is {Status} for {Email} company {CompanyId}",
                    operation.Value.Status, toEmail, companyId);
                return false;
            }
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex,
                "Azure Communication Services error sending email to {Email} for company {CompanyId}. Status: {Status}, ErrorCode: {ErrorCode}",
                toEmail, companyId, ex.Status, ex.ErrorCode);
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
}

