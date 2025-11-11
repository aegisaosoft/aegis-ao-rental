using System;
using System.ComponentModel.DataAnnotations;

namespace CarRental.Api.DTOs.Payments;

public class CreateCheckoutSessionDto
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public Guid CompanyId { get; set; }

    [Obsolete("Use BookingId instead.")]
    public Guid? ReservationId { get; set; }

    public Guid? BookingId
    {
        get
        {
            if (_bookingId.HasValue)
            {
                return _bookingId;
            }
#pragma warning disable CS0618
            return ReservationId;
#pragma warning restore CS0618
        }
        set => _bookingId = value;
    }

    public string? BookingNumber { get; set; }

    private Guid? _bookingId;

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(10)]
    public string Currency { get; set; } = "usd";

    [StringLength(200)]
    public string? Description { get; set; }

    [Required]
    [Url]
    public string SuccessUrl { get; set; } = string.Empty;

    [Required]
    [Url]
    public string CancelUrl { get; set; } = string.Empty;
}
