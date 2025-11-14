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

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRental.Api.Data;
using CarRental.Api.DTOs;
using CarRental.Api.Models;
using CarRental.Api.Services;

namespace CarRental.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly CarRentalDbContext _context;
    private readonly IStripeService _stripeService;
    private readonly ILogger<CustomersController> _logger;

    public CustomersController(
        CarRentalDbContext context, 
        IStripeService stripeService, 
        ILogger<CustomersController> logger)
    {
        _context = context;
        _stripeService = stripeService;
        _logger = logger;
    }

    /// <summary>
    /// Get customer type constants
    /// </summary>
    /// <returns>List of available customer type values</returns>
    [HttpGet("type-constants")]
    [ProducesResponseType(typeof(object), 200)]
    public IActionResult GetCustomerTypeConstants()
    {
        var typeConstants = new
        {
            Individual = CustomerTypeConstants.Individual,
            Corporate = CustomerTypeConstants.Corporate
        };

        return Ok(typeConstants);
    }

    /// <summary>
    /// Get all customers with optional filtering
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<object>> GetCustomers(
        string? search = null,
        bool? isVerified = null,
        string? state = null,
        string? country = null,
        Guid? companyId = null,
        string? excludeRole = null,
        int page = 1,
        int pageSize = 20)
    {
        try
        {
            var query = _context.Customers.AsQueryable();

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => 
                c.FirstName.Contains(search) || 
                c.LastName.Contains(search) || 
                c.Email.Contains(search));
        }

        if (isVerified.HasValue)
            query = query.Where(c => c.IsVerified == isVerified.Value);

        if (!string.IsNullOrEmpty(state))
            query = query.Where(c => c.State == state);

        if (!string.IsNullOrEmpty(country))
            query = query.Where(c => c.Country == country);

        // Filter by companyId if provided
        if (companyId.HasValue)
        {
            query = query.Where(c => c.CompanyId == companyId.Value);
        }

        // Exclude specific role if provided (e.g., exclude 'customer' to get only employees)
        if (!string.IsNullOrEmpty(excludeRole))
        {
            query = query.Where(c => c.Role != excludeRole);
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        var customers = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto
            {
                CustomerId = c.Id,
                Email = c.Email,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone = c.Phone,
                DateOfBirth = c.DateOfBirth,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Country = c.Country,
                PostalCode = c.PostalCode,
                StripeCustomerId = c.StripeCustomerId,
                IsVerified = c.IsVerified,
                CustomerType = c.CustomerType.ToString(),
                Role = c.Role,
                CompanyId = c.CompanyId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

            return Ok(new
            {
                items = customers,
                totalCount = totalCount,
                page = page,
                pageSize = pageSize,
                totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting customers. CompanyId: {CompanyId}, ExcludeRole: {ExcludeRole}, Page: {Page}, PageSize: {PageSize}", 
                companyId, excludeRole, page, pageSize);
            _logger.LogError("Exception details: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
            
            // Check if it's a database column error
            if (ex.Message.Contains("is_security_deposit_mandatory") || (ex.Message.Contains("column") && ex.Message.Contains("does not exist")))
            {
                return StatusCode(500, new { 
                    error = "Database schema error: Missing column 'is_security_deposit_mandatory'. Please run the migration script: add_is_security_deposit_mandatory_to_companies.sql",
                    details = ex.Message 
                });
            }
            
            return StatusCode(500, new { error = "An error occurred while retrieving customers", details = ex.Message });
        }
    }

    /// <summary>
    /// Get customers who have bookings for a specific company with pagination
    /// </summary>
    [HttpGet("with-bookings/{companyId}")]
    public async Task<ActionResult<object>> GetCustomersWithBookings(
        Guid companyId,
        string? search = null,
        string? excludeRole = null,
        int page = 1,
        int pageSize = 20)
    {
        // Get distinct customer IDs who have bookings for this company
        var customerIdsWithBookings = await _context.Bookings
            .Where(b => b.CompanyId == companyId)
            .Select(b => b.CustomerId)
            .Distinct()
            .ToListAsync();

        // Query customers who have bookings
        var query = _context.Customers
            .Where(c => customerIdsWithBookings.Contains(c.Id) && c.CompanyId == companyId);

        // Exclude specific role if provided (e.g., exclude 'customer' to get only employees)
        if (!string.IsNullOrEmpty(excludeRole))
        {
            query = query.Where(c => c.Role != excludeRole);
        }

        // Apply search filter if provided
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(c => 
                c.FirstName.Contains(search) || 
                c.LastName.Contains(search) || 
                c.Email.Contains(search));
        }

        // Get total count for pagination
        var totalCount = await query.CountAsync();

        // Get paginated customers
        var customers = await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CustomerDto
            {
                CustomerId = c.Id,
                Email = c.Email,
                FirstName = c.FirstName,
                LastName = c.LastName,
                Phone = c.Phone,
                DateOfBirth = c.DateOfBirth,
                Address = c.Address,
                City = c.City,
                State = c.State,
                Country = c.Country,
                PostalCode = c.PostalCode,
                StripeCustomerId = c.StripeCustomerId,
                IsVerified = c.IsVerified,
                CustomerType = c.CustomerType.ToString(),
                Role = c.Role,
                CompanyId = c.CompanyId,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            items = customers,
            totalCount = totalCount,
            page = page,
            pageSize = pageSize,
            totalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
        });
    }

    /// <summary>
    /// Get a specific customer by ID
    /// </summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<CustomerDto>> GetCustomer(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        var customerDto = new CustomerDto
        {
            CustomerId = customer.Id,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            DateOfBirth = customer.DateOfBirth,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Country = customer.Country,
            PostalCode = customer.PostalCode,
            StripeCustomerId = customer.StripeCustomerId,
            IsVerified = customer.IsVerified,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };

        return Ok(customerDto);
    }

    /// <summary>
    /// Get customer by email
    /// </summary>
    [HttpGet("email/{email}")]
    public async Task<ActionResult<CustomerDto>> GetCustomerByEmail(string email)
    {
        var customer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Email == email);

        if (customer == null)
            return NotFound();

        var customerDto = new CustomerDto
        {
            CustomerId = customer.Id,
            Email = customer.Email,
            FirstName = customer.FirstName,
            LastName = customer.LastName,
            Phone = customer.Phone,
            DateOfBirth = customer.DateOfBirth,
            Address = customer.Address,
            City = customer.City,
            State = customer.State,
            Country = customer.Country,
            PostalCode = customer.PostalCode,
            StripeCustomerId = customer.StripeCustomerId,
            IsVerified = customer.IsVerified,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt
        };

        return Ok(customerDto);
    }

    /// <summary>
    /// Create a new customer
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> CreateCustomer(CreateCustomerDto createCustomerDto)
    {
        // Check if customer with email already exists
        var existingCustomer = await _context.Customers
            .FirstOrDefaultAsync(c => c.Email == createCustomerDto.Email);

        if (existingCustomer != null)
            return Conflict("Customer with this email already exists");

        var customer = new Customer
        {
            Email = createCustomerDto.Email,
            FirstName = createCustomerDto.FirstName,
            LastName = createCustomerDto.LastName,
            Phone = createCustomerDto.Phone,
            DateOfBirth = createCustomerDto.DateOfBirth,
            Address = createCustomerDto.Address,
            City = createCustomerDto.City,
            State = createCustomerDto.State,
            Country = createCustomerDto.Country,
            PostalCode = createCustomerDto.PostalCode,
            CustomerType = Enum.TryParse<CustomerType>(createCustomerDto.CustomerType ?? "Individual", out var customerType) ? customerType : CustomerType.Individual
        };

        try
        {
            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            // Create Stripe customer
            try
            {
                customer = await _stripeService.CreateCustomerAsync(customer);
                _context.Customers.Update(customer);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create Stripe customer for {Email}", customer.Email);
                // Continue without Stripe customer for now
            }

            var customerDto = new CustomerDto
            {
                CustomerId = customer.Id,
                Email = customer.Email,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Phone = customer.Phone,
                DateOfBirth = customer.DateOfBirth,
                Address = customer.Address,
                City = customer.City,
                State = customer.State,
                Country = customer.Country,
                PostalCode = customer.PostalCode,
                StripeCustomerId = customer.StripeCustomerId,
                IsVerified = customer.IsVerified,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt
            };

            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customerDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating customer");
            return BadRequest("Error creating customer");
        }
    }

    /// <summary>
    /// Update a customer
    /// </summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCustomer(Guid id, UpdateCustomerDto updateCustomerDto)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        // Check if email is being changed and if it already exists
        if (!string.IsNullOrEmpty(updateCustomerDto.Email) && updateCustomerDto.Email != customer.Email)
        {
            var existingCustomer = await _context.Customers
                .FirstOrDefaultAsync(c => c.Email == updateCustomerDto.Email && c.Id != id);

            if (existingCustomer != null)
                return Conflict("Customer with this email already exists");
        }

        // Update fields
        if (!string.IsNullOrEmpty(updateCustomerDto.Email))
            customer.Email = updateCustomerDto.Email;

        if (!string.IsNullOrEmpty(updateCustomerDto.FirstName))
            customer.FirstName = updateCustomerDto.FirstName;

        if (!string.IsNullOrEmpty(updateCustomerDto.LastName))
            customer.LastName = updateCustomerDto.LastName;

        if (updateCustomerDto.Phone != null)
            customer.Phone = updateCustomerDto.Phone;

        if (updateCustomerDto.DateOfBirth.HasValue)
            customer.DateOfBirth = updateCustomerDto.DateOfBirth;

        // Note: Drivers license fields have been removed

        if (updateCustomerDto.Address != null)
            customer.Address = updateCustomerDto.Address;

        if (updateCustomerDto.City != null)
            customer.City = updateCustomerDto.City;

        if (updateCustomerDto.State != null)
            customer.State = updateCustomerDto.State;

        if (updateCustomerDto.Country != null)
            customer.Country = updateCustomerDto.Country;

        if (updateCustomerDto.PostalCode != null)
            customer.PostalCode = updateCustomerDto.PostalCode;

        if (updateCustomerDto.CustomerType != null)
        {
            if (Enum.TryParse<CustomerType>(updateCustomerDto.CustomerType, out var customerType))
                customer.CustomerType = customerType;
        }

        // Update role if provided
        if (!string.IsNullOrEmpty(updateCustomerDto.Role))
        {
            customer.Role = updateCustomerDto.Role;
            // If role is being set to "customer", set company ID to null
            if (updateCustomerDto.Role == "customer")
            {
                customer.CompanyId = null;
            }
        }

        // Update company ID if provided (only if role is not "customer")
        // When role is being set to an employee role, CompanyId should be provided
        if (!string.IsNullOrEmpty(updateCustomerDto.Role) && updateCustomerDto.Role != "customer")
        {
            if (updateCustomerDto.CompanyId.HasValue)
            {
                customer.CompanyId = updateCustomerDto.CompanyId.Value;
            }
        }

        customer.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();

            // Update Stripe customer if exists
            if (!string.IsNullOrEmpty(customer.StripeCustomerId))
            {
                try
                {
                    await _stripeService.UpdateCustomerAsync(customer);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update Stripe customer {StripeCustomerId}", customer.StripeCustomerId);
                }
            }

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating customer {CustomerId}", id);
            return BadRequest("Error updating customer");
        }
    }

    /// <summary>
    /// Delete a customer
    /// </summary>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCustomer(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        // Check if customer has active reservations or rentals
        var hasActiveReservations = await _context.Bookings
            .AnyAsync(r => r.CustomerId == id && r.Status == "Confirmed");

        var hasActiveRentals = await _context.Rentals
            .AnyAsync(r => r.CustomerId == id && r.Status == "active");

        if (hasActiveReservations || hasActiveRentals)
            return BadRequest("Cannot delete customer with active reservations or rentals");

        try
        {
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting customer {CustomerId}", id);
            return BadRequest("Error deleting customer");
        }
    }

    /// <summary>
    /// Verify a customer
    /// </summary>
    [HttpPost("{id}/verify")]
    public async Task<IActionResult> VerifyCustomer(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        customer.IsVerified = true;
        customer.UpdatedAt = DateTime.UtcNow;

        try
        {
            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying customer {CustomerId}", id);
            return BadRequest("Error verifying customer");
        }
    }

    /// <summary>
    /// Get customer statistics
    /// </summary>
    [HttpGet("{id}/stats")]
    public async Task<ActionResult<object>> GetCustomerStats(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);

        if (customer == null)
            return NotFound();

        var stats = new
        {
            TotalReservations = await _context.Bookings.CountAsync(r => r.CustomerId == id),
            TotalRentals = await _context.Rentals.CountAsync(r => r.CustomerId == id),
            TotalPayments = await _context.Payments.CountAsync(p => p.CustomerId == id),
            TotalSpent = await _context.Payments.Where(p => p.CustomerId == id && p.Status == "succeeded")
                .SumAsync(p => p.Amount),
            AverageRating = await _context.Reviews.Where(r => r.CustomerId == id)
                .AverageAsync(r => (double?)r.Rating),
            LastRental = await _context.Rentals
                .Where(r => r.CustomerId == id)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => r.CreatedAt)
                .FirstOrDefaultAsync()
        };

        return Ok(stats);
    }

    /// <summary>
    /// Get customer license
    /// </summary>
    [HttpGet("{id}/license")]
    public async Task<ActionResult<CustomerLicenseDto>> GetCustomerLicense(Guid id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();

        var license = await _context.CustomerLicenses
            .FirstOrDefaultAsync(cl => cl.CustomerId == id);

        if (license == null)
            return NotFound("Customer license not found");

        var licenseDto = new CustomerLicenseDto
        {
            Id = license.Id,
            CustomerId = license.CustomerId,
            LicenseNumber = license.LicenseNumber,
            StateIssued = license.StateIssued,
            CountryIssued = license.CountryIssued,
            Sex = license.Sex,
            Height = license.Height,
            EyeColor = license.EyeColor,
            MiddleName = license.MiddleName,
            IssueDate = license.IssueDate,
            ExpirationDate = license.ExpirationDate,
            LicenseAddress = license.LicenseAddress,
            LicenseCity = license.LicenseCity,
            LicenseState = license.LicenseState,
            LicensePostalCode = license.LicensePostalCode,
            LicenseCountry = license.LicenseCountry,
            RestrictionCode = license.RestrictionCode,
            Endorsements = license.Endorsements,
            IsVerified = license.IsVerified,
            CreatedAt = license.CreatedAt,
            UpdatedAt = license.UpdatedAt
        };

        return Ok(licenseDto);
    }

    /// <summary>
    /// Create or update customer license
    /// </summary>
    [HttpPost("{id}/license")]
    public async Task<ActionResult<CustomerLicenseDto>> UpsertCustomerLicense(Guid id, CreateCustomerLicenseDto createLicenseDto)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound("Customer not found");

        // Check if license already exists
        var existingLicense = await _context.CustomerLicenses
            .FirstOrDefaultAsync(cl => cl.CustomerId == id);

        CustomerLicense license;
        if (existingLicense != null)
        {
            // Update existing license
            existingLicense.LicenseNumber = createLicenseDto.LicenseNumber;
            existingLicense.StateIssued = createLicenseDto.StateIssued;
            existingLicense.CountryIssued = createLicenseDto.CountryIssued ?? "US";
            existingLicense.Sex = createLicenseDto.Sex;
            existingLicense.Height = createLicenseDto.Height;
            existingLicense.EyeColor = createLicenseDto.EyeColor;
            existingLicense.MiddleName = createLicenseDto.MiddleName;
            existingLicense.IssueDate = createLicenseDto.IssueDate;
            existingLicense.ExpirationDate = createLicenseDto.ExpirationDate;
            existingLicense.LicenseAddress = createLicenseDto.LicenseAddress;
            existingLicense.LicenseCity = createLicenseDto.LicenseCity;
            existingLicense.LicenseState = createLicenseDto.LicenseState;
            existingLicense.LicensePostalCode = createLicenseDto.LicensePostalCode;
            existingLicense.LicenseCountry = createLicenseDto.LicenseCountry;
            existingLicense.RestrictionCode = createLicenseDto.RestrictionCode;
            existingLicense.Endorsements = createLicenseDto.Endorsements;
            existingLicense.UpdatedAt = DateTime.UtcNow;

            license = existingLicense;
            _context.CustomerLicenses.Update(license);
        }
        else
        {
            // Create new license
            license = new CustomerLicense
            {
                CustomerId = id,
                LicenseNumber = createLicenseDto.LicenseNumber,
                StateIssued = createLicenseDto.StateIssued,
                CountryIssued = createLicenseDto.CountryIssued ?? "US",
                Sex = createLicenseDto.Sex,
                Height = createLicenseDto.Height,
                EyeColor = createLicenseDto.EyeColor,
                MiddleName = createLicenseDto.MiddleName,
                IssueDate = createLicenseDto.IssueDate,
                ExpirationDate = createLicenseDto.ExpirationDate,
                LicenseAddress = createLicenseDto.LicenseAddress,
                LicenseCity = createLicenseDto.LicenseCity,
                LicenseState = createLicenseDto.LicenseState,
                LicensePostalCode = createLicenseDto.LicensePostalCode,
                LicenseCountry = createLicenseDto.LicenseCountry,
                RestrictionCode = createLicenseDto.RestrictionCode,
                Endorsements = createLicenseDto.Endorsements,
                IsVerified = true,
                VerificationDate = DateTime.UtcNow,
                VerificationMethod = "manual_entry"
            };

            _context.CustomerLicenses.Add(license);
        }

        try
        {
            await _context.SaveChangesAsync();

            var licenseDto = new CustomerLicenseDto
            {
                Id = license.Id,
                CustomerId = license.CustomerId,
                LicenseNumber = license.LicenseNumber,
                StateIssued = license.StateIssued,
                CountryIssued = license.CountryIssued,
                Sex = license.Sex,
                Height = license.Height,
                EyeColor = license.EyeColor,
                MiddleName = license.MiddleName,
                IssueDate = license.IssueDate,
                ExpirationDate = license.ExpirationDate,
                LicenseAddress = license.LicenseAddress,
                LicenseCity = license.LicenseCity,
                LicenseState = license.LicenseState,
                LicensePostalCode = license.LicensePostalCode,
                LicenseCountry = license.LicenseCountry,
                RestrictionCode = license.RestrictionCode,
                Endorsements = license.Endorsements,
                IsVerified = license.IsVerified,
                CreatedAt = license.CreatedAt,
                UpdatedAt = license.UpdatedAt
            };

            return Ok(licenseDto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving customer license for customer {CustomerId}", id);
            return BadRequest("Error saving customer license");
        }
    }
}
