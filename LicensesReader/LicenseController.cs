using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RentalPlatform.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LicenseController : ControllerBase
    {
        private readonly ILicenseService _licenseService;
        private readonly ILogger<LicenseController> _logger;

        public LicenseController(ILicenseService licenseService, ILogger<LicenseController> logger)
        {
            _licenseService = licenseService;
            _logger = logger;
        }

        /// <summary>
        /// Scan and link driver license to existing customer
        /// POST /api/license/scan
        /// </summary>
        [HttpPost("scan")]
        public async Task<ActionResult<LicenseVerificationResult>> ScanLicense(
            [FromBody] LicenseScanRequest request)
        {
            try
            {
                // TODO: Get companyId from authenticated user's claims
                // var companyId = Guid.Parse(User.FindFirst("CompanyId")?.Value ?? "");
                var companyId = request.CompanyId;

                _logger.LogInformation(
                    "Processing license scan for customer {CustomerId}, License: {LicenseNumber}",
                    request.CustomerId, request.LicenseData.LicenseNumber);

                var validationResult = ValidateLicenseData(request.LicenseData);
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { success = false, errors = validationResult.Errors });
                }

                var age = CalculateAge(request.LicenseData.DateOfBirth!);
                if (age < 21)
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Customer must be at least 21 years old to rent",
                        age = age
                    });
                }

                if (IsLicenseExpired(request.LicenseData.ExpirationDate!))
                {
                    return BadRequest(new
                    {
                        success = false,
                        error = "Driver license has expired",
                        expirationDate = request.LicenseData.ExpirationDate
                    });
                }

                var isAlreadyUsed = await _licenseService.IsLicenseAlreadyUsedAsync(
                    companyId,
                    request.LicenseData.LicenseNumber!,
                    request.LicenseData.State!,
                    request.CustomerId);

                if (isAlreadyUsed)
                {
                    return Conflict(new
                    {
                        success = false,
                        error = $"License {request.LicenseData.LicenseNumber} is already associated with another customer"
                    });
                }

                var result = await _licenseService.ProcessLicenseScanAsync(
                    companyId,
                    request.CustomerId,
                    request.LicenseData,
                    request.SyncCustomerData,
                    request.ScannedBy);

                return Ok(new
                {
                    success = true,
                    customerId = result.CustomerId,
                    licenseId = result.LicenseId,
                    message = result.Message,
                    age = age,
                    licenseStatus = "valid",
                    customerDataUpdated = result.CustomerDataUpdated,
                    fieldsUpdated = result.FieldsUpdated
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Invalid operation during license scan");
                return BadRequest(new { success = false, error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing license scan");
                return StatusCode(500, new
                {
                    success = false,
                    error = "Internal server error processing license scan"
                });
            }
        }

        /// <summary>
        /// Get customer's license information
        /// GET /api/license/customer/{customerId}
        /// </summary>
        [HttpGet("customer/{customerId}")]
        public async Task<ActionResult<CustomerWithLicenseDto>> GetCustomerLicense(Guid customerId)
        {
            try
            {
                var license = await _licenseService.GetCustomerWithLicenseAsync(customerId);

                if (license == null)
                {
                    return NotFound(new { error = "Customer not found" });
                }

                if (license.LicenseId == null)
                {
                    return Ok(new
                    {
                        customer = license,
                        hasLicense = false,
                        message = "Customer has no license on file"
                    });
                }

                var age = license.DateOfBirth.HasValue 
                    ? DateTime.Today.Year - license.DateOfBirth.Value.Year 
                    : 0;
                
                var isExpired = license.ExpirationDate < DateTime.Today;
                var daysUntilExpiration = license.ExpirationDate.HasValue 
                    ? (license.ExpirationDate.Value - DateTime.Today).Days 
                    : 0;

                return Ok(new
                {
                    customer = license,
                    hasLicense = true,
                    age = age,
                    isExpired = isExpired,
                    daysUntilExpiration = daysUntilExpiration,
                    status = isExpired ? "expired" : 
                             daysUntilExpiration < 30 ? "expiring_soon" : "valid"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving license for customer {CustomerId}", customerId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get all customers with expired licenses for a company
        /// GET /api/license/company/{companyId}/expired
        /// </summary>
        [HttpGet("company/{companyId}/expired")]
        public async Task<ActionResult<List<ExpiredLicenseDto>>> GetExpiredLicenses(Guid companyId)
        {
            try
            {
                var expiredLicenses = await _licenseService.GetExpiredLicensesAsync(companyId);

                return Ok(new
                {
                    count = expiredLicenses.Count,
                    expiredLicenses = expiredLicenses
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving expired licenses for company {CompanyId}", companyId);
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Get customers without license
        /// GET /api/license/company/{companyId}/without-license
        /// </summary>
        [HttpGet("company/{companyId}/without-license")]
        public async Task<ActionResult> GetCustomersWithoutLicense(Guid companyId)
        {
            try
            {
                var customers = await _licenseService.GetCustomersWithoutLicenseAsync(companyId);

                return Ok(new
                {
                    count = customers.Count,
                    customers = customers
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving customers without license");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Check if a license number is already in use
        /// GET /api/license/company/{companyId}/check-license?licenseNumber=D1234567&state=CA
        /// </summary>
        [HttpGet("company/{companyId}/check-license")]
        public async Task<ActionResult> CheckLicenseAvailability(
            Guid companyId,
            [FromQuery] string licenseNumber,
            [FromQuery] string state,
            [FromQuery] Guid? excludeCustomerId = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(licenseNumber) || string.IsNullOrWhiteSpace(state))
                {
                    return BadRequest(new { error = "License number and state are required" });
                }

                var isUsed = await _licenseService.IsLicenseAlreadyUsedAsync(
                    companyId,
                    licenseNumber,
                    state,
                    excludeCustomerId);

                return Ok(new
                {
                    licenseNumber = licenseNumber,
                    state = state,
                    isAvailable = !isUsed,
                    message = isUsed 
                        ? "This license is already associated with another customer" 
                        : "This license is available"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking license availability");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        /// <summary>
        /// Validate license data before scanning (pre-check)
        /// POST /api/license/validate
        /// </summary>
        [HttpPost("validate")]
        public ActionResult ValidateLicense([FromBody] LicenseData licenseData)
        {
            try
            {
                var validationResult = ValidateLicenseData(licenseData);
                
                if (!validationResult.IsValid)
                {
                    return BadRequest(new { valid = false, errors = validationResult.Errors });
                }

                var age = CalculateAge(licenseData.DateOfBirth!);
                var isExpired = IsLicenseExpired(licenseData.ExpirationDate!);
                var daysUntilExpiration = CalculateDaysUntilExpiration(licenseData.ExpirationDate!);

                var warnings = new List<string>();
                
                if (age < 21)
                {
                    warnings.Add($"Customer is only {age} years old. Minimum age is 21.");
                }

                if (isExpired)
                {
                    warnings.Add("License has expired");
                }
                else if (daysUntilExpiration < 30)
                {
                    warnings.Add($"License expires in {daysUntilExpiration} days");
                }

                return Ok(new
                {
                    valid = warnings.Count == 0,
                    age = age,
                    isExpired = isExpired,
                    daysUntilExpiration = daysUntilExpiration,
                    warnings = warnings
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating license");
                return StatusCode(500, new { error = "Internal server error" });
            }
        }

        // Private helper methods
        private ValidationResult ValidateLicenseData(LicenseData data)
        {
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(data.FirstName))
                errors.Add("First name is required");

            if (string.IsNullOrWhiteSpace(data.LastName))
                errors.Add("Last name is required");

            if (string.IsNullOrWhiteSpace(data.LicenseNumber))
                errors.Add("License number is required");

            if (string.IsNullOrWhiteSpace(data.DateOfBirth))
                errors.Add("Date of birth is required");
            else if (!DateTime.TryParse(data.DateOfBirth, out _))
                errors.Add("Invalid date of birth format");

            if (string.IsNullOrWhiteSpace(data.State))
                errors.Add("State is required");
            else if (data.State.Length != 2)
                errors.Add("State must be 2-letter code (e.g., CA, NY)");

            if (string.IsNullOrWhiteSpace(data.ExpirationDate))
                errors.Add("Expiration date is required");
            else if (!DateTime.TryParse(data.ExpirationDate, out _))
                errors.Add("Invalid expiration date format");

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private int CalculateAge(string dateOfBirth)
        {
            if (DateTime.TryParse(dateOfBirth, out DateTime dob))
            {
                var today = DateTime.Today;
                var age = today.Year - dob.Year;
                if (dob.Date > today.AddYears(-age)) age--;
                return age;
            }
            return 0;
        }

        private bool IsLicenseExpired(string expirationDate)
        {
            return DateTime.TryParse(expirationDate, out DateTime expDate) && expDate.Date < DateTime.Today;
        }

        private int CalculateDaysUntilExpiration(string expirationDate)
        {
            return DateTime.TryParse(expirationDate, out DateTime expDate) 
                ? (expDate.Date - DateTime.Today).Days 
                : 0;
        }
    }

    // Request Models
    public class LicenseScanRequest
    {
        public Guid CompanyId { get; set; }
        public Guid CustomerId { get; set; }
        public LicenseData LicenseData { get; set; } = new();
        public bool SyncCustomerData { get; set; } = true;
        public Guid? ScannedBy { get; set; }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
