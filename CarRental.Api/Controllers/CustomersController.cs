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
    public async Task<ActionResult<IEnumerable<CustomerDto>>> GetCustomers(
        string? search = null,
        bool? isVerified = null,
        string? state = null,
        string? country = null,
        int page = 1,
        int pageSize = 20)
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
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync();

        return Ok(customers);
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
        var hasActiveReservations = await _context.Reservations
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
            TotalReservations = await _context.Reservations.CountAsync(r => r.CustomerId == id),
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
}
