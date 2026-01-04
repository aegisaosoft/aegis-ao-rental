using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace CarRental.Api.Data;

/// <summary>
/// Entity Framework Core models for Rental Agreement storage
/// Compatible with Azure PostgreSQL schema (uses gen_random_uuid())
/// Foreign keys reference existing tables: companies, bookings, customers, vehicles
/// </summary>

public class RentalAgreementEntity
{
    public Guid Id { get; set; }
    
    // Foreign keys (matching your existing schema)
    public Guid CompanyId { get; set; }
    public Guid BookingId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid VehicleId { get; set; }
    
    // Agreement reference
    [MaxLength(50)]
    public string AgreementNumber { get; set; } = null!;
    
    [MaxLength(20)]
    public string Language { get; set; } = "en";
    
    // Customer info snapshot
    [MaxLength(255)]
    public string CustomerName { get; set; } = null!;
    
    [MaxLength(255)]
    public string CustomerEmail { get; set; } = null!;
    
    [MaxLength(50)]
    public string? CustomerPhone { get; set; }
    
    public string? CustomerAddress { get; set; }
    
    [MaxLength(100)]
    public string? DriverLicenseNumber { get; set; }
    
    [MaxLength(100)]
    public string? DriverLicenseState { get; set; }
    
    // Rental details snapshot
    [MaxLength(255)]
    public string VehicleName { get; set; } = null!;
    
    [MaxLength(50)]
    public string? VehiclePlate { get; set; }
    
    public DateTime PickupDate { get; set; }
    
    [MaxLength(255)]
    public string? PickupLocation { get; set; }
    
    public DateTime ReturnDate { get; set; }
    
    [MaxLength(255)]
    public string? ReturnLocation { get; set; }
    
    // Payment details
    public decimal RentalAmount { get; set; }
    public decimal DepositAmount { get; set; }
    
    [MaxLength(10)]
    public string Currency { get; set; } = "USD";
    
    // Signature data
    public string SignatureImage { get; set; } = null!;
    
    [MaxLength(128)]
    public string SignatureHash { get; set; } = null!;
    
    // Consent timestamps
    public DateTime TermsAcceptedAt { get; set; }
    public DateTime NonRefundableAcceptedAt { get; set; }
    public DateTime DamagePolicyAcceptedAt { get; set; }
    public DateTime CardAuthorizationAcceptedAt { get; set; }
    
    // Consent texts
    public string TermsText { get; set; } = null!;
    public string NonRefundableText { get; set; } = null!;
    public string DamagePolicyText { get; set; } = null!;
    public string CardAuthorizationText { get; set; } = null!;
    
    // Signing metadata
    public DateTime SignedAt { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    public string? UserAgent { get; set; }
    
    [MaxLength(100)]
    public string? Timezone { get; set; }
    
    public JsonDocument? DeviceInfo { get; set; }
    
    // Geolocation
    public decimal? GeoLatitude { get; set; }
    public decimal? GeoLongitude { get; set; }
    public decimal? GeoAccuracy { get; set; }
    
    // PDF storage
    public string? PdfUrl { get; set; }
    public DateTime? PdfGeneratedAt { get; set; }
    
    // Additional services snapshot (JSON)
    public string? AdditionalServicesJson { get; set; }
    
    // Status
    [MaxLength(50)]
    public string Status { get; set; } = "active";
    
    public DateTime? VoidedAt { get; set; }
    public string? VoidedReason { get; set; }
    public Guid? SupersededById { get; set; }
    
    // Audit
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    // Navigation properties
    public virtual ICollection<RentalAgreementAuditLog> AuditLogs { get; set; } = new List<RentalAgreementAuditLog>();
}

public class RentalAgreementAuditLog
{
    public Guid Id { get; set; }
    public Guid AgreementId { get; set; }
    
    [MaxLength(50)]
    public string Action { get; set; } = null!;
    
    [MaxLength(255)]
    public string? PerformedBy { get; set; }
    
    public DateTime PerformedAt { get; set; }
    
    [MaxLength(45)]
    public string? IpAddress { get; set; }
    
    public string? UserAgent { get; set; }
    
    public JsonDocument? Details { get; set; }
    
    public DateTime CreatedAt { get; set; }
    
