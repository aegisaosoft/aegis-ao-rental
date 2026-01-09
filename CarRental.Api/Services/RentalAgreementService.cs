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
    
    /// <summary>
    /// Signs an existing booking that doesn't have an agreement yet.
    /// Creates the agreement and generates PDF.
    /// </summary>
    Task<RentalAgreementEntity> SignExistingBookingAsync(Guid bookingId, AgreementDataDto agreementData, string? ipAddress = null, CancellationToken ct = default);
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
    
    // Additional services
    public List<AgreementServiceItem>? AdditionalServices { get; set; }
    
    // Agreement data from frontend
    public AgreementDataDto AgreementData { get; set; } = new();
    
    // Request context
    public string? IpAddress { get; set; }
}

// Service item for agreement
public class AgreementServiceItem
{
    [System.Text.Json.Serialization.JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
    
    [System.Text.Json.Serialization.JsonPropertyName("dailyRate")]
    public decimal DailyRate { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("days")]
    public int Days { get; set; }
    
    [System.Text.Json.Serialization.JsonPropertyName("total")]
    public decimal Total { get; set; }
}

public class RentalAgreementService : IRentalAgreementService
{
    private readonly CarRentalDbContext _db;
    private readonly ILogger<RentalAgreementService> _logger;
    private readonly IAzureBlobStorageService _blobStorageService;

