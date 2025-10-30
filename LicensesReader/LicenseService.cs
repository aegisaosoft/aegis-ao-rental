using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RentalPlatform.Services
{
    public interface ILicenseService
    {
        Task<LicenseVerificationResult> ProcessLicenseScanAsync(
            Guid companyId,
            Guid customerId,
            LicenseData licenseData,
            bool syncCustomerData = true,
            Guid? scannedBy = null);

        Task<CustomerWithLicenseDto?> GetCustomerWithLicenseAsync(Guid customerId);
        Task<List<ExpiredLicenseDto>> GetExpiredLicensesAsync(Guid companyId);
        Task<List<CustomerWithoutLicenseDto>> GetCustomersWithoutLicenseAsync(Guid companyId);
        Task<bool> IsLicenseAlreadyUsedAsync(Guid companyId, string licenseNumber, string state, Guid? excludeCustomerId = null);
    }

    public class LicenseService : ILicenseService
    {
        private readonly string _connectionString;
        private readonly ILogger<LicenseService> _logger;

        public LicenseService(IConfiguration configuration, ILogger<LicenseService> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") 
                ?? throw new ArgumentNullException("ConnectionString not configured");
            _logger = logger;
        }

        public async Task<LicenseVerificationResult> ProcessLicenseScanAsync(
            Guid companyId,
            Guid customerId,
            LicenseData licenseData,
            bool syncCustomerData = true,
            Guid? scannedBy = null)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var customerExists = await VerifyCustomerBelongsToCompanyAsync(connection, customerId, companyId);
                if (!customerExists)
                {
                    throw new InvalidOperationException("Customer not found or does not belong to this company");
                }

                var isLicenseUsed = await IsLicenseAlreadyUsedInternalAsync(
                    connection,
                    companyId,
                    licenseData.LicenseNumber!,
                    licenseData.State!,
                    customerId);

                if (isLicenseUsed)
                {
                    throw new InvalidOperationException(
                        $"License {licenseData.LicenseNumber} ({licenseData.State}) is already associated with another customer");
                }

                var (licenseId, fieldsUpdated) = await UpsertCustomerLicenseWithSyncAsync(
                    connection,
                    customerId,
                    companyId,
                    licenseData,
                    syncCustomerData,
                    scannedBy);

                await RecordLicenseScanAsync(
                    connection,
                    companyId,
                    customerId,
                    licenseId,
                    licenseData,
                    syncCustomerData && fieldsUpdated.Any(),
                    fieldsUpdated,
                    scannedBy);

                await transaction.CommitAsync();

                _logger.LogInformation(
                    "License scan processed successfully for customer {CustomerId}. " +
                    "License: {LicenseNumber}, Fields updated: {FieldsUpdated}",
                    customerId, licenseData.LicenseNumber, string.Join(", ", fieldsUpdated));

                return new LicenseVerificationResult
                {
                    Success = true,
                    CustomerId = customerId,
                    LicenseId = licenseId,
                    CustomerDataUpdated = fieldsUpdated.Any(),
                    FieldsUpdated = fieldsUpdated,
                    Message = "License verified and linked successfully"
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error processing license scan for customer {CustomerId}", customerId);
                throw;
            }
        }

        private async Task<bool> VerifyCustomerBelongsToCompanyAsync(
            NpgsqlConnection connection,
            Guid customerId,
            Guid companyId)
        {
            var query = @"
                SELECT EXISTS(
                    SELECT 1 FROM customers 
                    WHERE id = @customerId AND company_id = @companyId AND is_active = true
                )";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("customerId", customerId);
            command.Parameters.AddWithValue("companyId", companyId);

            var result = await command.ExecuteScalarAsync();
            return (bool)result!;
        }

        private async Task<bool> IsLicenseAlreadyUsedInternalAsync(
            NpgsqlConnection connection,
            Guid companyId,
            string licenseNumber,
            string state,
            Guid? excludeCustomerId = null)
        {
            var query = @"
                SELECT is_license_already_used(@companyId, @licenseNumber, @state, @excludeCustomerId)";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("companyId", companyId);
            command.Parameters.AddWithValue("licenseNumber", licenseNumber);
            command.Parameters.AddWithValue("state", state);
            command.Parameters.AddWithValue("excludeCustomerId", (object?)excludeCustomerId ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return (bool)result!;
        }

        public async Task<bool> IsLicenseAlreadyUsedAsync(
            Guid companyId,
            string licenseNumber,
            string state,
            Guid? excludeCustomerId = null)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            return await IsLicenseAlreadyUsedInternalAsync(
                connection,
                companyId,
                licenseNumber,
                state,
                excludeCustomerId);
        }

        private async Task<(Guid licenseId, List<string> fieldsUpdated)> UpsertCustomerLicenseWithSyncAsync(
            NpgsqlConnection connection,
            Guid customerId,
            Guid companyId,
            LicenseData data,
            bool syncCustomerData,
            Guid? createdBy)
        {
            var query = @"
                SELECT * FROM upsert_customer_license_with_sync(
                    @customerId, @companyId,
                    @licenseNumber, @stateIssued, @countryIssued,
                    @middleName, @sex, @height, @eyeColor,
                    @issueDate, @expirationDate,
                    @licenseAddress, @licenseCity, @licenseState, @licensePostalCode, @licenseCountry,
                    @restrictionCode, @endorsements,
                    @rawData,
                    @firstName, @lastName, @dateOfBirth,
                    @syncCustomerData,
                    @createdBy
                )";

            await using var command = new NpgsqlCommand(query, connection);

            command.Parameters.AddWithValue("customerId", customerId);
            command.Parameters.AddWithValue("companyId", companyId);
            command.Parameters.AddWithValue("licenseNumber", data.LicenseNumber ?? "");
            command.Parameters.AddWithValue("stateIssued", data.State ?? "");
            command.Parameters.AddWithValue("countryIssued", data.Country ?? "US");
            command.Parameters.AddWithValue("middleName", (object?)data.MiddleName ?? DBNull.Value);
            command.Parameters.AddWithValue("sex", (object?)data.Sex ?? DBNull.Value);
            command.Parameters.AddWithValue("height", (object?)data.Height ?? DBNull.Value);
            command.Parameters.AddWithValue("eyeColor", (object?)data.EyeColor ?? DBNull.Value);
            command.Parameters.AddWithValue("issueDate",
                string.IsNullOrEmpty(data.IssueDate) ? DBNull.Value : DateTime.Parse(data.IssueDate));
            command.Parameters.AddWithValue("expirationDate", DateTime.Parse(data.ExpirationDate!));
            
            command.Parameters.AddWithValue("licenseAddress", (object?)data.Address ?? DBNull.Value);
            command.Parameters.AddWithValue("licenseCity", (object?)data.City ?? DBNull.Value);
            command.Parameters.AddWithValue("licenseState", (object?)data.State ?? DBNull.Value);
            command.Parameters.AddWithValue("licensePostalCode", (object?)data.ZipCode ?? DBNull.Value);
            command.Parameters.AddWithValue("licenseCountry", (object?)data.Country ?? DBNull.Value);
            
            command.Parameters.AddWithValue("restrictionCode", (object?)data.RestrictionCode ?? DBNull.Value);
            command.Parameters.AddWithValue("endorsements", (object?)data.Endorsements ?? DBNull.Value);
            command.Parameters.AddWithValue("rawData", (object?)data.RawData ?? DBNull.Value);
            
            command.Parameters.AddWithValue("firstName", data.FirstName ?? "");
            command.Parameters.AddWithValue("lastName", data.LastName ?? "");
            command.Parameters.AddWithValue("dateOfBirth", DateTime.Parse(data.DateOfBirth!));
            
            command.Parameters.AddWithValue("syncCustomerData", syncCustomerData);
            command.Parameters.AddWithValue("createdBy", (object?)createdBy ?? DBNull.Value);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                var licenseId = reader.GetGuid(0);
                var fieldsUpdated = reader.IsDBNull(1) 
                    ? new List<string>() 
                    : ((string[])reader.GetValue(1)).ToList();
                
                return (licenseId, fieldsUpdated);
            }

            throw new InvalidOperationException("Failed to upsert customer license");
        }

        private async Task RecordLicenseScanAsync(
            NpgsqlConnection connection,
            Guid companyId,
            Guid customerId,
            Guid licenseId,
            LicenseData data,
            bool customerDataUpdated,
            List<string> fieldsUpdated,
            Guid? scannedBy)
        {
            var age = CalculateAge(data.DateOfBirth!);
            var isExpired = IsLicenseExpired(data.ExpirationDate!);
            var daysUntilExpiration = CalculateDaysUntilExpiration(data.ExpirationDate!);

            var query = @"
                INSERT INTO license_scans (
                    company_id, customer_id, customer_license_id,
                    scanned_by, scan_source,
                    device_type, app_version,
                    captured_data, barcode_data,
                    age_at_scan, was_expired, days_until_expiration,
                    validation_passed,
                    customer_data_updated, fields_updated
                ) VALUES (
                    @companyId, @customerId, @licenseId,
                    @scannedBy, @scanSource,
                    @deviceType, @appVersion,
                    @capturedData::jsonb, @barcodeData,
                    @age, @isExpired, @daysUntilExpiration,
                    @validationPassed,
                    @customerDataUpdated, @fieldsUpdated
                )";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("companyId", companyId);
            command.Parameters.AddWithValue("customerId", customerId);
            command.Parameters.AddWithValue("licenseId", licenseId);
            command.Parameters.AddWithValue("scannedBy", (object?)scannedBy ?? DBNull.Value);
            command.Parameters.AddWithValue("scanSource", "mobile_app");
            command.Parameters.AddWithValue("deviceType", data.DeviceType ?? "unknown");
            command.Parameters.AddWithValue("appVersion", data.AppVersion ?? "1.0.0");
            command.Parameters.AddWithValue("capturedData", JsonSerializer.Serialize(data));
            command.Parameters.AddWithValue("barcodeData", (object?)data.RawData ?? DBNull.Value);
            command.Parameters.AddWithValue("age", age);
            command.Parameters.AddWithValue("isExpired", isExpired);
            command.Parameters.AddWithValue("daysUntilExpiration", daysUntilExpiration);
            command.Parameters.AddWithValue("validationPassed", !isExpired && age >= 21);
            command.Parameters.AddWithValue("customerDataUpdated", customerDataUpdated);
            command.Parameters.AddWithValue("fieldsUpdated", fieldsUpdated.ToArray());

            await command.ExecuteNonQueryAsync();
        }

        public async Task<CustomerWithLicenseDto?> GetCustomerWithLicenseAsync(Guid customerId)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"SELECT * FROM v_customers_complete WHERE customer_id = @customerId";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("customerId", customerId);

            await using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new CustomerWithLicenseDto
                {
                    CustomerId = reader.GetGuid(reader.GetOrdinal("customer_id")),
                    CompanyId = reader.GetGuid(reader.GetOrdinal("company_id")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
                    FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                    LastName = reader.GetString(reader.GetOrdinal("last_name")),
                    DateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) 
                        ? null 
                        : reader.GetDateTime(reader.GetOrdinal("date_of_birth")),
                    Address = reader.IsDBNull(reader.GetOrdinal("address")) ? null : reader.GetString(reader.GetOrdinal("address")),
                    City = reader.IsDBNull(reader.GetOrdinal("city")) ? null : reader.GetString(reader.GetOrdinal("city")),
                    State = reader.IsDBNull(reader.GetOrdinal("state")) ? null : reader.GetString(reader.GetOrdinal("state")),
                    PostalCode = reader.IsDBNull(reader.GetOrdinal("postal_code")) ? null : reader.GetString(reader.GetOrdinal("postal_code")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("is_active")),
                    Role = reader.GetString(reader.GetOrdinal("role")),
                    
                    LicenseId = reader.IsDBNull(reader.GetOrdinal("license_id")) 
                        ? null 
                        : reader.GetGuid(reader.GetOrdinal("license_id")),
                    LicenseNumber = reader.IsDBNull(reader.GetOrdinal("license_number")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("license_number")),
                    LicenseState = reader.IsDBNull(reader.GetOrdinal("state_issued")) 
                        ? null 
                        : reader.GetString(reader.GetOrdinal("state_issued")),
                    ExpirationDate = reader.IsDBNull(reader.GetOrdinal("expiration_date")) 
                        ? null 
                        : reader.GetDateTime(reader.GetOrdinal("expiration_date")),
                    LicenseStatus = reader.IsDBNull(reader.GetOrdinal("license_status")) 
                        ? "No License" 
                        : reader.GetString(reader.GetOrdinal("license_status")),
                    Age = reader.IsDBNull(reader.GetOrdinal("age")) 
                        ? null 
                        : (int)(double)reader.GetValue(reader.GetOrdinal("age"))
                };
            }

            return null;
        }

        public async Task<List<ExpiredLicenseDto>> GetExpiredLicensesAsync(Guid companyId)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT * FROM v_expired_licenses
                WHERE company_id = @companyId
                ORDER BY days_expired DESC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("companyId", companyId);

            var expiredLicenses = new List<ExpiredLicenseDto>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expiredLicenses.Add(new ExpiredLicenseDto
                {
                    CustomerId = reader.GetGuid(reader.GetOrdinal("customer_id")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
                    FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                    LastName = reader.GetString(reader.GetOrdinal("last_name")),
                    LicenseNumber = reader.GetString(reader.GetOrdinal("license_number")),
                    State = reader.GetString(reader.GetOrdinal("state_issued")),
                    ExpirationDate = reader.GetDateTime(reader.GetOrdinal("expiration_date")),
                    DaysExpired = reader.GetInt32(reader.GetOrdinal("days_expired"))
                });
            }

            return expiredLicenses;
        }

        public async Task<List<CustomerWithoutLicenseDto>> GetCustomersWithoutLicenseAsync(Guid companyId)
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync();

            var query = @"
                SELECT * FROM v_customers_without_license
                WHERE company_id = @companyId
                ORDER BY created_at DESC";

            await using var command = new NpgsqlCommand(query, connection);
            command.Parameters.AddWithValue("companyId", companyId);

            var customers = new List<CustomerWithoutLicenseDto>();

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                customers.Add(new CustomerWithoutLicenseDto
                {
                    CustomerId = reader.GetGuid(reader.GetOrdinal("customer_id")),
                    Email = reader.GetString(reader.GetOrdinal("email")),
                    Phone = reader.IsDBNull(reader.GetOrdinal("phone")) ? null : reader.GetString(reader.GetOrdinal("phone")),
                    FirstName = reader.GetString(reader.GetOrdinal("first_name")),
                    LastName = reader.GetString(reader.GetOrdinal("last_name")),
                    DateOfBirth = reader.IsDBNull(reader.GetOrdinal("date_of_birth")) 
                        ? null 
                        : reader.GetDateTime(reader.GetOrdinal("date_of_birth")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at"))
                });
            }

            return customers;
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

    // DTO Classes
    public class LicenseData
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MiddleName { get; set; }
        public string? DateOfBirth { get; set; }
        public string? LicenseNumber { get; set; }
        public string? ExpirationDate { get; set; }
        public string? IssueDate { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Country { get; set; }
        public string? Sex { get; set; }
        public string? Height { get; set; }
        public string? EyeColor { get; set; }
        public string? RestrictionCode { get; set; }
        public string? Endorsements { get; set; }
        public string? RawData { get; set; }
        public string? DeviceType { get; set; }
        public string? AppVersion { get; set; }
    }

    public class LicenseVerificationResult
    {
        public bool Success { get; set; }
        public Guid CustomerId { get; set; }
        public Guid LicenseId { get; set; }
        public bool CustomerDataUpdated { get; set; }
        public List<string> FieldsUpdated { get; set; } = new();
        public string Message { get; set; } = string.Empty;
    }

    public class CustomerWithLicenseDto
    {
        public Guid CustomerId { get; set; }
        public Guid CompanyId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? PostalCode { get; set; }
        public bool IsActive { get; set; }
        public string Role { get; set; } = string.Empty;
        
        public Guid? LicenseId { get; set; }
        public string? LicenseNumber { get; set; }
        public string? LicenseState { get; set; }
        public DateTime? ExpirationDate { get; set; }
        public string LicenseStatus { get; set; } = string.Empty;
        public int? Age { get; set; }
    }

    public class ExpiredLicenseDto
    {
        public Guid CustomerId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string LicenseNumber { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public DateTime ExpirationDate { get; set; }
        public int DaysExpired { get; set; }
    }

    public class CustomerWithoutLicenseDto
    {
        public Guid CustomerId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public DateTime? DateOfBirth { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
