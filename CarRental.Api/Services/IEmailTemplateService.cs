using CarRental.Api.Models;

namespace CarRental.Api.Services;

/// <summary>
/// Interface for multi-tenant email template generation with localization
/// </summary>
public interface IEmailTemplateService
{
    string GenerateBookingConfirmationTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime pickupDate,
        DateTime returnDate,
        string vehicleName,
        EmailLanguage language);

    string GeneratePickupReminderTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime pickupDate,
        string vehicleName,
        string pickupLocation,
        EmailLanguage language);

    string GenerateReturnReminderTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime returnDate,
        string vehicleName,
        string returnLocation,
        EmailLanguage language);

    string GeneratePaymentConfirmationTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        decimal amount,
        string paymentMethod,
        string currencySymbol,
        EmailLanguage language);

    string GenerateInvoiceTemplate(
        TenantBranding branding,
        string customerName,
        string invoiceId,
        decimal totalAmount,
        string currencySymbol,
        EmailLanguage language);

    string GenerateOverdueReturnTemplate(
        TenantBranding branding,
        string customerName,
        string bookingId,
        DateTime expectedReturnDate,
        string vehicleName,
        decimal lateFee,
        string currencySymbol,
        EmailLanguage language);

    string GenerateInvitationTemplate(
        TenantBranding branding,
        string invitationUrl,
        EmailLanguage language);

    string GenerateInvitationWithBookingTemplate(
        TenantBranding branding,
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
        EmailLanguage language);

    string GeneratePasswordResetTemplate(
        TenantBranding branding,
        string resetUrl,
        EmailLanguage language);
}

