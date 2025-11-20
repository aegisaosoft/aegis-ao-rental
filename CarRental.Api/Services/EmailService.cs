/*
 *
 * Copyright (c) 2025 Alexander Orlov.
 * 34 Middletown Ave Atlantic Highlands NJ 07716
 *
 * THIS SOFTWARE IS THE CONFIDENTIAL AND PROPRIETARY INFORMATION OF
 * Alexander Orlov. ("CONFIDENTIAL INFORMATION"). YOU SHALL NOT DISCLOSE
 * SUCH CONFIDENTIAL INFORMATION AND SHALL USE IT ONLY IN ACCORDANCE
 * WITH THE TERMS OF THE LICENSE AGREEMENT YOU ENTERED INTO WITH
 * Alexander Orlov.
 *
 * Author: Alexander Orlov
 *
 */

using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.DTOs;
using CarRental.Api.Extensions;

namespace CarRental.Api.Services;

public interface IEmailService
{
    Task<bool> SendBookingLinkAsync(BookingToken bookingToken, string bookingUrl);
    Task<bool> SendBookingConfirmationAsync(BookingConfirmation confirmation, string confirmationUrl);
    Task<bool> SendPaymentSuccessNotificationAsync(string customerEmail, BookingDataDto bookingData);
    Task<bool> SendEmailAsync(string to, string subject, string body);
    Task<EmailNotification> CreateEmailNotificationAsync(string customerEmail, string notificationType, string subject, string body, Guid? bookingTokenId = null);
}

public class EmailService : IEmailService
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<EmailService> _logger;
    private readonly IConfiguration _configuration;
    private readonly MultiTenantEmailService _multiTenantEmailService;

    public EmailService(
        CarRentalDbContext context, 
        ILogger<EmailService> logger, 
        IConfiguration configuration, 
        MultiTenantEmailService multiTenantEmailService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _multiTenantEmailService = multiTenantEmailService;
    }

    public Task<bool> SendBookingLinkAsync(BookingToken bookingToken, string bookingUrl)
    {
        try
        {
            // For now, this is a placeholder - the booking link functionality would need to be implemented
            // in MultiTenantEmailService if needed, or we can use a generic email template
            _logger.LogWarning("SendBookingLinkAsync is not fully implemented with MultiTenantEmailService");
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending booking link to {Email}", bookingToken.CustomerEmail);
            return Task.FromResult(false);
        }
    }

    public async Task<bool> SendBookingConfirmationAsync(BookingConfirmation confirmation, string confirmationUrl)
    {
        try
        {
            // Extract booking details from confirmation
            var pickupDate = confirmation.BookingToken.BookingData?.PickupDate ?? DateTime.UtcNow;
            var returnDate = confirmation.BookingToken.BookingData?.ReturnDate ?? DateTime.UtcNow;
            var vehicleName = $"{confirmation.BookingToken.BookingData?.VehicleInfo?.Make} {confirmation.BookingToken.BookingData?.VehicleInfo?.Model}";
            var customerName = confirmation.CustomerEmail; // Could be enhanced to get actual name
            
            // Determine language from company
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == confirmation.BookingToken.CompanyId);
            var languageCode = company?.Language?.ToLower() ?? "en";
            var language = LanguageCodes.FromCode(languageCode);
            
            var emailSent = await _multiTenantEmailService.SendBookingConfirmationAsync(
                confirmation.BookingToken.CompanyId,
                confirmation.CustomerEmail,
                customerName,
                confirmation.ConfirmationNumber ?? confirmation.BookingToken.Id.ToString(),
                pickupDate,
                returnDate,
                vehicleName,
                language
            );
            
            // Create email notification record
            if (emailSent)
            {
                await CreateEmailNotificationAsync(
                    confirmation.CustomerEmail,
                    "booking_confirmation",
                    $"Booking Confirmed - {confirmation.ConfirmationNumber}",
                    "",
                    confirmation.BookingTokenId
                );
            }

            return emailSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending booking confirmation to {Email}", confirmation.CustomerEmail);
            return false;
        }
    }

    public async Task<bool> SendPaymentSuccessNotificationAsync(string customerEmail, BookingDataDto bookingData)
    {
        try
        {
            // Extract payment details
            var amount = bookingData.TotalAmount;
            var paymentMethod = "Credit Card"; // Default, could be enhanced to get from payment record
            var bookingId = "Unknown"; // Could be enhanced to get from booking record
            var customerName = customerEmail; // Could be enhanced
            
            // Get company ID from booking data company info
            Guid? companyId = null;
            if (bookingData.CompanyInfo != null && !string.IsNullOrEmpty(bookingData.CompanyInfo.Email))
            {
                var companyByEmail = await _context.Companies
                    .FirstOrDefaultAsync(c => c.Email == bookingData.CompanyInfo.Email);
                companyId = companyByEmail?.Id;
            }
            
            if (!companyId.HasValue)
            {
                _logger.LogWarning("Cannot send payment success notification - no company ID available");
                return false;
            }
            
            // Determine language from company
            var company = await _context.Companies.FirstOrDefaultAsync(c => c.Id == companyId.Value);
            var languageCode = company?.Language?.ToLower() ?? "en";
            var language = LanguageCodes.FromCode(languageCode);
            var currencySymbol = company?.Currency == "USD" ? "$" : company?.Currency == "BRL" ? "R$" : "$";
            
            var emailSent = await _multiTenantEmailService.SendPaymentConfirmationAsync(
                companyId.Value,
                customerEmail,
                customerName,
                bookingId,
                amount,
                paymentMethod,
                currencySymbol,
                language
            );
            
            // Create email notification record
            if (emailSent)
            {
                await CreateEmailNotificationAsync(
                    customerEmail,
                    "payment_success",
                    "Payment Successful - Your Car Rental is Confirmed",
                    ""
                );
            }

            return emailSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payment success notification to {Email}", customerEmail);
            return false;
        }
    }

    public async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            // In a real implementation, you would integrate with an email service like:
            // - SendGrid
            // - Mailgun
            // - Amazon SES
            // - SMTP server
            
            // For now, we'll just log the email and mark it as sent
            _logger.LogInformation("Sending email to {To} with subject: {Subject}", to, subject);
            
            // Simulate email sending delay
            await Task.Delay(100);
            
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {To}", to);
            return false;
        }
    }

    public async Task<EmailNotification> CreateEmailNotificationAsync(string customerEmail, string notificationType, string subject, string body, Guid? bookingTokenId = null)
    {
        try
        {
            var notification = new EmailNotification
            {
                BookingTokenId = bookingTokenId,
                CustomerEmail = customerEmail,
                NotificationType = notificationType,
                Subject = subject,
                Body = body,
                Status = "sent"
            };

            _context.EmailNotifications.Add(notification);
            await _context.SaveChangesAsync();

            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating email notification for {Email}", customerEmail);
            throw;
        }
    }

}
