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

using CarRental.Api.Data;
using CarRental.Api.Models;
using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

public interface IEmailTemplateService
{
    string GenerateBookingLinkEmail(BookingToken bookingToken, string bookingUrl, Models.CompanyEmailStyle? companyStyle = null);
    string GenerateBookingConfirmationEmail(BookingConfirmation confirmation, string confirmationUrl, Models.CompanyEmailStyle? companyStyle = null);
    string GeneratePaymentSuccessEmail(BookingDataDto bookingData, Models.CompanyEmailStyle? companyStyle = null);
    string GenerateWelcomeEmail(string customerEmail, string companyName, Models.CompanyEmailStyle? companyStyle = null);
    string GenerateReminderEmail(BookingToken bookingToken, string bookingUrl, Models.CompanyEmailStyle? companyStyle = null);
    Models.CompanyEmailStyle GetDefaultCompanyStyle();
    Models.CompanyEmailStyle GetCompanyStyle(Guid companyId);
}

public class EmailTemplateService : IEmailTemplateService
{
    private readonly CarRentalDbContext _context;
    private readonly ILogger<EmailTemplateService> _logger;

    public EmailTemplateService(CarRentalDbContext context, ILogger<EmailTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public string GenerateBookingLinkEmail(BookingToken bookingToken, string bookingUrl, Models.CompanyEmailStyle? companyStyle = null)
    {
        var style = companyStyle ?? GetDefaultCompanyStyle();
        var bookingData = bookingToken.BookingData;
        var vehicleInfo = bookingData.VehicleInfo;
        var companyInfo = bookingData.CompanyInfo;

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Complete Your Car Rental Booking</title>
    <style>
        {GetBaseStyles(style)}
        {GetBookingLinkStyles(style)}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header' style='background-color: {style.PrimaryColor};'>
            <div class='header-content'>
                <img src='{style.LogoUrl}' alt='{companyInfo?.Name}' class='logo' style='max-height: 60px;'>
                <h1 style='color: {style.HeaderTextColor}; margin: 0; font-size: 28px;'>{companyInfo?.Name}</h1>
            </div>
        </div>
        
        <div class='content'>
            <div class='greeting'>
                <h2 style='color: {style.PrimaryColor}; margin-bottom: 20px;'>Complete Your Car Rental Booking</h2>
                <p style='font-size: 16px; line-height: 1.6; color: {style.TextColor};'>
                    Hello,<br><br>
                    You have a pending car rental booking with <strong>{companyInfo?.Name}</strong>. 
                    Please complete your booking by clicking the secure link below:
                </p>
            </div>
            
            <div class='booking-summary' style='background-color: {style.BackgroundColor}; border-left: 4px solid {style.PrimaryColor};'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>Booking Summary</h3>
                <div class='booking-details'>
                    <div class='detail-row'>
                        <span class='label'>Vehicle:</span>
                        <span class='value'><strong>{vehicleInfo?.Make} {vehicleInfo?.Model} ({vehicleInfo?.Year})</strong></span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>License Plate:</span>
                        <span class='value'>{vehicleInfo?.LicensePlate}</span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Pickup Date:</span>
                        <span class='value'>{bookingData.PickupDate:MMMM dd, yyyy 'at' h:mm tt}</span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Return Date:</span>
                        <span class='value'>{bookingData.ReturnDate:MMMM dd, yyyy 'at' h:mm tt}</span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Total Days:</span>
                        <span class='value'>{bookingData.TotalDays} days</span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Daily Rate:</span>
                        <span class='value'>${bookingData.DailyRate:F2}</span>
                    </div>
                    <div class='detail-row total-row'>
                        <span class='label'><strong>Total Amount:</strong></span>
                        <span class='value'><strong style='color: {style.PrimaryColor}; font-size: 18px;'>${bookingData.TotalAmount:F2}</strong></span>
                    </div>
                </div>
            </div>
            
            <div class='cta-section' style='text-align: center; margin: 30px 0;'>
                <a href='{bookingUrl}' class='cta-button' style='background-color: {style.PrimaryColor}; color: {style.ButtonTextColor};'>
                    Complete Booking & Pay Now
                </a>
                <p class='expiration-notice' style='color: {style.WarningColor}; font-size: 14px; margin-top: 15px;'>
                    ⏰ This booking link expires on {bookingToken.ExpiresAt:MMMM dd, yyyy 'at' h:mm tt}
                </p>
            </div>
            
            <div class='vehicle-features' style='background-color: {style.BackgroundColor};'>
                <h4 style='color: {style.PrimaryColor}; margin-top: 0;'>Vehicle Features</h4>
                <ul style='list-style: none; padding: 0;'>
                    {string.Join("", (vehicleInfo?.Features ?? new string[0]).Select(f => $"<li style='padding: 5px 0; color: {style.TextColor};'>✓ {f}</li>"))}
                </ul>
            </div>
            
            <div class='contact-info'>
                <h4 style='color: {style.PrimaryColor};'>Pickup Location</h4>
                <p style='color: {style.TextColor};'>
                    {(bookingData.PickupLocationInfo != null ? 
                        $@"<strong>{bookingData.PickupLocationInfo.LocationName}</strong><br>
                        {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.Address) ? $"📍 {bookingData.PickupLocationInfo.Address}, {bookingData.PickupLocationInfo.City}, {bookingData.PickupLocationInfo.State} {bookingData.PickupLocationInfo.PostalCode}<br>" : "")}
                        {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.Phone) ? $"📞 {bookingData.PickupLocationInfo.Phone}<br>" : "")}
                        {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.Email) ? $"📧 {bookingData.PickupLocationInfo.Email}<br>" : "")}
                        {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.OpeningHours) ? $"🕒 {bookingData.PickupLocationInfo.OpeningHours}" : "")}" 
                        : $"📍 {bookingData.PickupLocation}")}
                </p>
                
                <h4 style='color: {style.PrimaryColor}; margin-top: 20px;'>Company Contact</h4>
                <p style='color: {style.TextColor};'>
                    {companyInfo?.Name}<br>
                    📧 {companyInfo?.Email}
                </p>
            </div>
        </div>
        
        <div class='footer' style='background-color: {style.FooterColor}; color: {style.FooterTextColor};'>
            <p style='margin: 0; text-align: center; font-size: 12px;'>
                This is an automated message from {companyInfo?.Name}. Please do not reply to this email.
            </p>
            <p style='margin: 10px 0 0 0; text-align: center; font-size: 12px;'>
                {style.FooterText}
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public string GenerateBookingConfirmationEmail(BookingConfirmation confirmation, string confirmationUrl, Models.CompanyEmailStyle? companyStyle = null)
    {
        var style = companyStyle ?? GetDefaultCompanyStyle();
        var bookingData = confirmation.BookingDetails;
        var vehicleInfo = bookingData.VehicleInfo;
        var companyInfo = bookingData.CompanyInfo;

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Booking Confirmed - {confirmation.ConfirmationNumber}</title>
    <style>
        {GetBaseStyles(style)}
        {GetConfirmationStyles(style)}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header' style='background-color: {style.SuccessColor};'>
            <div class='header-content'>
                <img src='{style.LogoUrl}' alt='{companyInfo?.Name}' class='logo' style='max-height: 60px;'>
                <h1 style='color: white; margin: 0; font-size: 28px;'>✅ Booking Confirmed!</h1>
            </div>
        </div>
        
        <div class='content'>
            <div class='confirmation-banner' style='background-color: {style.SuccessColor}; color: white; text-align: center; padding: 20px; margin: 20px 0; border-radius: 8px;'>
                <h2 style='margin: 0; font-size: 24px;'>Your booking is confirmed!</h2>
                <p style='margin: 10px 0 0 0; font-size: 18px; font-weight: bold;'>Confirmation #: {confirmation.ConfirmationNumber}</p>
            </div>
            
            <div class='booking-details' style='background-color: {style.BackgroundColor}; border: 1px solid {style.BorderColor};'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>Booking Details</h3>
                <div class='details-grid'>
                    <div class='detail-item'>
                        <span class='label'>Vehicle:</span>
                        <span class='value'><strong>{vehicleInfo?.Make} {vehicleInfo?.Model} ({vehicleInfo?.Year})</strong></span>
                    </div>
                    <div class='detail-item'>
                        <span class='label'>License Plate:</span>
                        <span class='value'>{vehicleInfo?.LicensePlate}</span>
                    </div>
                    <div class='detail-item'>
                        <span class='label'>Pickup Date:</span>
                        <span class='value'>{bookingData.PickupDate:MMMM dd, yyyy 'at' h:mm tt}</span>
                    </div>
                    <div class='detail-item'>
                        <span class='label'>Return Date:</span>
                        <span class='value'>{bookingData.ReturnDate:MMMM dd, yyyy 'at' h:mm tt}</span>
                    </div>
                    <div class='detail-item'>
                        <span class='label'>Pickup Location:</span>
                        <span class='value'>{bookingData.PickupLocation}</span>
                    </div>
                    <div class='detail-item'>
                        <span class='label'>Return Location:</span>
                        <span class='value'>{bookingData.ReturnLocation}</span>
                    </div>
                    <div class='detail-item total-item'>
                        <span class='label'><strong>Total Amount Paid:</strong></span>
                        <span class='value'><strong style='color: {style.SuccessColor}; font-size: 18px;'>${bookingData.TotalAmount:F2}</strong></span>
                    </div>
                </div>
            </div>
            
            <div class='next-steps' style='background-color: {style.InfoColor}; border-left: 4px solid {style.PrimaryColor};'>
                <h4 style='color: {style.PrimaryColor}; margin-top: 0;'>Next Steps</h4>
                <ol style='color: {style.TextColor}; line-height: 1.8;'>
                    <li>Please arrive at the pickup location <strong>15 minutes before</strong> your scheduled pickup time</li>
                    <li>Bring a <strong>valid driver's license</strong> and the <strong>credit card used for payment</strong></li>
                    <li>Contact {companyInfo?.Name} if you need to modify your booking</li>
                    <li>Keep this confirmation email for your records</li>
                </ol>
            </div>
            
            <div class='contact-info'>
                <h4 style='color: {style.PrimaryColor};'>Pickup Location Details</h4>
                <div style='background-color: {style.BackgroundColor}; padding: 15px; border-radius: 5px; margin-bottom: 15px;'>
                    <p style='margin: 5px 0; color: {style.TextColor};'>
                        {(bookingData.PickupLocationInfo != null ? 
                            $@"<strong>{bookingData.PickupLocationInfo.LocationName}</strong><br>
                            {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.Address) ? $"📍 {bookingData.PickupLocationInfo.Address}, {bookingData.PickupLocationInfo.City}, {bookingData.PickupLocationInfo.State} {bookingData.PickupLocationInfo.PostalCode}<br>" : "")}
                            {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.Phone) ? $"📞 {bookingData.PickupLocationInfo.Phone}<br>" : "")}
                            {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.Email) ? $"📧 {bookingData.PickupLocationInfo.Email}<br>" : "")}
                            {(!string.IsNullOrEmpty(bookingData.PickupLocationInfo.OpeningHours) ? $"🕒 {bookingData.PickupLocationInfo.OpeningHours}" : "")}" 
                            : $"📍 {bookingData.PickupLocation}")}
                    </p>
                </div>
                
                <h4 style='color: {style.PrimaryColor};'>Return Location Details</h4>
                <div style='background-color: {style.BackgroundColor}; padding: 15px; border-radius: 5px; margin-bottom: 15px;'>
                    <p style='margin: 5px 0; color: {style.TextColor};'>
                        {(bookingData.ReturnLocationInfo != null ? 
                            $@"<strong>{bookingData.ReturnLocationInfo.LocationName}</strong><br>
                            {(!string.IsNullOrEmpty(bookingData.ReturnLocationInfo.Address) ? $"📍 {bookingData.ReturnLocationInfo.Address}, {bookingData.ReturnLocationInfo.City}, {bookingData.ReturnLocationInfo.State} {bookingData.ReturnLocationInfo.PostalCode}<br>" : "")}
                            {(!string.IsNullOrEmpty(bookingData.ReturnLocationInfo.Phone) ? $"📞 {bookingData.ReturnLocationInfo.Phone}<br>" : "")}
                            {(!string.IsNullOrEmpty(bookingData.ReturnLocationInfo.Email) ? $"📧 {bookingData.ReturnLocationInfo.Email}<br>" : "")}
                            {(!string.IsNullOrEmpty(bookingData.ReturnLocationInfo.OpeningHours) ? $"🕒 {bookingData.ReturnLocationInfo.OpeningHours}" : "")}" 
                            : $"📍 {bookingData.ReturnLocation}")}
                    </p>
                </div>
                
                <h4 style='color: {style.PrimaryColor};'>Company Contact</h4>
                <div style='background-color: {style.BackgroundColor}; padding: 15px; border-radius: 5px;'>
                    <p style='margin: 5px 0; color: {style.TextColor};'>
                        <strong>{companyInfo?.Name}</strong><br>
                        📧 {companyInfo?.Email}
                    </p>
                </div>
            </div>
        </div>
        
        <div class='footer' style='background-color: {style.FooterColor}; color: {style.FooterTextColor};'>
            <p style='margin: 0; text-align: center; font-size: 12px;'>
                Thank you for choosing {companyInfo?.Name} for your car rental needs!
            </p>
            <p style='margin: 10px 0 0 0; text-align: center; font-size: 12px;'>
                {style.FooterText}
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public string GeneratePaymentSuccessEmail(BookingDataDto bookingData, Models.CompanyEmailStyle? companyStyle = null)
    {
        var style = companyStyle ?? GetDefaultCompanyStyle();
        var vehicleInfo = bookingData.VehicleInfo;
        var companyInfo = bookingData.CompanyInfo;

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Payment Successful</title>
    <style>
        {GetBaseStyles(style)}
        {GetPaymentSuccessStyles(style)}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header' style='background-color: {style.SuccessColor};'>
            <div class='header-content'>
                <img src='{style.LogoUrl}' alt='{companyInfo?.Name}' class='logo' style='max-height: 60px;'>
                <h1 style='color: white; margin: 0; font-size: 28px;'>💳 Payment Successful!</h1>
            </div>
        </div>
        
        <div class='content'>
            <div class='success-banner' style='background-color: {style.SuccessColor}; color: white; text-align: center; padding: 20px; margin: 20px 0; border-radius: 8px;'>
                <h2 style='margin: 0; font-size: 24px;'>Your payment has been processed successfully!</h2>
                <p style='margin: 10px 0 0 0; font-size: 16px;'>Your car rental booking is now confirmed.</p>
            </div>
            
            <div class='payment-summary' style='background-color: {style.BackgroundColor}; border: 1px solid {style.BorderColor};'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>Payment Summary</h3>
                <div class='payment-details'>
                    <div class='detail-row'>
                        <span class='label'>Vehicle:</span>
                        <span class='value'><strong>{vehicleInfo?.Make} {vehicleInfo?.Model} ({vehicleInfo?.Year})</strong></span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Amount Paid:</span>
                        <span class='value'><strong style='color: {style.SuccessColor}; font-size: 18px;'>${bookingData.TotalAmount:F2}</strong></span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Payment Status:</span>
                        <span class='value'><strong style='color: {style.SuccessColor};'>✅ Completed</strong></span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Company:</span>
                        <span class='value'><strong>{companyInfo?.Name}</strong></span>
                    </div>
                </div>
            </div>
            
            <div class='next-steps' style='background-color: {style.InfoColor}; border-left: 4px solid {style.PrimaryColor};'>
                <h4 style='color: {style.PrimaryColor}; margin-top: 0;'>What's Next?</h4>
                <p style='color: {style.TextColor}; line-height: 1.6;'>
                    You will receive a separate confirmation email with your complete booking details shortly. 
                    Please keep this payment confirmation for your records.
                </p>
            </div>
            
            <div class='contact-info'>
                <h4 style='color: {style.PrimaryColor};'>Questions?</h4>
                <p style='color: {style.TextColor};'>
                    If you have any questions about your payment or booking, please contact:<br>
                    <strong>{companyInfo?.Name}</strong><br>
                    📧 {companyInfo?.Email}
                </p>
            </div>
        </div>
        
        <div class='footer' style='background-color: {style.FooterColor}; color: {style.FooterTextColor};'>
            <p style='margin: 0; text-align: center; font-size: 12px;'>
                This is a payment confirmation from {companyInfo?.Name}. Please keep this email for your records.
            </p>
            <p style='margin: 10px 0 0 0; text-align: center; font-size: 12px;'>
                {style.FooterText}
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public string GenerateWelcomeEmail(string customerEmail, string companyName, Models.CompanyEmailStyle? companyStyle = null)
    {
        var style = companyStyle ?? GetDefaultCompanyStyle();

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Welcome to {companyName}</title>
    <style>
        {GetBaseStyles(style)}
        {GetWelcomeStyles(style)}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header' style='background-color: {style.PrimaryColor};'>
            <div class='header-content'>
                <img src='{style.LogoUrl}' alt='{companyName}' class='logo' style='max-height: 60px;'>
                <h1 style='color: {style.HeaderTextColor}; margin: 0; font-size: 28px;'>Welcome to {companyName}!</h1>
            </div>
        </div>
        
        <div class='content'>
            <div class='welcome-message'>
                <h2 style='color: {style.PrimaryColor}; margin-bottom: 20px;'>Thank you for choosing {companyName}!</h2>
                <p style='font-size: 16px; line-height: 1.6; color: {style.TextColor};'>
                    We're excited to have you as a customer. You can now enjoy our premium car rental services 
                    with easy booking and excellent customer support.
                </p>
            </div>
            
            <div class='features' style='background-color: {style.BackgroundColor};'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>What you can expect:</h3>
                <ul style='color: {style.TextColor}; line-height: 1.8;'>
                    <li>🚗 Wide selection of quality vehicles</li>
                    <li>💳 Secure online payment processing</li>
                    <li>📧 Instant booking confirmations</li>
                    <li>🛡️ Comprehensive insurance options</li>
                    <li>📞 24/7 customer support</li>
                </ul>
            </div>
            
            <div class='cta-section' style='text-align: center; margin: 30px 0;'>
                <a href='#' class='cta-button' style='background-color: {style.PrimaryColor}; color: {style.ButtonTextColor};'>
                    Start Your First Booking
                </a>
            </div>
        </div>
        
        <div class='footer' style='background-color: {style.FooterColor}; color: {style.FooterTextColor};'>
            <p style='margin: 0; text-align: center; font-size: 12px;'>
                Welcome to the {companyName} family!
            </p>
            <p style='margin: 10px 0 0 0; text-align: center; font-size: 12px;'>
                {style.FooterText}
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public string GenerateReminderEmail(BookingToken bookingToken, string bookingUrl, Models.CompanyEmailStyle? companyStyle = null)
    {
        var style = companyStyle ?? GetDefaultCompanyStyle();
        var bookingData = bookingToken.BookingData;
        var vehicleInfo = bookingData.VehicleInfo;
        var companyInfo = bookingData.CompanyInfo;

        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Booking Reminder - {companyInfo?.Name}</title>
    <style>
        {GetBaseStyles(style)}
        {GetReminderStyles(style)}
    </style>
</head>
<body>
    <div class='email-container'>
        <div class='header' style='background-color: {style.WarningColor};'>
            <div class='header-content'>
                <img src='{style.LogoUrl}' alt='{companyInfo?.Name}' class='logo' style='max-height: 60px;'>
                <h1 style='color: white; margin: 0; font-size: 28px;'>⏰ Booking Reminder</h1>
            </div>
        </div>
        
        <div class='content'>
            <div class='reminder-message'>
                <h2 style='color: {style.WarningColor}; margin-bottom: 20px;'>Don't miss out on your booking!</h2>
                <p style='font-size: 16px; line-height: 1.6; color: {style.TextColor};'>
                    You have a pending car rental booking with <strong>{companyInfo?.Name}</strong> that expires soon. 
                    Complete your booking now to secure your vehicle.
                </p>
            </div>
            
            <div class='booking-summary' style='background-color: {style.BackgroundColor}; border-left: 4px solid {style.WarningColor};'>
                <h3 style='color: {style.PrimaryColor}; margin-top: 0;'>Your Booking</h3>
                <div class='booking-details'>
                    <div class='detail-row'>
                        <span class='label'>Vehicle:</span>
                        <span class='value'><strong>{vehicleInfo?.Make} {vehicleInfo?.Model} ({vehicleInfo?.Year})</strong></span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Total Amount:</span>
                        <span class='value'><strong style='color: {style.PrimaryColor}; font-size: 18px;'>${bookingData.TotalAmount:F2}</strong></span>
                    </div>
                    <div class='detail-row'>
                        <span class='label'>Expires:</span>
                        <span class='value' style='color: {style.WarningColor}; font-weight: bold;'>{bookingToken.ExpiresAt:MMMM dd, yyyy 'at' h:mm tt}</span>
                    </div>
                </div>
            </div>
            
            <div class='cta-section' style='text-align: center; margin: 30px 0;'>
                <a href='{bookingUrl}' class='cta-button' style='background-color: {style.WarningColor}; color: white;'>
                    Complete Booking Now
                </a>
                <p class='urgency-notice' style='color: {style.WarningColor}; font-size: 14px; margin-top: 15px; font-weight: bold;'>
                    ⚠️ This booking expires in {GetTimeUntilExpiration(bookingToken.ExpiresAt)}
                </p>
            </div>
        </div>
        
        <div class='footer' style='background-color: {style.FooterColor}; color: {style.FooterTextColor};'>
            <p style='margin: 0; text-align: center; font-size: 12px;'>
                This is a reminder from {companyInfo?.Name}. Please complete your booking to avoid missing out.
            </p>
        </div>
    </div>
</body>
</html>";
    }

    public Models.CompanyEmailStyle GetDefaultCompanyStyle()
    {
        return new CompanyEmailStyle
        {
            PrimaryColor = "#007bff",
            SecondaryColor = "#6c757d",
            SuccessColor = "#28a745",
            WarningColor = "#ffc107",
            InfoColor = "#17a2b8",
            BackgroundColor = "#f8f9fa",
            BorderColor = "#dee2e6",
            TextColor = "#333333",
            HeaderTextColor = "#ffffff",
            ButtonTextColor = "#ffffff",
            FooterColor = "#343a40",
            FooterTextColor = "#ffffff",
            LogoUrl = "https://via.placeholder.com/200x60/007bff/ffffff?text=CarRental",
            FooterText = "© 2025 Car Rental System. All rights reserved.",
            FontFamily = "Arial, sans-serif"
        };
    }

    public Models.CompanyEmailStyle GetCompanyStyle(Guid companyId)
    {
        // In a real implementation, you would fetch company-specific styling from database
        // For now, return default style
        return GetDefaultCompanyStyle();
    }

    private string GetBaseStyles(CompanyEmailStyle style)
    {
        return $@"
        body {{
            font-family: {style.FontFamily};
            line-height: 1.6;
            color: {style.TextColor};
            margin: 0;
            padding: 0;
            background-color: #f4f4f4;
        }}
        .email-container {{
            max-width: 600px;
            margin: 0 auto;
            background-color: white;
            border-radius: 8px;
            overflow: hidden;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }}
        .header {{
            padding: 20px;
            text-align: center;
        }}
        .header-content {{
            display: flex;
            align-items: center;
            justify-content: center;
            gap: 15px;
        }}
        .content {{
            padding: 30px;
        }}
        .footer {{
            padding: 20px;
            text-align: center;
        }}
        .cta-button {{
            display: inline-block;
            padding: 15px 30px;
            text-decoration: none;
            border-radius: 5px;
            font-weight: bold;
            font-size: 16px;
            transition: all 0.3s ease;
        }}
        .cta-button:hover {{
            opacity: 0.9;
            transform: translateY(-2px);
        }}
        .detail-row {{
            display: flex;
            justify-content: space-between;
            padding: 8px 0;
            border-bottom: 1px solid {style.BorderColor};
        }}
        .detail-row:last-child {{
            border-bottom: none;
        }}
        .label {{
            font-weight: bold;
            color: {style.TextColor};
        }}
        .value {{
            color: {style.TextColor};
        }}
        @media (max-width: 600px) {{
            .email-container {{
                margin: 0;
                border-radius: 0;
            }}
            .content {{
                padding: 20px;
            }}
            .header-content {{
                flex-direction: column;
                gap: 10px;
            }}
        }}";
    }

    private string GetBookingLinkStyles(CompanyEmailStyle style)
    {
        return $@"
        .booking-summary {{
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .total-row {{
            background-color: {style.PrimaryColor};
            color: white;
            padding: 15px;
            margin: 10px -20px -20px -20px;
            border-radius: 0 0 5px 5px;
        }}
        .total-row .label,
        .total-row .value {{
            color: white;
        }}
        .vehicle-features {{
            padding: 15px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .contact-info {{
            background-color: {style.BackgroundColor};
            padding: 15px;
            border-radius: 5px;
            margin-top: 20px;
        }}";
    }

    private string GetConfirmationStyles(CompanyEmailStyle style)
    {
        return $@"
        .confirmation-banner {{
            text-align: center;
            padding: 20px;
            margin: 20px 0;
            border-radius: 8px;
        }}
        .booking-details {{
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .details-grid {{
            display: grid;
            grid-template-columns: 1fr 1fr;
            gap: 15px;
        }}
        .detail-item {{
            padding: 10px 0;
        }}
        .total-item {{
            grid-column: 1 / -1;
            background-color: {style.SuccessColor};
            color: white;
            padding: 15px;
            border-radius: 5px;
            margin-top: 10px;
        }}
        .next-steps {{
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        @media (max-width: 600px) {{
            .details-grid {{
                grid-template-columns: 1fr;
            }}
        }}";
    }

    private string GetPaymentSuccessStyles(CompanyEmailStyle style)
    {
        return $@"
        .success-banner {{
            text-align: center;
            padding: 20px;
            margin: 20px 0;
            border-radius: 8px;
        }}
        .payment-summary {{
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .payment-details {{
            background-color: {style.BackgroundColor};
            padding: 15px;
            border-radius: 5px;
        }}";
    }

    private string GetWelcomeStyles(CompanyEmailStyle style)
    {
        return $@"
        .welcome-message {{
            text-align: center;
            padding: 20px 0;
        }}
        .features {{
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .features ul {{
            list-style: none;
            padding: 0;
        }}
        .features li {{
            padding: 8px 0;
            font-size: 16px;
        }}";
    }

    private string GetReminderStyles(CompanyEmailStyle style)
    {
        return $@"
        .reminder-message {{
            text-align: center;
            padding: 20px 0;
        }}
        .booking-summary {{
            padding: 20px;
            margin: 20px 0;
            border-radius: 5px;
        }}
        .urgency-notice {{
            background-color: {style.WarningColor};
            color: white;
            padding: 10px;
            border-radius: 5px;
            display: inline-block;
        }}";
    }

    private string GetTimeUntilExpiration(DateTime expirationTime)
    {
        var timeSpan = expirationTime - DateTime.UtcNow;
        if (timeSpan.TotalHours < 1)
            return $"{(int)timeSpan.TotalMinutes} minutes";
        else if (timeSpan.TotalDays < 1)
            return $"{(int)timeSpan.TotalHours} hours";
        else
            return $"{(int)timeSpan.TotalDays} days";
    }
}

