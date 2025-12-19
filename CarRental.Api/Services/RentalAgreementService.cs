/*
 * Rental Agreement Service
 * Handles creation and storage for rental agreements
 * Copyright (c) 2025 Alexander Orlov.
 */

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;

namespace CarRental.Api.Services;

public interface IRentalAgreementService
{
    Task<RentalAgreementEntity> CreateAgreementAsync(CreateAgreementRequest request, CancellationToken ct = default);
    Task<RentalAgreementEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RentalAgreementEntity?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);
    Task<string> GenerateAndStorePdfAsync(Guid agreementId, CancellationToken ct = default);
    Task VoidAgreementAsync(Guid agreementId, string reason, string performedBy, CancellationToken ct = default);
    Task LogActionAsync(Guid agreementId, string action, string performedBy, string? ipAddress = null, string? userAgent = null, object? details = null, CancellationToken ct = default);
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
    private readonly IWebHostEnvironment _environment;

    public RentalAgreementService(
        CarRentalDbContext db,
        ILogger<RentalAgreementService> logger,
        IWebHostEnvironment environment)
    {
        _db = db;
        _logger = logger;
        _environment = environment;
    }

    public async Task<RentalAgreementEntity> CreateAgreementAsync(CreateAgreementRequest request, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[RentalAgreement] Creating agreement - BookingId: {BookingId}, CustomerId: {CustomerId}, HasSignature: {HasSignature}, SignatureLength: {SignatureLength}",
            request.BookingId,
            request.CustomerId,
            !string.IsNullOrEmpty(request.AgreementData?.SignatureImage),
            request.AgreementData?.SignatureImage?.Length ?? 0
        );
        
        if (request.AgreementData == null)
        {
            throw new ArgumentNullException(nameof(request.AgreementData), "AgreementData cannot be null");
        }
        
        if (string.IsNullOrEmpty(request.AgreementData.SignatureImage))
        {
            throw new ArgumentException("SignatureImage cannot be null or empty", nameof(request.AgreementData.SignatureImage));
        }
        
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

        try
        {
            _db.RentalAgreements.Add(agreement);
            await _db.SaveChangesAsync(ct);
            
            _logger.LogInformation(
                "[RentalAgreement] Successfully saved agreement {AgreementId} (AgreementNumber: {AgreementNumber}) to database for booking {BookingId}",
                agreement.Id,
                agreement.AgreementNumber,
                request.BookingId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "[RentalAgreement] Failed to save agreement to database - BookingId: {BookingId}, AgreementNumber: {AgreementNumber}, Error: {ErrorMessage}",
                request.BookingId,
                agreement.AgreementNumber,
                ex.Message);
            throw; // Re-throw to let the caller handle it
        }
        
        // Log creation (don't fail if audit log fails)
        try
        {
            await LogActionAsync(agreement.Id, "created", request.CustomerEmail, request.IpAddress, request.AgreementData.UserAgent, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[RentalAgreement] Failed to create audit log for agreement {AgreementId}, but agreement was saved", agreement.Id);
        }
        
        return agreement;
    }

    public async Task<RentalAgreementEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.RentalAgreements
            .Include(a => a.AuditLogs)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
    }

    public async Task<RentalAgreementEntity?> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default)
    {
        return await _db.RentalAgreements
            .Where(a => a.BookingId == bookingId && a.Status == "active")
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string> GenerateAndStorePdfAsync(Guid agreementId, CancellationToken ct = default)
    {
        var agreement = await _db.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId, ct);
            
        if (agreement == null)
            throw new InvalidOperationException($"Agreement {agreementId} not found");
        
        var company = await _db.Companies
            .FirstOrDefaultAsync(c => c.Id == agreement.CompanyId, ct);
            
        if (company == null)
            throw new InvalidOperationException($"Company {agreement.CompanyId} not found");
        
        // Generate PDF using the PDF generator
        var pdfGenerator = new RentalAgreementPdfGenerator();
        var pdfBytes = pdfGenerator.Generate(new RentalAgreementPdfData
        {
            AgreementNumber = agreement.AgreementNumber,
            Language = agreement.Language,
            CompanyName = company.CompanyName,
            CompanyAddress = GetCompanyAddress(company),
            CompanyPhone = null, // Company model doesn't have Phone property
            CompanyEmail = company.Email,
            CustomerName = agreement.CustomerName,
            CustomerEmail = agreement.CustomerEmail,
            CustomerPhone = agreement.CustomerPhone,
            CustomerAddress = agreement.CustomerAddress,
            DriverLicenseNumber = agreement.DriverLicenseNumber,
            DriverLicenseState = agreement.DriverLicenseState,
            VehicleName = agreement.VehicleName,
            VehiclePlate = agreement.VehiclePlate,
            PickupDate = agreement.PickupDate,
            PickupLocation = agreement.PickupLocation,
            ReturnDate = agreement.ReturnDate,
            ReturnLocation = agreement.ReturnLocation,
            RentalAmount = agreement.RentalAmount,
            DepositAmount = agreement.DepositAmount,
            Currency = agreement.Currency,
            SignatureImage = agreement.SignatureImage,
            SignedAt = agreement.SignedAt,
            TermsText = agreement.TermsText,
            NonRefundableText = agreement.NonRefundableText,
            DamagePolicyText = agreement.DamagePolicyText,
            CardAuthorizationText = agreement.CardAuthorizationText,
        });
        
        // Save PDF to company agreements folder (existing location)
        var agreementsFolder = Path.Combine(_environment.ContentRootPath, "wwwroot", "agreements", agreement.CompanyId.ToString());
        Directory.CreateDirectory(agreementsFolder);
        
        var fileName = $"{agreement.AgreementNumber}.pdf";
        var companyFilePath = Path.Combine(agreementsFolder, fileName);
        
        await System.IO.File.WriteAllBytesAsync(companyFilePath, pdfBytes, ct);
        
        // Additionally store under customers/{customerId}/agreements/{YYYY-MM-DD}/
        // to satisfy per-customer archival and static serving from /customers path
        try
        {
            var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var customerAgreementsFolder = Path.Combine(
                _environment.ContentRootPath,
                "wwwroot",
                "customers",
                agreement.CustomerId.ToString(),
                "agreements",
                dateFolder
            );
            Directory.CreateDirectory(customerAgreementsFolder);
            var customerFilePath = Path.Combine(customerAgreementsFolder, fileName);
            await System.IO.File.WriteAllBytesAsync(customerFilePath, pdfBytes, ct);
            
            _logger.LogInformation(
                "Stored agreement PDF for customer at {CustomerFilePath}",
                customerFilePath
            );
        }
        catch (Exception copyEx)
        {
            // Don't fail overall generation if copy fails; log and continue
            _logger.LogWarning(copyEx, "Failed to copy agreement PDF to customer folder for Agreement {AgreementNumber}", agreement.AgreementNumber);
        }
        
        // Generate URL path (existing URL kept for compatibility)
        var pdfUrl = $"/agreements/{agreement.CompanyId}/{fileName}";
        
        // Update agreement with PDF URL
        agreement.PdfUrl = pdfUrl;
        agreement.PdfGeneratedAt = DateTime.UtcNow;
        agreement.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        
        await LogActionAsync(agreement.Id, "pdf_generated", "system", null, null, null, ct);
        
        _logger.LogInformation("Generated PDF for agreement {AgreementNumber}: {PdfUrl}", 
            agreement.AgreementNumber, pdfUrl);
        
        return pdfUrl;
    }

    public async Task VoidAgreementAsync(Guid agreementId, string reason, string performedBy, CancellationToken ct = default)
    {
        var agreement = await _db.RentalAgreements
            .FirstOrDefaultAsync(a => a.Id == agreementId, ct);
            
        if (agreement == null)
            throw new InvalidOperationException($"Agreement {agreementId} not found");
        
        agreement.Status = "voided";
        agreement.VoidedAt = DateTime.UtcNow;
        agreement.VoidedReason = reason;
        agreement.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync(ct);
        
        await LogActionAsync(agreementId, "voided", performedBy, null, null, new { reason }, ct);
        
        _logger.LogInformation("Voided agreement {AgreementId} by {PerformedBy}: {Reason}", 
            agreementId, performedBy, reason);
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

    public async Task LogActionAsync(
        Guid agreementId, 
        string action, 
        string performedBy, 
        string? ipAddress = null, 
        string? userAgent = null,
        object? details = null,
        CancellationToken ct = default)
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
            Details = details != null ? JsonSerializer.SerializeToDocument(details) : null,
            CreatedAt = DateTime.UtcNow
        };
        
        _db.RentalAgreementAuditLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    private static string? GetCompanyAddress(Company company)
    {
        // Company model doesn't have address fields, return null or use company name
        // If you need company address, you may need to get it from CompanyLocation or another source
        return null;
    }
}

