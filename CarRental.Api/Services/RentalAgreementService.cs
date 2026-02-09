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
    public string VehicleName { get; set; } = string.Empty; // Deprecated: use separate fields
    public string? VehiclePlate { get; set; } // Deprecated: use VehicleLicensePlate
    public string? VehicleMake { get; set; }
    public string? VehicleModel { get; set; }
    public int? VehicleYear { get; set; }
    public string? VehicleColor { get; set; }
    public string? VehicleCategory { get; set; }
    public string? VehicleLicensePlate { get; set; }
    
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
            SmsConsentAcceptedAt = request.AgreementData.Consents.SmsConsentAcceptedAt ?? DateTime.UtcNow,

            // Consent texts (store exactly what was shown to customer)
            TermsText = $"{request.AgreementData.ConsentTexts.TermsTitle}\n{request.AgreementData.ConsentTexts.TermsText}",
            NonRefundableText = $"{request.AgreementData.ConsentTexts.NonRefundableTitle}\n{request.AgreementData.ConsentTexts.NonRefundableText}",
            DamagePolicyText = $"{request.AgreementData.ConsentTexts.DamagePolicyTitle}\n{request.AgreementData.ConsentTexts.DamagePolicyText}",
            CardAuthorizationText = $"{request.AgreementData.ConsentTexts.CardAuthorizationTitle}\n{request.AgreementData.ConsentTexts.CardAuthorizationText}",
            SmsConsentText = $"{request.AgreementData.ConsentTexts.SmsConsentTitle}\n{request.AgreementData.ConsentTexts.SmsConsentText}",
            
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
            .Include(b => b.Vehicle)
                .ThenInclude(v => v.VehicleModel)
                    .ThenInclude(vm => vm!.Model)
                        .ThenInclude(m => m.Category)
            .FirstOrDefaultAsync(b => b.Id == agreement.BookingId, ct);

        // Load customer license separately to ensure we get the data
        var license = await _db.CustomerLicenses
            .FirstOrDefaultAsync(l => l.CustomerId == agreement.CustomerId, ct);
        
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
        // license is already loaded separately above
        
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
        
            // Get full rules and terms based on language using unified service
            var (rulesText, fullTermsText) = RentalTermsService.GetRulesAndTermsTexts(agreement.Language);
            
            // Generate PDF using the PDF generator
            var pdfGenerator = new RentalAgreementPdfGenerator();
            var pdfBytes = pdfGenerator.Generate(new RentalAgreementPdfData
            {
                AgreementNumber = agreement.AgreementNumber,
                BookingNumber = booking?.BookingNumber ?? "BKG-" + agreement.BookingId.ToString()[..8],
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

                // SMS Consent - now rendered separately from numbered sections
                SmsConsentText = RentalTermsService.GetSmsConsentText(agreement.Language),

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

        // Debug logging for missing data
        _logger.LogInformation(
            "[RentalAgreement] Customer data - CustomerId: {CustomerId}, FirstName: '{FirstName}', LastName: '{LastName}', Email: '{Email}'",
            booking.CustomerId,
            booking.Customer.FirstName ?? "NULL",
            booking.Customer.LastName ?? "NULL",
            booking.Customer.Email ?? "NULL"
        );

        _logger.LogInformation(
            "[RentalAgreement] License data - HasLicense: {HasLicense}, LicenseNumber: '{LicenseNumber}', StateIssued: '{StateIssued}'",
            license != null,
            license?.LicenseNumber ?? "NULL",
            license?.StateIssued ?? "NULL"
        );

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

        // Check if booking is ready to be confirmed (payment + agreement signed)
        await CheckAndUpdateBookingStatusAsync(bookingId, ct);

        return agreement;
    }

    /// <summary>
    /// Checks if booking has both payment and agreement completed, and updates status to Confirmed if ready.
    /// This prevents double payments by ensuring status is confirmed only when both conditions are met.
    /// </summary>
    private async Task CheckAndUpdateBookingStatusAsync(Guid bookingId, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("[RentalAgreement] Checking if booking {BookingId} is ready for confirmation", bookingId);

            // Get booking with current status
            var booking = await _db.Bookings.FindAsync(new object[] { bookingId }, cancellationToken: ct);
            if (booking == null)
            {
                _logger.LogWarning("[RentalAgreement] Booking {BookingId} not found for status check", bookingId);
                return;
            }

            // Skip if already confirmed
            if (booking.Status == BookingStatus.Confirmed)
            {
                _logger.LogInformation("[RentalAgreement] Booking {BookingId} already confirmed", bookingId);
                return;
            }

            // Check if payment is completed
            bool paymentCompleted = booking.PaymentStatus == "completed";

            // Check if agreement is signed (we just signed it, so it should be true now)
            bool agreementSigned = await _db.RentalAgreements
                .AnyAsync(a => a.BookingId == bookingId && a.Status != "Voided" && !string.IsNullOrEmpty(a.SignatureImage), ct);

            _logger.LogInformation(
                "[RentalAgreement] Status check for booking {BookingId}: PaymentCompleted={PaymentCompleted}, AgreementSigned={AgreementSigned}",
                bookingId, paymentCompleted, agreementSigned);

            // Update status to Confirmed only if both conditions are met
            if (paymentCompleted && agreementSigned)
            {
                var rowsUpdated = await _db.Bookings
                    .Where(b => b.Id == bookingId && b.Status != BookingStatus.Confirmed)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(b => b.Status, BookingStatus.Confirmed)
                        .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), ct);

                if (rowsUpdated > 0)
                {
                    _logger.LogInformation(
                        "[RentalAgreement] Successfully updated booking {BookingId} status to Confirmed (payment + agreement both completed)",
                        bookingId);
                }
                else
                {
                    _logger.LogInformation(
                        "[RentalAgreement] Booking {BookingId} was already confirmed by another process",
                        bookingId);
                }
            }
            else
            {
                _logger.LogInformation(
                    "[RentalAgreement] Booking {BookingId} not ready for confirmation yet. Waiting for: {MissingRequirements}",
                    bookingId,
                    !paymentCompleted && !agreementSigned ? "payment and agreement" :
                    !paymentCompleted ? "payment" : "agreement");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RentalAgreement] Error checking booking {BookingId} status for confirmation", bookingId);
        }
    }

    private static string? GetCompanyAddress(Company company)
    {
        // Company model doesn't have address fields, return null or use company name
        // If you need company address, you may need to get it from CompanyLocation or another source
        return null;
    }

}
