using CarRental.Api.Models;

namespace CarRental.Api.Services;

/// <summary>
/// Service for generating HTML email templates with tenant-specific branding and localization
/// </summary>
public class EmailTemplateService : IEmailTemplateService
{
    private readonly EmailLocalizationService _localization;

    public EmailTemplateService(EmailLocalizationService localization)
    {
        _localization = localization;
    }

    public string GenerateBookingConfirmationTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime pickupDate,
        DateTime returnDate,
        string vehicleName,
        EmailLanguage language)
    {
        var loc = _localization;
        var days = (returnDate - pickupDate).Days;
        
        var content = $@"
            <h1 style='color: {branding.BrandColor}; margin-bottom: 20px;'>{loc.Get("booking_confirmation", language)}</h1>
            <p>{loc.Get("dear", language)} {customerName},</p>
            <p>{loc.Get("thank_you", language)} {branding.CompanyName}! {loc.Get("booking_confirmed", language)}</p>
            
            <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <h2 style='margin-top: 0; color: {branding.BrandColor};'>{loc.Get("booking_details", language)}</h2>
                <p><strong>{loc.Get("booking_id", language)}:</strong> {bookingId}</p>
                <p><strong>{loc.Get("vehicle", language)}:</strong> {vehicleName}</p>
                <p><strong>{loc.Get("pickup_date", language)}:</strong> {pickupDate:dddd, MMMM dd, yyyy 'at' hh:mm tt}</p>
                <p><strong>{loc.Get("return_date", language)}:</strong> {returnDate:dddd, MMMM dd, yyyy 'at' hh:mm tt}</p>
                <p><strong>{loc.Get("duration", language)}:</strong> {days} {loc.Get("days", language)}</p>
            </div>

            <p><strong>{loc.Get("whats_next", language)}</strong></p>
            <ul>
                <li>{loc.Get("pickup_reminder_24h", language)}</li>
                <li>{loc.Get("bring_license", language)}</li>
                <li>{loc.Get("arrive_early", language)}</li>
            </ul>

            <p>{loc.Get("if_questions", language)}</p>
            <p>{loc.Get("look_forward", language)}</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GeneratePickupReminderTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime pickupDate,
        string vehicleName,
        string pickupLocation,
        EmailLanguage language)
    {
        var loc = _localization;
        
        var content = $@"
            <h1 style='color: {branding.BrandColor}; margin-bottom: 20px;'>{loc.Get("pickup_reminder", language)}</h1>
            <p>{loc.Get("dear", language)} {customerName},</p>
            <p>{loc.Get("pickup_tomorrow", language)}</p>
            
            <div style='background-color: #fef3c7; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #f59e0b;'>
                <h2 style='margin-top: 0; color: #92400e;'>{loc.Get("pickup_information", language)}</h2>
                <p><strong>{loc.Get("booking_id", language)}:</strong> {bookingId}</p>
                <p><strong>{loc.Get("vehicle", language)}:</strong> {vehicleName}</p>
                <p><strong>{loc.Get("pickup_date", language)} & {loc.Get("pickup_location", language)}:</strong> {pickupDate:dddd, MMMM dd, yyyy 'at' hh:mm tt}</p>
                <p><strong>{loc.Get("pickup_location", language)}:</strong> {pickupLocation}</p>
            </div>

            <p><strong>{loc.Get("important_reminders", language)}:</strong></p>
            <ul>
                <li>✓ {loc.Get("bring_drivers_license", language)}</li>
                <li>✓ {loc.Get("bring_credit_card", language)}</li>
                <li>✓ {loc.Get("arrive_15_min", language)}</li>
                <li>✓ {loc.Get("review_vehicle", language)}</li>
            </ul>

            <p>{loc.Get("see_you_tomorrow", language)}</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GenerateReturnReminderTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime returnDate,
        string vehicleName,
        string returnLocation,
        EmailLanguage language)
    {
        var loc = _localization;
        
        var content = $@"
            <h1 style='color: {branding.BrandColor}; margin-bottom: 20px;'>{loc.Get("return_reminder", language)}</h1>
            <p>{loc.Get("dear", language)} {customerName},</p>
            <p>{loc.Get("return_tomorrow", language)}</p>
            
            <div style='background-color: #dbeafe; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid {branding.BrandColor};'>
                <h2 style='margin-top: 0; color: {branding.BrandColor};'>{loc.Get("return_information", language)}</h2>
                <p><strong>{loc.Get("booking_id", language)}:</strong> {bookingId}</p>
                <p><strong>{loc.Get("vehicle", language)}:</strong> {vehicleName}</p>
                <p><strong>{loc.Get("return_date", language)}:</strong> {returnDate:dddd, MMMM dd, yyyy 'at' hh:mm tt}</p>
                <p><strong>{loc.Get("return_location", language)}:</strong> {returnLocation}</p>
            </div>

            <p><strong>{loc.Get("before_returning", language)}:</strong></p>
            <ul>
                <li>✓ {loc.Get("refill_gas", language)}</li>
                <li>✓ {loc.Get("remove_belongings", language)}</li>
                <li>✓ {loc.Get("clean_interior", language)}</li>
                <li>✓ {loc.Get("avoid_late_fees", language)}</li>
            </ul>

            <p>{loc.Get("thank_you", language)} {branding.CompanyName}!</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GeneratePaymentConfirmationTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        decimal amount,
        string paymentMethod,
        string currencySymbol,
        EmailLanguage language)
    {
        var loc = _localization;
        var successColor = branding.SecondaryColor ?? "#059669";
        
        var content = $@"
            <h1 style='color: {successColor}; margin-bottom: 20px;'>{loc.Get("payment_confirmation", language)}</h1>
            <p>{loc.Get("dear", language)} {customerName},</p>
            <p>{loc.Get("payment_received", language)}</p>
            
            <div style='background-color: #d1fae5; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid {successColor};'>
                <h2 style='margin-top: 0; color: #065f46;'>{loc.Get("payment_details", language)}</h2>
                <p><strong>{loc.Get("booking_id", language)}:</strong> {bookingId}</p>
                <p><strong>{loc.Get("amount_paid", language)}:</strong> {currencySymbol}{amount:N2}</p>
                <p><strong>{loc.Get("payment_method", language)}:</strong> {paymentMethod}</p>
                <p><strong>{loc.Get("payment_date", language)}:</strong> {DateTime.UtcNow:MMMM dd, yyyy 'at' hh:mm tt} UTC</p>
            </div>

            <p>{loc.Get("invoice_sent", language)}</p>
            <p>{loc.Get("payment_questions", language)}</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GenerateInvoiceTemplate(
        TenantBranding branding,
        string customerName,
        string invoiceId,
        decimal totalAmount,
        string currencySymbol,
        EmailLanguage language)
    {
        var loc = _localization;
        
        var content = $@"
            <h1 style='color: {branding.BrandColor}; margin-bottom: 20px;'>{loc.Get("invoice", language)}</h1>
            <p>{loc.Get("dear", language)} {customerName},</p>
            <p>{loc.Get("invoice_attached", language)}</p>
            
            <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0;'>
                <h2 style='margin-top: 0; color: {branding.BrandColor};'>{loc.Get("invoice_summary", language)}</h2>
                <p><strong>{loc.Get("invoice_id", language)}:</strong> {invoiceId}</p>
                <p><strong>{loc.Get("invoice_date", language)}:</strong> {DateTime.UtcNow:MMMM dd, yyyy}</p>
                <p style='font-size: 24px; color: {branding.BrandColor}; margin-top: 20px;'>
                    <strong>{loc.Get("total_amount", language)}: {currencySymbol}{totalAmount:N2}</strong>
                </p>
            </div>

            <p>{loc.Get("invoice_pdf_attached", language)}</p>
            <p>{loc.Get("thank_you_business", language)}</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GenerateOverdueReturnTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime expectedReturnDate,
        string vehicleName,
        decimal lateFee,
        string currencySymbol,
        EmailLanguage language)
    {
        var loc = _localization;
        var daysOverdue = (DateTime.UtcNow - expectedReturnDate).Days;
        
        var content = $@"
            <h1 style='color: #dc2626; margin-bottom: 20px;'>⚠️ {loc.Get("overdue_return", language)}</h1>
            <p>{loc.Get("dear", language)} {customerName},</p>
            <p>{loc.Get("vehicle_not_returned", language)}</p>
            
            <div style='background-color: #fee2e2; padding: 20px; border-radius: 8px; margin: 20px 0; border-left: 4px solid #dc2626;'>
                <h2 style='margin-top: 0; color: #991b1b;'>{loc.Get("overdue_details", language)}</h2>
                <p><strong>{loc.Get("booking_id", language)}:</strong> {bookingId}</p>
                <p><strong>{loc.Get("vehicle", language)}:</strong> {vehicleName}</p>
                <p><strong>{loc.Get("expected_return_date", language)}:</strong> {expectedReturnDate:dddd, MMMM dd, yyyy 'at' hh:mm tt}</p>
                <p><strong>{loc.Get("days_overdue", language)}:</strong> {daysOverdue} {loc.Get("days", language)}</p>
                <p style='font-size: 20px; color: #dc2626; margin-top: 15px;'>
                    <strong>{loc.Get("late_fee", language)}: {currencySymbol}{lateFee:N2}</strong>
                </p>
            </div>

            <p><strong style='color: #dc2626;'>{loc.Get("immediate_action", language)}:</strong></p>
            <ul>
                <li>{loc.Get("return_immediately", language)}</li>
                <li>{loc.Get("contact_if_issues", language)}</li>
                <li>{loc.Get("fees_continue", language)}</li>
            </ul>

            <p style='color: #dc2626;'><strong>{loc.Get("vehicle_stolen_note", language)}</strong></p>
            
            <p>{loc.Get("understand_circumstances", language)}</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GenerateInvitationTemplate(
        TenantBranding branding,
        string invitationUrl,
        EmailLanguage language)
    {
        var loc = _localization;
        
        var content = $@"
            <h1 style='color: {branding.BrandColor}; margin-bottom: 20px;'>{loc.Get("email_verification", language)}</h1>
            <p>{loc.Get("dear", language)} Customer,</p>
            <p>{loc.Get("welcome_aboard", language)}</p>
            <p>{loc.Get("verify_email", language)}</p>
            
            <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0; text-align: center;'>
                <p style='margin-bottom: 20px;'>{loc.Get("click_button_verify", language)}</p>
                <a href='{invitationUrl}' style='display: inline-block; padding: 12px 24px; background-color: {branding.BrandColor}; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold;'>{loc.Get("verify_email_button", language)}</a>
            </div>

            <p style='font-size: 12px; color: #6b7280;'>{loc.Get("verification_expires", language)}</p>
            <p>{loc.Get("thank_you", language)} {branding.CompanyName}!</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    public string GeneratePasswordResetTemplate(
        TenantBranding branding,
        string resetUrl,
        EmailLanguage language)
    {
        var loc = _localization;
        
        var content = $@"
            <h1 style='color: {branding.BrandColor}; margin-bottom: 20px;'>{loc.Get("password_reset", language)}</h1>
            <p>{loc.Get("dear", language)} Customer,</p>
            <p>{loc.Get("password_reset_requested", language)}</p>
            
            <div style='background-color: #f3f4f6; padding: 20px; border-radius: 8px; margin: 20px 0; text-align: center;'>
                <p style='margin-bottom: 20px;'>{loc.Get("click_button_reset", language)}</p>
                <a href='{resetUrl}' style='display: inline-block; padding: 12px 24px; background-color: {branding.BrandColor}; color: #ffffff; text-decoration: none; border-radius: 6px; font-weight: bold;'>{loc.Get("reset_password", language)}</a>
            </div>

            <p style='font-size: 12px; color: #6b7280;'>{loc.Get("link_expires", language)}</p>
            <p style='font-size: 12px; color: #6b7280;'>{loc.Get("not_requested", language)}</p>
            <p style='font-size: 12px; color: #6b7280;'>{loc.Get("security_tip", language)}</p>
        ";

        return GetBaseTemplate(branding, content, language);
    }

    private string GetBaseTemplate(TenantBranding branding, string content, EmailLanguage language)
    {
        var loc = _localization;
        
        // Build logo section if logo URL is provided
        var logoSection = string.Empty;
        if (!string.IsNullOrEmpty(branding.LogoUrl))
        {
            logoSection = $@"
                <div style='text-align: center; margin-bottom: 20px;'>
                    <img src='{branding.LogoUrl}' alt='{branding.CompanyName}' style='max-width: 200px; max-height: 80px;' />
                </div>";
        }

        // Build address section if provided
        var addressSection = string.Empty;
        if (!string.IsNullOrEmpty(branding.Address))
        {
            addressSection = $@"
                <p style='margin: 10px 0 0 0; font-size: 12px;'>
                    {branding.Address}
                </p>";
        }

        // Build footer text if provided
        var footerText = string.Empty;
        if (!string.IsNullOrEmpty(branding.FooterText))
        {
            footerText = $@"
                <p style='margin: 15px 0 0 0; font-size: 13px; font-style: italic; color: #6b7280;'>
                    {branding.FooterText}
                </p>";
        }

        // Build website link
        var websiteLink = string.Empty;
        if (!string.IsNullOrEmpty(branding.WebsiteUrl))
        {
            websiteLink = $@"
                <p style='margin: 10px 0 0 0; font-size: 12px;'>
                    Website: <a href='{branding.WebsiteUrl}' style='color: {branding.BrandColor}; text-decoration: none;'>{branding.WebsiteUrl.Replace("https://", "").Replace("http://", "")}</a>
                </p>";
        }

        var phoneSection = string.Empty;
        if (!string.IsNullOrEmpty(branding.SupportPhone))
        {
            phoneSection = $"<br>Phone: <a href='tel:{branding.SupportPhone}' style='color: {branding.BrandColor}; text-decoration: none;'>{branding.SupportPhone}</a>";
        }

        return $@"
<!DOCTYPE html>
<html lang='{LanguageCodes.ToCode(language)}'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{branding.CompanyName} Notification</title>
</head>
<body style='margin: 0; padding: 0; font-family: Arial, sans-serif; background-color: #f9fafb;'>
    <table role='presentation' style='width: 100%; border-collapse: collapse;'>
        <tr>
            <td style='padding: 40px 0;'>
                <table role='presentation' style='width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1);'>
                    <!-- Header -->
                    <tr>
                        <td style='padding: 40px 40px 20px 40px; text-align: center; background-color: {branding.BrandColor}; border-radius: 8px 8px 0 0;'>
                            {logoSection}
                            <h1 style='margin: 0; color: #ffffff; font-size: 28px; font-weight: bold;'>{branding.CompanyName}</h1>
                        </td>
                    </tr>
                    
                    <!-- Content -->
                    <tr>
                        <td style='padding: 40px; color: #374151; line-height: 1.6;'>
                            {content}
                        </td>
                    </tr>
                    
                    <!-- Footer -->
                    <tr>
                        <td style='padding: 30px 40px; background-color: #f3f4f6; border-radius: 0 0 8px 8px; text-align: center; color: #6b7280; font-size: 14px;'>
                            <p style='margin: 0 0 10px 0;'>
                                <strong>{branding.CompanyName}</strong>
                            </p>
                            <p style='margin: 0 0 10px 0;'>
                                Email: <a href='mailto:{branding.SupportEmail}' style='color: {branding.BrandColor}; text-decoration: none;'>{branding.SupportEmail}</a>{phoneSection}
                            </p>
                            {websiteLink}
                            {addressSection}
                            {footerText}
                            <p style='margin: 20px 0 0 0; font-size: 12px;'>
                                © {DateTime.UtcNow.Year} {branding.CompanyName}. {loc.Get("all_rights_reserved", language)}
                            </p>
                            <p style='margin: 10px 0 0 0; font-size: 12px;'>
                                {loc.Get("automated_message", language)}
                            </p>
                        </td>
                    </tr>
                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
