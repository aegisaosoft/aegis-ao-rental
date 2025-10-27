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
    private readonly IEmailTemplateService _templateService;

    public EmailService(CarRentalDbContext context, ILogger<EmailService> logger, IConfiguration configuration, IEmailTemplateService templateService)
    {
        _context = context;
        _logger = logger;
        _configuration = configuration;
        _templateService = templateService;
    }

    public async Task<bool> SendBookingLinkAsync(BookingToken bookingToken, string bookingUrl)
    {
        try
        {
            var subject = $"Complete Your Car Rental Booking - {bookingToken.BookingData.VehicleInfo?.Make} {bookingToken.BookingData.VehicleInfo?.Model}";
            
            // Get company email style
            var companyStyle = await GetCompanyEmailStyleAsync(bookingToken.CompanyId);
            var body = _templateService.GenerateBookingLinkEmail(bookingToken, bookingUrl, companyStyle);
            
            var emailSent = await SendEmailAsync(bookingToken.CustomerEmail, subject, body);
            
            // Create email notification record
            await CreateEmailNotificationAsync(
                bookingToken.CustomerEmail,
                "booking_link",
                subject,
                body,
                bookingToken.TokenId
            );

            return emailSent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending booking link to {Email}", bookingToken.CustomerEmail);
            return false;
        }
    }

    public async Task<bool> SendBookingConfirmationAsync(BookingConfirmation confirmation, string confirmationUrl)
    {
        try
        {
            var subject = $"Booking Confirmed - {confirmation.ConfirmationNumber}";
            
            // Get company email style
            var companyStyle = await GetCompanyEmailStyleAsync(confirmation.BookingToken.CompanyId);
            var body = _templateService.GenerateBookingConfirmationEmail(confirmation, confirmationUrl, companyStyle);
            
            var emailSent = await SendEmailAsync(confirmation.CustomerEmail, subject, body);
            
            // Create email notification record
            await CreateEmailNotificationAsync(
                confirmation.CustomerEmail,
                "booking_confirmation",
                subject,
                body,
                confirmation.BookingTokenId
            );

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
            var subject = "Payment Successful - Your Car Rental is Confirmed";
            
            // Get company email style (you might need to pass company ID)
            var companyStyle = _templateService.GetDefaultCompanyStyle();
            var body = _templateService.GeneratePaymentSuccessEmail(bookingData, companyStyle);
            
            var emailSent = await SendEmailAsync(customerEmail, subject, body);
            
            // Create email notification record
            await CreateEmailNotificationAsync(
                customerEmail,
                "payment_success",
                subject,
                body
            );

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

    private async Task<CompanyEmailStyle?> GetCompanyEmailStyleAsync(Guid companyId)
    {
        try
        {
            var style = await _context.CompanyEmailStyles
                .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive);
            
            return style;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting company email style for {CompanyId}, using default", companyId);
            return null;
        }
    }
}
