namespace CarRental.Api.Models;

/// <summary>
/// Driver license data extracted from PDF417 barcode (back side)
/// </summary>
public class DriverLicenseData
{
    // Personal Information
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;

    // Dates
    public string DateOfBirth { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;

    // License Information
    public string LicenseNumber { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string IssuingState { get; set; } = string.Empty;

    // Address
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    // Physical Description
    public string Gender { get; set; } = string.Empty;
    public string EyeColor { get; set; } = string.Empty;
    public string HairColor { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;

    // AAMVA Version Info
    public string AamvaVersion { get; set; } = string.Empty;
    public string JurisdictionVersion { get; set; } = string.Empty;

    // Additional fields for backwards compatibility
    public string Address { get; set; } = string.Empty; // Derived from AddressLine1
    public string Sex { get; set; } = string.Empty;     // Alias for Gender
}

/// <summary>
/// Enhanced driver license data from OCR processing (front side)
/// Includes confidence scores and additional fields
/// </summary>
public class LicenseOcrData
{
    // Personal Information (matching barcode format)
    public string FirstName { get; set; } = string.Empty;
    public string MiddleName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Suffix { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public string IssueDate { get; set; } = string.Empty;

    // Document Information (matching barcode format)
    public string LicenseNumber { get; set; } = string.Empty;
    public string DocumentNumber { get; set; } = string.Empty;
    public string IssuingState { get; set; } = string.Empty;

    // Address Information (matching barcode format)
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;

    // Physical Characteristics (matching barcode format)
    public string Gender { get; set; } = string.Empty;
    public string EyeColor { get; set; } = string.Empty;
    public string HairColor { get; set; } = string.Empty;
    public string Height { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;

    // AAMVA Information (matching barcode format)
    public string AamvaVersion { get; set; } = string.Empty;
    public string JurisdictionVersion { get; set; } = string.Empty;

    // Additional fields for backwards compatibility
    public string Address { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Sex { get; set; } = string.Empty;
    public string Restrictions { get; set; } = string.Empty;
    public string Endorsements { get; set; } = string.Empty;
    public string VehicleClass { get; set; } = string.Empty;

    // Confidence scores
    public Dictionary<string, float?> Confidence { get; set; } = new();
    public float? OverallConfidence { get; set; }
}

/// <summary>
/// Response models for license parsing endpoints
/// </summary>
public class LicenseParseResponse
{
    public bool Success { get; set; }
    public DriverLicenseData? Data { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string Source { get; set; } = "barcode";
}

public class LicenseAnalyzeResponse
{
    public bool Success { get; set; }
    public LicenseOcrData? Data { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public string Source { get; set; } = "google_document_ai";
}

public class HealthCheckResponse
{
    public string Service { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string[] Libraries { get; set; } = Array.Empty<string>();
    public string[] SupportedFormats { get; set; } = Array.Empty<string>();
    public string Version { get; set; } = string.Empty;
}