    public RentalAgreementService(
        CarRentalDbContext db,
        ILogger<RentalAgreementService> logger,
        IAzureBlobStorageService blobStorageService)
    {
        _db = db;
        _logger = logger;
        _blobStorageService = blobStorageService;
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
            
            // Additional services snapshot
            AdditionalServicesJson = request.AdditionalServices != null && request.AdditionalServices.Any()
                ? JsonSerializer.Serialize(request.AdditionalServices)
                : null,
            
            // Status
            Status = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        // Log additional services for debugging
        _logger.LogInformation(
            "[RentalAgreement] Additional services for booking {BookingId}: Count={Count}, JSON={Json}",
            request.BookingId,
            request.AdditionalServices?.Count ?? 0,
            agreement.AdditionalServicesJson ?? "null"
        );

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
        
        // Load booking with all related data
        var booking = await _db.Bookings
            .Include(b => b.Customer)
                .ThenInclude(c => c.License)
            .Include(b => b.Vehicle)
                .ThenInclude(v => v.VehicleModel)
                    .ThenInclude(vm => vm!.Model)
                        .ThenInclude(m => m.Category)
            .FirstOrDefaultAsync(b => b.Id == agreement.BookingId, ct);
        
        // Load additional services from agreement's JSON snapshot (preferred)
        // or fallback to booking's JSON, or booking_services table
        List<AdditionalServiceItem> additionalServices = new();
        decimal servicesTotal = 0;
        
        if (!string.IsNullOrEmpty(agreement.AdditionalServicesJson))
        {
            try
            {
                var savedServices = JsonSerializer.Deserialize<List<AgreementServiceItem>>(agreement.AdditionalServicesJson);
                if (savedServices != null)
                {
                    additionalServices = savedServices.Select(s => new AdditionalServiceItem
                    {
                        Name = s.Name,
                        DailyRate = s.DailyRate,
                        Days = s.Days,
                        Total = s.Total
                    }).ToList();
                    servicesTotal = additionalServices.Sum(s => s.Total);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deserialize additional services from agreement JSON");
            }
        }
        else if (booking != null && !string.IsNullOrEmpty(booking.AdditionalServicesJson))
        {
            // Fallback: try to load from booking's JSON
            try
            {
                var bookingServicesJson = JsonSerializer.Deserialize<List<AgreementServiceItem>>(booking.AdditionalServicesJson);
                if (bookingServicesJson != null)
                {
                    additionalServices = bookingServicesJson.Select(s => new AdditionalServiceItem
                    {
                        Name = s.Name,
                        DailyRate = s.DailyRate,
                        Days = s.Days,
                        Total = s.Total
                    }).ToList();
                    servicesTotal = additionalServices.Sum(s => s.Total);
                    
                    _logger.LogInformation("Loaded {Count} services from booking JSON for agreement {AgreementId}", 
                        additionalServices.Count, agreementId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not deserialize additional services from booking JSON");
            }
        }
        else
        {
            // Fallback: try to load from booking_services table
            try
            {
                var bookingServices = await _db.BookingServices
                    .Include(bs => bs.AdditionalService)
                    .Where(bs => bs.BookingId == agreement.BookingId)
                    .ToListAsync(ct);
                    
                additionalServices = bookingServices.Select(bs => new AdditionalServiceItem
                {
                    Name = bs.AdditionalService?.Name ?? "Service",
                    DailyRate = bs.PriceAtBooking,
                    Days = booking?.TotalDays ?? 1,
                    Total = bs.Subtotal
                }).ToList();
                servicesTotal = bookingServices.Sum(bs => bs.Subtotal);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load booking services from table, continuing without them");
            }
        }
        
        // Build vehicle info
        var vehicle = booking?.Vehicle;
        var vehicleModel = vehicle?.VehicleModel?.Model;
        var vehicleName = vehicleModel != null 
            ? $"{vehicleModel.Make} {vehicleModel.ModelName}".Trim()
            : (agreement.VehicleName ?? "Vehicle");
        var vehicleYear = vehicleModel?.Year;
        var vehicleType = vehicleModel?.Category?.CategoryName ?? "";
        
        // Build customer info
        var customer = booking?.Customer;
        var license = customer?.License;
        
        // Parse customer name into parts if needed
        var nameParts = agreement.CustomerName?.Split(' ', 2) ?? new[] { "", "" };
        var customerFirstName = customer?.FirstName ?? (nameParts.Length > 0 ? nameParts[0] : "");
        var customerLastName = customer?.LastName ?? (nameParts.Length > 1 ? nameParts[1] : "");
        
        // Build full address
        var customerAddress = customer?.Address;
        if (customer != null && !string.IsNullOrEmpty(customer.City))
        {
            var addressParts = new List<string>();
            if (!string.IsNullOrEmpty(customer.Address)) addressParts.Add(customer.Address);
            if (!string.IsNullOrEmpty(customer.City)) addressParts.Add(customer.City);
            if (!string.IsNullOrEmpty(customer.State)) addressParts.Add(customer.State);
            if (!string.IsNullOrEmpty(customer.PostalCode)) addressParts.Add(customer.PostalCode);
            customerAddress = string.Join(", ", addressParts);
        }
        
        // Calculate totals
        var rentalDays = booking?.TotalDays ?? 1;
        var dailyRate = booking?.DailyRate ?? 0;
        var rentalAmount = dailyRate * rentalDays;
        var subtotal = rentalAmount + servicesTotal;
        var totalCharges = booking?.TotalAmount ?? agreement.RentalAmount;
        
            // Get full rules and terms based on language
            var (rulesText, fullTermsText) = GetRulesAndTermsTexts(agreement.Language);
            
            // Generate PDF using the PDF generator
            var pdfGenerator = new RentalAgreementPdfGenerator();
            var pdfBytes = pdfGenerator.Generate(new RentalAgreementPdfData
            {
                AgreementNumber = agreement.AgreementNumber,
                Language = agreement.Language,
                CompanyName = company.CompanyName,
                CompanyAddress = GetCompanyAddress(company),
                CompanyPhone = null,
                CompanyEmail = company.Email,
                
                // Customer / Primary Renter
                CustomerName = agreement.CustomerName ?? "",
                CustomerFirstName = customerFirstName ?? "",
                CustomerMiddleName = license?.MiddleName,
                CustomerLastName = customerLastName ?? "",
                CustomerEmail = agreement.CustomerEmail ?? "",
                CustomerPhone = agreement.CustomerPhone ?? customer?.Phone,
                CustomerAddress = customerAddress ?? agreement.CustomerAddress,
                CustomerDateOfBirth = customer?.DateOfBirth,
                DriverLicenseNumber = license?.LicenseNumber ?? agreement.DriverLicenseNumber,
                DriverLicenseState = license?.StateIssued ?? agreement.DriverLicenseState,
                DriverLicenseExpiration = license?.ExpirationDate,
                
                // Vehicle
                VehicleName = vehicleName ?? "Vehicle",
                VehicleType = vehicleType,
                VehicleYear = vehicleYear,
                VehicleColor = vehicle?.Color,
                VehiclePlate = vehicle?.LicensePlate ?? agreement.VehiclePlate,
                VehicleVin = vehicle?.Vin,
                OdometerStart = vehicle?.Mileage,
                
                // Rental Period
                PickupDate = agreement.PickupDate,
                PickupTime = booking?.PickupTime,
                PickupLocation = agreement.PickupLocation ?? booking?.PickupLocation,
                ReturnDate = agreement.ReturnDate,
                ReturnTime = booking?.ReturnTime,
                ReturnLocation = agreement.ReturnLocation ?? booking?.ReturnLocation,
                
                // Financial
                RentalAmount = rentalAmount,
                DailyRate = dailyRate,
                RentalDays = rentalDays,
                DepositAmount = booking?.SecurityDeposit ?? agreement.DepositAmount,
                Currency = agreement.Currency,
                AdditionalServices = additionalServices,
                Subtotal = subtotal,
                TotalCharges = totalCharges,
                
                // Signature
                SignatureImage = agreement.SignatureImage,
                SignedAt = agreement.SignedAt,
                
                // Full Terms (not the short consent texts)
                RulesText = rulesText,
                FullTermsText = fullTermsText, // Use FullTermsText for the full legal document
                // Don't include short consent texts as they create empty sections
                // TermsText, NonRefundableText, DamagePolicyText, CardAuthorizationText are left empty
            });
        
        // Check if Azure Blob Storage is configured
        if (!await _blobStorageService.IsConfiguredAsync())
        {
            throw new InvalidOperationException("Azure Blob Storage is not configured. Cannot save rental agreement PDF.");
        }
        
        var fileName = $"{agreement.AgreementNumber}.pdf";
        const string containerName = "agreements";
        
        // Save PDF to company folder in blob storage: agreements/{companyId}/{fileName}
        var companyBlobPath = $"{agreement.CompanyId}/{fileName}";
        string pdfUrl;
        
        using (var stream = new MemoryStream(pdfBytes))
        {
            pdfUrl = await _blobStorageService.UploadFileAsync(stream, containerName, companyBlobPath, "application/pdf");
        }
        
        _logger.LogInformation("Uploaded agreement PDF to blob storage: {PdfUrl}", pdfUrl);
        
        // Additionally store under customers/{customerId}/agreements/{YYYY-MM-DD}/
        try
        {
            var dateFolder = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var customerBlobPath = $"{agreement.CustomerId}/agreements/{dateFolder}/{fileName}";
            
            using var customerStream = new MemoryStream(pdfBytes);
            var customerPdfUrl = await _blobStorageService.UploadFileAsync(customerStream, containerName, customerBlobPath, "application/pdf");
            
            _logger.LogInformation("Stored agreement PDF for customer at {CustomerPdfUrl}", customerPdfUrl);
        }
        catch (Exception copyEx)
        {
            // Don't fail overall generation if copy fails; log and continue
            _logger.LogWarning(copyEx, "Failed to copy agreement PDF to customer folder for Agreement {AgreementNumber}", agreement.AgreementNumber);
        }
        
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

    public async Task<RentalAgreementEntity> SignExistingBookingAsync(
        Guid bookingId, 
        AgreementDataDto agreementData, 
        string? ipAddress = null, 
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[RentalAgreement] SignExistingBookingAsync - BookingId: {BookingId}, HasSignature: {HasSignature}",
            bookingId,
            !string.IsNullOrEmpty(agreementData?.SignatureImage)
        );

        // Validate signature
        if (agreementData == null || string.IsNullOrEmpty(agreementData.SignatureImage))
        {
            throw new ArgumentException("SignatureImage is required", nameof(agreementData));
        }

        // Check if agreement already exists
        var existingAgreement = await _db.RentalAgreements
            .FirstOrDefaultAsync(a => a.BookingId == bookingId && a.Status != "Voided", ct);
        
        if (existingAgreement != null)
        {
            throw new InvalidOperationException($"Agreement already exists for booking {bookingId}");
        }

        // Get booking with related entities
        var booking = await _db.Bookings
            .Include(b => b.Customer)
            .Include(b => b.Vehicle)
                .ThenInclude(v => v.VehicleModel!)
                    .ThenInclude(vm => vm.Model)
            .Include(b => b.Company)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking == null)
        {
            throw new InvalidOperationException($"Booking {bookingId} not found");
        }

        // Get customer license
        var license = await _db.CustomerLicenses
            .FirstOrDefaultAsync(l => l.CustomerId == booking.CustomerId, ct);

        // Build customer address
        var customerAddressParts = new List<string>();
        if (!string.IsNullOrEmpty(booking.Customer.Address)) customerAddressParts.Add(booking.Customer.Address);
        if (!string.IsNullOrEmpty(booking.Customer.City)) customerAddressParts.Add(booking.Customer.City);
        if (!string.IsNullOrEmpty(booking.Customer.State)) customerAddressParts.Add(booking.Customer.State);
        if (!string.IsNullOrEmpty(booking.Customer.PostalCode)) customerAddressParts.Add(booking.Customer.PostalCode);
        var customerAddress = customerAddressParts.Count > 0 ? string.Join(", ", customerAddressParts) : null;

        // Get vehicle name
        var vehicleName = (booking.Vehicle?.VehicleModel?.Model != null)
            ? $"{booking.Vehicle.VehicleModel.Model.Make} {booking.Vehicle.VehicleModel.Model.ModelName} ({booking.Vehicle.VehicleModel.Model.Year})"
            : "Unknown Vehicle";

        // Create agreement request
        var agreementRequest = new CreateAgreementRequest
        {
            CompanyId = booking.CompanyId,
            BookingId = booking.Id,
            CustomerId = booking.CustomerId,
            VehicleId = booking.VehicleId,
            
            // Customer info
            CustomerName = $"{booking.Customer.FirstName} {booking.Customer.LastName}",
            CustomerEmail = booking.Customer.Email,
            CustomerPhone = booking.Customer.Phone,
            CustomerAddress = customerAddress,
            DriverLicenseNumber = license?.LicenseNumber,
            DriverLicenseState = license?.StateIssued,
            
            // Vehicle info
            VehicleName = vehicleName,
            VehiclePlate = booking.Vehicle?.LicensePlate,
            
            // Rental details
            PickupDate = booking.PickupDate,
            PickupLocation = booking.PickupLocation,
            ReturnDate = booking.ReturnDate,
            ReturnLocation = booking.ReturnLocation,
            RentalAmount = booking.TotalAmount,
            DepositAmount = booking.SecurityDeposit,
            Currency = booking.Company?.Currency ?? "USD",
            
            // Agreement data
            AgreementData = agreementData,
            
            // Request context
            IpAddress = ipAddress
        };

        // Create the agreement
        var agreement = await CreateAgreementAsync(agreementRequest, ct);

        // Generate PDF
        try
        {
            await GenerateAndStorePdfAsync(agreement.Id, ct);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Azure Blob Storage is not configured"))
        {
            _logger.LogWarning("Azure Blob Storage not configured for agreement {AgreementId}. PDF generation skipped: {Message}", agreement.Id, ex.Message);
            // Continue without PDF - this is acceptable for local development
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate PDF for agreement {AgreementId}: {Message}", agreement.Id, ex.Message);
            // Continue without PDF but log the detailed error
        }

        _logger.LogInformation(
            "[RentalAgreement] Successfully signed existing booking {BookingId} - AgreementId: {AgreementId}, AgreementNumber: {AgreementNumber}",
            bookingId,
            agreement.Id,
            agreement.AgreementNumber
        );

        return agreement;
    }

    private static string? GetCompanyAddress(Company company)
    {
        // Company model doesn't have address fields, return null or use company name
        // If you need company address, you may need to get it from CompanyLocation or another source
        return null;
    }
    
    private static (string RulesText, string FullTermsText) GetRulesAndTermsTexts(string language)
    {
        return language?.ToLower() switch
        {
            "es" => (
                RulesText: "REGLAS DE ACCIÓN\n\n" +
                    "SOLO CONDUCTORES AUTORIZADOS pueden operar este vehículo.\n\n" +
                    "PROHIBIDO ALCOHOL O NARCÓTICOS - Se prohíbe el uso de alcohol o narcóticos en el vehículo.\n\n" +
                    "NO FUMAR - Tarifa de limpieza de $250 por fumar en el vehículo.\n\n" +
                    "SIN MASCOTAS - No se permiten mascotas sin autorización previa.\n\n" +
                    "MANTENER EL VEHÍCULO LIMPIO - Devolver el vehículo en condiciones de limpieza razonables.\n\n" +
                    "INFORMAR DAÑOS INMEDIATAMENTE - Reportar cualquier daño o accidente de inmediato.\n\n" +
                    "SEGUIR LAS LEYES DE TRÁFICO - Obedecer todas las leyes de tráfico y estacionamiento.\n\n" +
                    "NO CONDUCIR FUERA DE CARRETERA - Solo uso en carretera a menos que se especifique.\n\n" +
                    "DEVOLUCIÓN CON TANQUE LLENO - Devolver con el nivel de combustible original o se aplicará cargo.\n\n" +
                    "NO SUBARRENDAR - No subarrendar ni prestar el vehículo.\n\n" +
                    "KILOMETRAJE PERMITIDO - Se puede aplicar cargo por exceso de kilometraje.",
                FullTermsText: GetFullTermsTextSpanish()
            ),
            _ => (
                RulesText: "RULES OF ACTION\n\n" +
                    "AUTHORIZED DRIVERS ONLY may operate this vehicle.\n\n" +
                    "NO ALCOHOL OR NARCOTICS - Use of alcohol or narcotics in the vehicle is prohibited.\n\n" +
                    "NO SMOKING - $250 cleaning fee for smoking in the vehicle.\n\n" +
                    "NO PETS - Pets are not allowed without prior authorization.\n\n" +
                    "KEEP VEHICLE CLEAN - Return the vehicle in reasonably clean condition.\n\n" +
                    "REPORT DAMAGE IMMEDIATELY - Report any damage or accidents immediately.\n\n" +
                    "FOLLOW TRAFFIC LAWS - Obey all traffic laws and parking regulations.\n\n" +
                    "NO OFF-ROAD DRIVING - On-road use only unless otherwise specified.\n\n" +
                    "RETURN WITH FULL TANK - Return with original fuel level or fuel charge applies.\n\n" +
                    "NO SUBLETTING - Do not sublet or loan the vehicle to others.\n\n" +
                    "MILEAGE ALLOWANCE - Excess mileage charges may apply.",
                FullTermsText: GetFullTermsTextEnglish()
            )
        };
    }

    private static string GetFullTermsTextEnglish()
    {
        return @"1. DEFINITIONS
""Agreement"" means this Rental Agreement and all terms and conditions herein. ""Company,"" ""We,"" ""Us,"" or ""Our"" refers to the rental company identified on the face of this Agreement. ""Renter,"" ""You,"" or ""Your"" refers to the person(s) identified as the renter on the face of this Agreement and any Authorized Driver. ""Authorized Driver"" means the Renter and any additional driver listed on this Agreement who has been approved by Us. ""Vehicle"" means the vehicle identified in this Agreement, including all its tires, tools, accessories, equipment, keys, and documents.

2. RENTAL AND RETURN
You agree to return the Vehicle to Our designated return location on the date and time specified in this Agreement. If You fail to return the Vehicle on time, You agree to pay: (a) an additional day's rental charge for each 24-hour period or portion thereof that the Vehicle is not returned; (b) all costs We incur in locating and recovering the Vehicle; and (c) any applicable late fees.

3. CONDITION AND RETURN OF VEHICLE
You acknowledge receiving the Vehicle in good operating condition, clean, with a full tank of fuel (unless otherwise noted), and with all accessories and equipment intact. You agree to return the Vehicle in the same condition. A cleaning fee will be charged if the Vehicle is returned excessively dirty. Fuel charges apply if the Vehicle is not returned with the same fuel level.

4. VEHICLE USE RESTRICTIONS
You agree NOT to use or permit the Vehicle to be used: (a) by anyone who is not an Authorized Driver; (b) by anyone under the influence of alcohol, drugs, or any substance that impairs driving ability; (c) for any illegal purpose; (d) to push or tow anything; (e) in any race, test, or contest; (f) to carry passengers for hire; (g) to transport hazardous materials; (h) outside the United States without Our prior written consent; (i) on unpaved roads or off-road; (j) to carry more passengers than the Vehicle has seat belts; (k) for smoking or transporting pets (unless pre-authorized).

5. PROHIBITED USES
The following uses are strictly prohibited and will void all liability coverage: (a) use by an unauthorized driver; (b) use while intoxicated; (c) use for illegal activities; (d) use in violation of the terms of this Agreement; (e) leaving the Vehicle unlocked or keys in the Vehicle; (f) failing to use reasonable care; (g) use during the commission of a crime.

6. YOUR RESPONSIBILITY FOR DAMAGE OR LOSS
You are responsible for all damage to, loss of, or theft of the Vehicle, regardless of fault, unless You have purchased and paid for optional damage waiver coverage (if available) and have complied with all terms of this Agreement. Your responsibility includes: (a) all physical damage; (b) loss due to theft; (c) vandalism; (d) acts of nature; (e) loss of use; (f) diminished value; (g) Our administrative fees.

7. INSURANCE AND LIABILITY
You are responsible for providing Your own automobile liability insurance. If You do not have insurance or Your insurance is insufficient, You are personally liable for all damages and claims. Any optional coverage We may offer is subject to the terms, conditions, and exclusions stated in the coverage documents.

8. INDEMNIFICATION
You agree to indemnify, defend, and hold Us harmless from all claims, liability, costs, and attorney fees arising from Your use of the Vehicle, Your breach of this Agreement, or any accident or incident involving the Vehicle during the rental period.

9. PAYMENT
You authorize Us to charge to Your credit/debit card: (a) all rental charges; (b) any optional services; (c) fees and surcharges; (d) applicable taxes; (e) fuel charges; (f) toll charges; (g) traffic/parking violations; (h) damage or loss not covered by insurance or waiver; (i) cleaning fees; (j) late return fees; (k) excess mileage charges; (l) administrative fees.

10. SECURITY DEPOSIT
A security deposit may be required and authorized on Your credit card at the time of rental. This deposit will be released within 7-14 business days after the Vehicle is returned, subject to inspection and final charges.

11. TRAFFIC AND PARKING VIOLATIONS
You are responsible for all traffic and parking violations, tolls, and related fees incurred during Your rental period. We may charge Your credit card for any violations We receive plus an administrative fee.

12. ACCIDENT PROCEDURES
In case of an accident: (a) report it immediately to the police and to Us; (b) do not admit fault or liability; (c) obtain names and contact information of all parties involved and witnesses; (d) take photographs if possible; (e) complete an accident report form.

13. DISCLAIMERS
WE MAKE NO WARRANTIES, EXPRESS OR IMPLIED, INCLUDING WARRANTIES OF MERCHANTABILITY OR FITNESS FOR A PARTICULAR PURPOSE. WE ARE NOT RESPONSIBLE FOR ANY LOSS OF YOUR PERSONAL PROPERTY LEFT IN THE VEHICLE.

14. ARBITRATION AGREEMENT
Any dispute arising out of or relating to this Agreement shall be resolved through binding arbitration in accordance with the rules of the American Arbitration Association. You waive Your right to a jury trial and to participate in a class action lawsuit.

15. GOVERNING LAW
This Agreement shall be governed by the laws of the state where the rental originates.

16. STATE-SPECIFIC PROVISIONS

CALIFORNIA: The California Consumer Privacy Act (CCPA) notice is available upon request.

ILLINOIS: The Illinois Consumer Fraud Act applies.

INDIANA: Total rental charges are outlined on the rental document.

NEVADA: Security deposit return timeframe is 30 days.

NEW YORK: New York General Business Law provisions apply.

WISCONSIN: This Agreement is subject to Wisconsin law.

17. ENTIRE AGREEMENT
This Agreement, including any addenda, constitutes the entire agreement between You and Us. No oral statements or representations modify this Agreement.

18. ACCEPTANCE
By signing this Agreement, You acknowledge that You have read, understand, and agree to be bound by all terms and conditions herein.";
    }

    private static string GetFullTermsTextSpanish()
    {
        return @"1. DEFINICIONES
""Contrato"" significa este Contrato de Alquiler y todos los términos y condiciones aquí establecidos. ""Compañía,"" ""Nosotros,"" o ""Nuestro"" se refiere a la empresa de alquiler identificada en este Contrato. ""Arrendatario,"" ""Usted,"" o ""Su"" se refiere a la(s) persona(s) identificada(s) como arrendatario en este Contrato y cualquier Conductor Autorizado.

2. ALQUILER Y DEVOLUCIÓN
Usted acepta devolver el Vehículo en la ubicación designada en la fecha y hora especificadas. Si no devuelve el Vehículo a tiempo, acepta pagar cargos adicionales.

3. CONDICIÓN Y DEVOLUCIÓN DEL VEHÍCULO
Usted reconoce haber recibido el Vehículo en buenas condiciones de funcionamiento, limpio y con el tanque lleno de combustible.

4. RESTRICCIONES DE USO DEL VEHÍCULO
Usted acepta NO usar o permitir que el Vehículo sea usado por personas no autorizadas, bajo la influencia de alcohol o drogas, o para fines ilegales.

5. USOS PROHIBIDOS
Los siguientes usos están estrictamente prohibidos y anularán toda cobertura de responsabilidad.

6. SU RESPONSABILIDAD POR DAÑOS O PÉRDIDAS
Usted es responsable de todos los daños al Vehículo, pérdida o robo, independientemente de la culpa.

7. SEGURO Y RESPONSABILIDAD
Usted es responsable de proporcionar su propio seguro de responsabilidad civil automotriz.

8. INDEMNIZACIÓN
Usted acepta indemnizar y eximir de responsabilidad a la Compañía de todas las reclamaciones.

9. PAGO
Usted nos autoriza a cargar a su tarjeta de crédito/débito todos los cargos de alquiler.

10. DEPÓSITO DE SEGURIDAD
Se puede requerir un depósito de seguridad en el momento del alquiler.

11. VIOLACIONES DE TRÁFICO Y ESTACIONAMIENTO
Usted es responsable de todas las violaciones de tráfico y estacionamiento.

12. PROCEDIMIENTOS EN CASO DE ACCIDENTE
En caso de accidente, repórtelo inmediatamente a la policía y a nosotros.

13. RENUNCIAS
NO HACEMOS GARANTÍAS, EXPRESAS O IMPLÍCITAS.

14. ACUERDO DE ARBITRAJE
Cualquier disputa se resolverá mediante arbitraje vinculante.

15. LEY APLICABLE
Este Contrato se regirá por las leyes del estado donde se origina el alquiler.

16. DISPOSICIONES ESPECÍFICAS POR ESTADO
Se aplican las disposiciones específicas según el estado.

17. ACUERDO COMPLETO
Este Contrato constituye el acuerdo completo entre Usted y Nosotros.

18. ACEPTACIÓN
Al firmar este Contrato, Usted reconoce que ha leído, entendido y acepta estar obligado por todos los términos y condiciones aquí establecidos.";
    }
}

