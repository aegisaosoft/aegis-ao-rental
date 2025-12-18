/*
 * Rental Agreement Service
 * Handles creation and storage for rental agreements
 * Copyright (c) 2025 Alexander Orlov.
 */

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;

namespace CarRental.Api.Services;

public interface IRentalAgreementService
{
    Task<RentalAgreementEntity> CreateAgreementAsync(CreateAgreementRequest request, CancellationToken ct = default);
    Task<RentalAgreementEntity?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);
}

public class CreateAgreementRequest
{
    public Guid CompanyId { get; set; }
    public Guid BookingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    
    // Customer info
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerAddress { get; set; }
    public string? DriverLicenseNumber { get; set; }
    public string? DriverLicenseState { get; set; }
    
    // Vehicle info
    public string VehicleName { get; set; } = string.Empty;
    public string? VehiclePlate { get; set; }
    
    // Rental details
    public DateTime PickupDate { get; set; }
    public string? PickupLocation { get; set; }
    public DateTime ReturnDate { get; set; }
    public string? ReturnLocation { get; set; }
    public decimal RentalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    public string Currency { get; set; } = "USD";
    
    // Agreement data from frontend
    public AgreementDataDto AgreementData { get; set; } = new();
    
    // Request context
    public string? IpAddress { get; set; }
}

public class RentalAgreementService : IRentalAgreementService
{
    private readonly CarRentalDbContext _db;
    private readonly ILogger<RentalAgreementService> _logger;

    public RentalAgreementService(
        CarRentalDbContext db,
        ILogger<RentalAgreementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<RentalAgreementEntity> CreateAgreementAsync(CreateAgreementRequest request, CancellationToken ct = default)
    {
        // Generate agreement number
        var agreementNumber = await GenerateAgreementNumberAsync(request.CompanyId, ct);
        
        // Compute signature hash
        var signatureHash = ComputeSha256Hash(request.AgreementData.SignatureImage);
        
        var agreement = new RentalAgreementEntity
        {
            Id = Guid.NewGuid(),
            CompanyId = request.CompanyId,
            BookingId = request.BookingId,
            CustomerId = request.CustomerId,
            VehicleId = request.VehicleId,
            AgreementNumber = agreementNumber,
            Language = request.AgreementData.Language ?? "en",
            
            // Customer snapshot
            CustomerName = request.CustomerName,
            CustomerEmail = request.CustomerEmail,
            CustomerPhone = request.CustomerPhone,
            CustomerAddress = request.CustomerAddress,
            DriverLicenseNumber = request.DriverLicenseNumber,
            DriverLicenseState = request.DriverLicenseState,
            
            // Vehicle snapshot
            VehicleName = request.VehicleName,
            VehiclePlate = request.VehiclePlate,
            
            // Rental details
            PickupDate = request.PickupDate,
            PickupLocation = request.PickupLocation,
            ReturnDate = request.ReturnDate,
            ReturnLocation = request.ReturnLocation,
            RentalAmount = request.RentalAmount,
            DepositAmount = request.DepositAmount,
            Currency = request.Currency,
            
            // Signature
            SignatureImage = request.AgreementData.SignatureImage,
            SignatureHash = signatureHash,
            
            // Consent timestamps
            TermsAcceptedAt = request.AgreementData.Consents.TermsAcceptedAt ?? DateTime.UtcNow,
            NonRefundableAcceptedAt = request.AgreementData.Consents.NonRefundableAcceptedAt ?? DateTime.UtcNow,
            DamagePolicyAcceptedAt = request.AgreementData.Consents.DamagePolicyAcceptedAt ?? DateTime.UtcNow,
            CardAuthorizationAcceptedAt = request.AgreementData.Consents.CardAuthorizationAcceptedAt ?? DateTime.UtcNow,
            
            // Consent texts (store exactly what was shown to customer)
            TermsText = $"{request.AgreementData.ConsentTexts.TermsTitle}\n{request.AgreementData.ConsentTexts.TermsText}",
            NonRefundableText = $"{request.AgreementData.ConsentTexts.NonRefundableTitle}\n{request.AgreementData.ConsentTexts.NonRefundableText}",
            DamagePolicyText = $"{request.AgreementData.ConsentTexts.DamagePolicyTitle}\n{request.AgreementData.ConsentTexts.DamagePolicyText}",
            CardAuthorizationText = $"{request.AgreementData.ConsentTexts.CardAuthorizationTitle}\n{request.AgreementData.ConsentTexts.CardAuthorizationText}",
            
            // Metadata
            SignedAt = request.AgreementData.SignedAt,
            IpAddress = request.IpAddress,
            UserAgent = request.AgreementData.UserAgent,
            Timezone = request.AgreementData.Timezone,
            DeviceInfo = request.AgreementData.DeviceInfo != null 
                ? JsonSerializer.SerializeToDocument(request.AgreementData.DeviceInfo) 
                : null,
            
            // Geolocation
            GeoLatitude = request.AgreementData.Geolocation?.Latitude,
            GeoLongitude = request.AgreementData.Geolocation?.Longitude,
            GeoAccuracy = request.AgreementData.Geolocation?.Accuracy,
            
            // Status
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.RentalAgreements.Add(agreement);
        await _db.SaveChangesAsync(ct);
        
        // Log creation
        await LogActionAsync(agreement.Id, "created", request.CustomerEmail, request.IpAddress, request.AgreementData.UserAgent, ct);
        
        _logger.LogInformation("Created rental agreement {AgreementNumber} for booking {BookingId}", 
            agreementNumber, request.BookingId);
        
        return agreement;
    }

    public async Task<RentalAgreementEntity?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        return await _db.RentalAgreements
            .Where(a => a.BookingId == bookingId && a.Status == "active")
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string> GenerateAgreementNumberAsync(Guid companyId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"AGR-{year}-";
        
        var lastNumber = await _db.RentalAgreements
            .Where(a => a.CompanyId == companyId && a.AgreementNumber.StartsWith(prefix))
            .OrderByDescending(a => a.AgreementNumber)
            .Select(a => a.AgreementNumber)
            .FirstOrDefaultAsync(ct);
        
        var sequence = 1;
        if (lastNumber != null)
        {
            var parts = lastNumber.Split('-');
            if (parts.Length == 3 && int.TryParse(parts[2], out var lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }
        
        return $"{prefix}{sequence:D6}";
    }

    private static string ComputeSha256Hash(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private async Task LogActionAsync(Guid agreementId, string action, string? performedBy, string? ipAddress, string? userAgent, CancellationToken ct)
    {
        var log = new RentalAgreementAuditLog
        {
            Id = Guid.NewGuid(),
            AgreementId = agreementId,
            Action = action,
            PerformedBy = performedBy,
            PerformedAt = DateTime.UtcNow,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.RentalAgreementAuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }
}