    // Navigation
    public virtual RentalAgreementEntity Agreement { get; set; } = null!;
}

public class RentalAgreementConfiguration : IEntityTypeConfiguration<RentalAgreementEntity>
{
    public void Configure(EntityTypeBuilder<RentalAgreementEntity> builder)
    {
        builder.ToTable("rental_agreements");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        
        // Column mappings (snake_case for PostgreSQL)
        builder.Property(e => e.CompanyId).HasColumnName("company_id");
        builder.Property(e => e.BookingId).HasColumnName("booking_id");
        builder.Property(e => e.CustomerId).HasColumnName("customer_id");
        builder.Property(e => e.VehicleId).HasColumnName("vehicle_id");
        builder.Property(e => e.AgreementNumber).HasColumnName("agreement_number");
        builder.Property(e => e.Language).HasColumnName("language");
        builder.Property(e => e.CustomerName).HasColumnName("customer_name");
        builder.Property(e => e.CustomerEmail).HasColumnName("customer_email");
        builder.Property(e => e.CustomerPhone).HasColumnName("customer_phone");
        builder.Property(e => e.CustomerAddress).HasColumnName("customer_address");
        builder.Property(e => e.DriverLicenseNumber).HasColumnName("driver_license_number");
        builder.Property(e => e.DriverLicenseState).HasColumnName("driver_license_state");
        builder.Property(e => e.VehicleName).HasColumnName("vehicle_name");
        builder.Property(e => e.VehiclePlate).HasColumnName("vehicle_plate");
        builder.Property(e => e.PickupDate).HasColumnName("pickup_date");
        builder.Property(e => e.PickupLocation).HasColumnName("pickup_location");
        builder.Property(e => e.ReturnDate).HasColumnName("return_date");
        builder.Property(e => e.ReturnLocation).HasColumnName("return_location");
        builder.Property(e => e.RentalAmount).HasColumnName("rental_amount").HasPrecision(10, 2);
        builder.Property(e => e.DepositAmount).HasColumnName("deposit_amount").HasPrecision(10, 2);
        builder.Property(e => e.Currency).HasColumnName("currency");
        builder.Property(e => e.SignatureImage).HasColumnName("signature_image");
        builder.Property(e => e.SignatureHash).HasColumnName("signature_hash");
        builder.Property(e => e.TermsAcceptedAt).HasColumnName("terms_accepted_at");
        builder.Property(e => e.NonRefundableAcceptedAt).HasColumnName("non_refundable_accepted_at");
        builder.Property(e => e.DamagePolicyAcceptedAt).HasColumnName("damage_policy_accepted_at");
        builder.Property(e => e.CardAuthorizationAcceptedAt).HasColumnName("card_authorization_accepted_at");
        builder.Property(e => e.TermsText).HasColumnName("terms_text");
        builder.Property(e => e.NonRefundableText).HasColumnName("non_refundable_text");
        builder.Property(e => e.DamagePolicyText).HasColumnName("damage_policy_text");
        builder.Property(e => e.CardAuthorizationText).HasColumnName("card_authorization_text");
        builder.Property(e => e.SignedAt).HasColumnName("signed_at");
        builder.Property(e => e.IpAddress).HasColumnName("ip_address");
        builder.Property(e => e.UserAgent).HasColumnName("user_agent");
        builder.Property(e => e.Timezone).HasColumnName("timezone");
        builder.Property(e => e.DeviceInfo).HasColumnName("device_info").HasColumnType("jsonb");
        builder.Property(e => e.GeoLatitude).HasColumnName("geo_latitude").HasPrecision(10, 8);
        builder.Property(e => e.GeoLongitude).HasColumnName("geo_longitude").HasPrecision(11, 8);
        builder.Property(e => e.GeoAccuracy).HasColumnName("geo_accuracy").HasPrecision(10, 2);
        builder.Property(e => e.PdfUrl).HasColumnName("pdf_url");
        builder.Property(e => e.PdfGeneratedAt).HasColumnName("pdf_generated_at");
        builder.Property(e => e.AdditionalServicesJson).HasColumnName("additional_services_json");
        builder.Property(e => e.Status).HasColumnName("status");
        builder.Property(e => e.VoidedAt).HasColumnName("voided_at");
        builder.Property(e => e.VoidedReason).HasColumnName("voided_reason");
        builder.Property(e => e.SupersededById).HasColumnName("superseded_by_id");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        // Indexes
        builder.HasIndex(e => e.CompanyId);
        builder.HasIndex(e => e.BookingId);
        builder.HasIndex(e => e.CustomerId);
        builder.HasIndex(e => new { e.CompanyId, e.AgreementNumber }).IsUnique();
        builder.HasIndex(e => e.CustomerEmail);
        builder.HasIndex(e => e.DriverLicenseNumber);
        builder.HasIndex(e => e.SignedAt);
        builder.HasIndex(e => e.SignatureHash);
        
        // Relationships
        builder.HasMany(e => e.AuditLogs)
            .WithOne(a => a.Agreement)
            .HasForeignKey(a => a.AgreementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RentalAgreementAuditLogConfiguration : IEntityTypeConfiguration<RentalAgreementAuditLog>
{
    public void Configure(EntityTypeBuilder<RentalAgreementAuditLog> builder)
    {
        builder.ToTable("rental_agreement_audit_log");
        
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.AgreementId).HasColumnName("agreement_id");
        builder.Property(e => e.Action).HasColumnName("action");
        builder.Property(e => e.PerformedBy).HasColumnName("performed_by");
        builder.Property(e => e.PerformedAt).HasColumnName("performed_at");
        builder.Property(e => e.IpAddress).HasColumnName("ip_address");
        builder.Property(e => e.UserAgent).HasColumnName("user_agent");
        builder.Property(e => e.Details).HasColumnName("details").HasColumnType("jsonb");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
        
        builder.HasIndex(e => e.AgreementId);
        builder.HasIndex(e => e.Action);
        builder.HasIndex(e => e.PerformedAt);
    }
}

