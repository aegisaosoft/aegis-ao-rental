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

using Microsoft.EntityFrameworkCore;
using CarRental.Api.Models;

namespace CarRental.Api.Data;

public class CarRentalDbContext : DbContext
{
    public CarRentalDbContext(DbContextOptions<CarRentalDbContext> options) : base(options)
    {
    }

    public DbSet<Company> Companies { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<AegisUser> AegisUsers { get; set; }
    public DbSet<VehicleCategory> VehicleCategories { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<CompanyLocation> CompanyLocations { get; set; }
    public DbSet<CompanyMode> CompanyModes { get; set; }
    public DbSet<Booking> Bookings { get; set; }
    public DbSet<Booking> Reservations => Bookings;
    public DbSet<Rental> Rentals { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<CustomerPaymentMethod> CustomerPaymentMethods { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<BookingToken> BookingTokens { get; set; }
    public DbSet<EmailNotification> EmailNotifications { get; set; }
    public DbSet<BookingConfirmation> BookingConfirmations { get; set; }
    public DbSet<CompanyEmailStyle> CompanyEmailStyles { get; set; }
    public DbSet<AdditionalService> AdditionalServices { get; set; }
    public DbSet<CompanyServiceLink> CompanyServices { get; set; }
    public DbSet<BookingService> BookingServices { get; set; }
    public DbSet<CustomerLicense> CustomerLicenses { get; set; }
    public DbSet<Model> Models { get; set; }
    public DbSet<VehicleModel> VehicleModels { get; set; }
    public DbSet<Setting> Settings { get; set; }
    
    // Stripe Settings Tables
    public DbSet<StripeSettings> StripeSettings { get; set; }
    public DbSet<StripeCompany> StripeCompanies { get; set; }
    
    // Stripe & Payment Tables
    public DbSet<Currency> Currencies { get; set; }
    public DbSet<StripeTransfer> StripeTransfers { get; set; }
    public DbSet<StripePayoutRecord> StripePayoutRecords { get; set; }
    public DbSet<StripeBalanceTransaction> StripeBalanceTransactions { get; set; }
    public DbSet<StripeOnboardingSession> StripeOnboardingSessions { get; set; }
    public DbSet<StripeAccountCapability> StripeAccountCapabilities { get; set; }
    public DbSet<WebhookEvent> WebhookEvents { get; set; }
    public DbSet<RefundRecord> RefundRecords { get; set; }
    public DbSet<DisputeRecord> DisputeRecords { get; set; }
    public DbSet<DisputeEvidenceFile> DisputeEvidenceFiles { get; set; }
    
    // Analytics Tables
    public DbSet<RefundAnalytics> RefundAnalytics { get; set; }
    public DbSet<DisputeAnalytics> DisputeAnalytics { get; set; }
    
    // Insurance & License Tables
    public DbSet<AutoInsuranceCard> AutoInsuranceCards { get; set; }
    public DbSet<LicenseScan> LicenseScans { get; set; }
    
    // Violations Table
    public DbSet<Violation> Violations { get; set; }
    
    // Finders List Table
    public DbSet<FindersList> FindersLists { get; set; }
    
    // Rental Agreement Tables
    public DbSet<RentalAgreementEntity> RentalAgreements { get; set; }
    public DbSet<RentalAgreementAuditLog> RentalAgreementAuditLogs { get; set; }
    
    // Meta (Facebook/Instagram) Integration Tables
    public DbSet<CompanyMetaCredentials> CompanyMetaCredentials { get; set; }
    public DbSet<VehicleSocialPost> VehicleSocialPosts { get; set; }
    
    // Instagram Campaign Tables
    public DbSet<ScheduledPost> ScheduledPosts { get; set; }
    public DbSet<CompanyAutoPostSettings> CompanyAutoPostSettings { get; set; }
    public DbSet<SocialPostTemplate> SocialPostTemplates { get; set; }
    public DbSet<SocialPostAnalytics> SocialPostAnalytics { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Note: MetaCredentialStatus and SocialPlatform enums are stored as strings (VARCHAR)
        // using HasConversion<string>(), same pattern as CustomerType and other enums.
        // No PostgreSQL native enum types are used.

        // Configure UUID generation
        modelBuilder.Entity<Company>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        // Configure IsSecurityDepositMandatory
        modelBuilder.Entity<Company>()
            .Property(e => e.IsSecurityDepositMandatory)
            .HasDefaultValue(true)
            .IsRequired();

        // Configure StripeAccountId - not mapped to database column (stored in stripe_company table)
        modelBuilder.Entity<Company>()
            .Ignore(e => e.StripeAccountId);

        // Configure StripeSettingsId (nullable foreign key)
        modelBuilder.Entity<Company>()
            .Property(e => e.StripeSettingsId)
            .HasColumnName("stripe_settings_id")
            .IsRequired(false);

        // Configure Company to StripeSettings relationship (optional)
        modelBuilder.Entity<Company>()
            .HasOne<StripeSettings>()
            .WithMany()
            .HasForeignKey(c => c.StripeSettingsId)
            .HasPrincipalKey(ss => ss.Id)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Customer>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<AegisUser>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<AegisUser>()
            .Property(e => e.Role)
            .HasMaxLength(50)
            .HasDefaultValue("agent");

        modelBuilder.Entity<AegisUser>()
            .ToTable(tb => tb.HasCheckConstraint("valid_aegis_user_role", "role IN ('agent','admin','mainadmin','designer')"));

        modelBuilder.Entity<VehicleCategory>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Vehicle>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Location>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<CompanyLocation>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        // Configure CompanyMode
        modelBuilder.Entity<CompanyMode>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<CompanyMode>()
            .Property(e => e.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        // Configure CompanyMode to Company relationship
        modelBuilder.Entity<CompanyMode>()
            .HasOne(cm => cm.Company)
            .WithMany()
            .HasForeignKey(cm => cm.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure CompanyMode indexes
        modelBuilder.Entity<CompanyMode>()
            .HasIndex(cm => cm.CompanyId)
            .IsUnique(); // One CompanyMode per company

        modelBuilder.Entity<Reservation>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Rental>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Payment>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<CustomerPaymentMethod>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Setting>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Setting>()
            .HasIndex(s => s.Key)
            .IsUnique();

        modelBuilder.Entity<Review>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        // Configure relationships
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Company)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Vehicle.LocationDetails relationship (links to CompanyLocation via location_id)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.LocationDetails)
            .WithMany(cl => cl.Vehicles)
            .HasForeignKey(v => v.LocationId)
            .HasPrincipalKey(cl => cl.Id)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Explicitly map LocationId property to prevent shadow property creation
        modelBuilder.Entity<Vehicle>()
            .Property(v => v.LocationId)
            .HasColumnName("location_id");

        // Configure Vehicle.CurrentLocation relationship (links to Location via current_location_id)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.CurrentLocation)
            .WithMany(l => l.Vehicles)
            .HasForeignKey(v => v.CurrentLocationId)
            .HasPrincipalKey(l => l.Id)
            .OnDelete(DeleteBehavior.SetNull);
        
        // Explicitly map CurrentLocationId property to prevent shadow property creation
        modelBuilder.Entity<Vehicle>()
            .Property(v => v.CurrentLocationId)
            .HasColumnName("current_location_id");

        // Configure Vehicle.VehicleModel relationship (many-to-one)
        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.VehicleModel)
            .WithMany()
            .HasForeignKey(v => v.VehicleModelId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Location>()
            .HasOne(l => l.Company)
            .WithMany(c => c.Locations)
            .HasForeignKey(l => l.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CompanyLocation>()
            .HasOne(cl => cl.Company)
            .WithMany()
            .HasForeignKey(cl => cl.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .HasOne(r => r.Customer)
            .WithMany(c => c.Bookings)
            .HasForeignKey(r => r.CustomerId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Booking>()
            .HasOne(r => r.Vehicle)
            .WithMany(v => v.Bookings)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Booking>()
            .HasOne(r => r.Company)
            .WithMany(c => c.Bookings)
            .HasForeignKey(r => r.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Rental>()
            .HasOne(r => r.Booking)
            .WithMany(res => res.Rentals)
            .HasForeignKey(r => r.BookingId)
            .HasPrincipalKey(res => res.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Rental>()
            .HasOne(r => r.Customer)
            .WithMany(c => c.Rentals)
            .HasForeignKey(r => r.CustomerId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Rental>()
            .HasOne(r => r.Vehicle)
            .WithMany(v => v.Rentals)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Rental>()
            .HasOne(r => r.Company)
            .WithMany(c => c.Rentals)
            .HasForeignKey(r => r.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Customer)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.CustomerId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Payment>()
            .HasOne(p => p.Company)
            .WithMany(c => c.Payments)
            .HasForeignKey(p => p.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomerPaymentMethod>()
            .HasOne(pm => pm.Customer)
            .WithMany(c => c.PaymentMethods)
            .HasForeignKey(pm => pm.CustomerId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Rental)
            .WithMany(rental => rental.Reviews)
            .HasForeignKey(r => r.RentalId)
            .HasPrincipalKey(rental => rental.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Customer)
            .WithMany(c => c.Reviews)
            .HasForeignKey(r => r.CustomerId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Company)
            .WithMany(c => c.Reviews)
            .HasForeignKey(r => r.CompanyId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Review>()
            .HasOne(r => r.Vehicle)
            .WithMany(v => v.Reviews)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure CustomerLicense
        modelBuilder.Entity<CustomerLicense>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<CustomerLicense>()
            .Property(e => e.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<CustomerLicense>()
            .Property(e => e.UpdatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<CustomerLicense>()
            .Property(e => e.VerificationDate)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        modelBuilder.Entity<CustomerLicense>()
            .HasOne(cl => cl.Customer)
            .WithOne(c => c.License)
            .HasForeignKey<CustomerLicense>(cl => cl.CustomerId)
            .HasPrincipalKey<Customer>(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Model
        modelBuilder.Entity<Model>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        // Configure Make and ModelName to be stored in uppercase
        modelBuilder.Entity<Model>()
            .Property(e => e.Make)
            .HasConversion(
                v => v.ToUpperInvariant(),
                v => v
            );

        modelBuilder.Entity<Model>()
            .Property(e => e.ModelName)
            .HasConversion(
                v => v.ToUpperInvariant(),
                v => v
            );

        // DailyRate has been moved to VehicleModel table

        // Configure Features array
        modelBuilder.Entity<Model>()
            .Property(e => e.Features)
            .HasColumnType("text[]");

        // Configure Model to VehicleCategory relationship
        modelBuilder.Entity<Model>()
            .HasOne(m => m.Category)
            .WithMany()
            .HasForeignKey(m => m.CategoryId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Model>()
            .HasIndex(m => m.CategoryId);

        // Configure VehicleModel (catalog - primary key is id)
        modelBuilder.Entity<VehicleModel>(entity =>
        {
            entity.HasKey(e => e.Id); // Primary key is id
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.DailyRate).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
                
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .HasPrincipalKey(c => c.Id)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Model)
                .WithMany()
                .HasForeignKey(e => e.ModelId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure indexes
        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.CompanyId);

        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.Status);

        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.LocationId);

        modelBuilder.Entity<Vehicle>()
            .HasIndex(v => v.CurrentLocationId);

        modelBuilder.Entity<Location>()
            .HasIndex(l => l.CompanyId);

        modelBuilder.Entity<Location>()
            .HasIndex(l => l.IsActive);

        modelBuilder.Entity<Location>()
            .HasIndex(l => l.IsPickupLocation);

        modelBuilder.Entity<Location>()
            .HasIndex(l => l.IsReturnLocation);

        modelBuilder.Entity<CompanyLocation>()
            .HasIndex(cl => cl.CompanyId);

        modelBuilder.Entity<CompanyLocation>()
            .HasIndex(cl => cl.IsActive);

        modelBuilder.Entity<CompanyLocation>()
            .HasIndex(cl => cl.IsPickupLocation);

        modelBuilder.Entity<CompanyLocation>()
            .HasIndex(cl => cl.IsReturnLocation);

        modelBuilder.Entity<Reservation>()
            .HasIndex(r => r.CustomerId);

        modelBuilder.Entity<Reservation>()
            .HasIndex(r => r.VehicleId);

        modelBuilder.Entity<Reservation>()
            .HasIndex(r => r.CompanyId);

        modelBuilder.Entity<Reservation>()
            .HasIndex(r => new { r.PickupDate, r.ReturnDate });

        modelBuilder.Entity<Reservation>()
            .HasIndex(r => r.Status);

        modelBuilder.Entity<Rental>()
            .HasIndex(r => r.CustomerId);

        modelBuilder.Entity<Rental>()
            .HasIndex(r => r.VehicleId);

        modelBuilder.Entity<Rental>()
            .HasIndex(r => r.Status);

        modelBuilder.Entity<Rental>()
            .HasIndex(r => new { r.ActualPickupDate, r.ExpectedReturnDate });

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.CustomerId);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.CompanyId);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.ReservationId);

        modelBuilder.Entity<Payment>()
            .HasIndex(p => p.Status);

        modelBuilder.Entity<CustomerPaymentMethod>()
            .HasIndex(pm => pm.CustomerId);

        modelBuilder.Entity<CustomerLicense>()
            .HasIndex(cl => cl.CustomerId);

        modelBuilder.Entity<CustomerLicense>()
            .HasIndex(cl => new { cl.LicenseNumber, cl.StateIssued });

        modelBuilder.Entity<CustomerLicense>()
            .HasIndex(cl => cl.ExpirationDate);

        modelBuilder.Entity<CustomerLicense>()
            .HasIndex(cl => cl.StateIssued);

        modelBuilder.Entity<Model>()
            .HasIndex(m => m.Make);

        modelBuilder.Entity<Model>()
            .HasIndex(m => new { m.Make, m.ModelName });

        modelBuilder.Entity<Model>()
            .HasIndex(m => m.Year);

        // Configure array properties for PostgreSQL
        modelBuilder.Entity<Vehicle>()
            .Property(v => v.Features)
            .HasColumnType("text[]");

        // Configure enum conversions
        modelBuilder.Entity<Vehicle>()
            .Property(e => e.Status)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<Customer>()
            .Property(e => e.CustomerType)
            .HasConversion<string>()
            .HasMaxLength(50);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.DailyRate)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.Subtotal)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.TaxAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.InsuranceAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.AdditionalFees)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Reservation>()
            .Property(r => r.TotalAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Rental>()
            .Property(r => r.AdditionalCharges)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Payment>()
            .Property(p => p.Amount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Location>()
            .Property(l => l.Latitude)
            .HasPrecision(10, 8);

        modelBuilder.Entity<Location>()
            .Property(l => l.Longitude)
            .HasPrecision(11, 8);

        modelBuilder.Entity<CompanyLocation>()
            .Property(cl => cl.Latitude)
            .HasPrecision(10, 8);

        modelBuilder.Entity<CompanyLocation>()
            .Property(cl => cl.Longitude)
            .HasPrecision(11, 8);

        // Configure BookingToken entity
        modelBuilder.Entity<BookingToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Token).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CustomerEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // Explicitly ignore the BookingData property (it's a computed property from BookingDataJson)
            entity.Ignore(e => e.BookingData);
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure BookingConfirmation entity
        modelBuilder.Entity<BookingConfirmation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CustomerEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ConfirmationNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PaymentStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.BookingToken)
                .WithMany(bt => bt.BookingConfirmations)
                .HasForeignKey(e => e.BookingTokenId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Reservation)
                .WithMany()
                .HasForeignKey(e => e.ReservationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure EmailNotification entity
        modelBuilder.Entity<EmailNotification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CustomerEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.NotificationType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Subject).HasMaxLength(255).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(50).HasDefaultValue("pending");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.BookingToken)
                .WithMany(bt => bt.EmailNotifications)
                .HasForeignKey(e => e.BookingTokenId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        // Configure BookingConfirmation entity
        modelBuilder.Entity<BookingConfirmation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CustomerEmail).HasMaxLength(255).IsRequired();
            entity.Property(e => e.ConfirmationNumber).HasMaxLength(50).IsRequired();
            entity.Property(e => e.PaymentStatus).HasMaxLength(50).IsRequired();
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.BookingToken)
                .WithMany(e => e.BookingConfirmations)
                .HasForeignKey(e => e.BookingTokenId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Reservation)
                .WithMany()
                .HasForeignKey(e => e.ReservationId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure decimal precision for booking entities
        modelBuilder.Entity<BookingToken>()
            .Property(bt => bt.BookingData)
            .HasColumnType("jsonb");

        modelBuilder.Entity<BookingConfirmation>()
            .Property(bc => bc.BookingDetails)
            .HasColumnType("jsonb");

        // Configure AdditionalService entity
        modelBuilder.Entity<AdditionalService>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.ServiceType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.IsMandatory).HasDefaultValue(false);
            entity.Property(e => e.MaxQuantity).HasDefaultValue(1);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .HasPrincipalKey(c => c.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CompanyServiceLink entity (junction table)
        // Renamed from CompanyService to avoid conflict with CarRental.Api.Services.CompanyService
        modelBuilder.Entity<CompanyServiceLink>(entity =>
        {
            entity.HasKey(e => new { e.CompanyId, e.AdditionalServiceId });
            entity.Property(e => e.Price).HasPrecision(10, 2);
            entity.Property(e => e.IsMandatory);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .HasPrincipalKey(c => c.Id)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.AdditionalService)
                .WithMany()
                .HasForeignKey(e => e.AdditionalServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure BookingService entity (junction table)
        modelBuilder.Entity<BookingService>(entity =>
        {
            entity.HasKey(e => new { e.BookingId, e.AdditionalServiceId });
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.PriceAtBooking).HasPrecision(10, 2);
            entity.Property(e => e.Subtotal).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Booking)
                .WithMany()
                .HasForeignKey(e => e.BookingId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.AdditionalService)
                .WithMany()
                .HasForeignKey(e => e.AdditionalServiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure CompanyEmailStyle entity
        modelBuilder.Entity<CompanyEmailStyle>(entity =>
        {
            entity.HasKey(e => e.StyleId);
            entity.Property(e => e.StyleId).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.PrimaryColor).HasMaxLength(7).HasDefaultValue("#007bff");
            entity.Property(e => e.SecondaryColor).HasMaxLength(7).HasDefaultValue("#6c757d");
            entity.Property(e => e.SuccessColor).HasMaxLength(7).HasDefaultValue("#28a745");
            entity.Property(e => e.WarningColor).HasMaxLength(7).HasDefaultValue("#ffc107");
            entity.Property(e => e.InfoColor).HasMaxLength(7).HasDefaultValue("#17a2b8");
            entity.Property(e => e.BackgroundColor).HasMaxLength(7).HasDefaultValue("#f8f9fa");
            entity.Property(e => e.BorderColor).HasMaxLength(7).HasDefaultValue("#dee2e6");
            entity.Property(e => e.TextColor).HasMaxLength(7).HasDefaultValue("#333333");
            entity.Property(e => e.HeaderTextColor).HasMaxLength(7).HasDefaultValue("#ffffff");
            entity.Property(e => e.ButtonTextColor).HasMaxLength(7).HasDefaultValue("#ffffff");
            entity.Property(e => e.FooterColor).HasMaxLength(7).HasDefaultValue("#343a40");
            entity.Property(e => e.FooterTextColor).HasMaxLength(7).HasDefaultValue("#ffffff");
            entity.Property(e => e.LogoUrl).HasMaxLength(500);
            entity.Property(e => e.FooterText).HasMaxLength(500);
            entity.Property(e => e.FontFamily).HasMaxLength(100).HasDefaultValue("Arial, sans-serif");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Setting entity
        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Value).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(s => s.Key).IsUnique();
        });

        // Configure Currency entity
        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(e => e.Code);
            entity.Property(e => e.Code).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
        });

        // Configure StripeTransfer entity
        modelBuilder.Entity<StripeTransfer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.PlatformFee).HasPrecision(10, 2);
            entity.Property(e => e.NetAmount).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StripeTransferId).IsUnique();
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.Status);
        });

        // Configure StripePayoutRecord entity
        modelBuilder.Entity<StripePayoutRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StripePayoutId).IsUnique();
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.Status);
        });

        // Configure StripeBalanceTransaction entity
        modelBuilder.Entity<StripeBalanceTransaction>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.Net).HasPrecision(10, 2);
            entity.Property(e => e.Fee).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StripeBalanceTransactionId).IsUnique();
            entity.HasIndex(e => e.CompanyId);
        });

        // Configure StripeOnboardingSession entity
        modelBuilder.Entity<StripeOnboardingSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.Completed);
        });

        // Configure StripeAccountCapability entity
        modelBuilder.Entity<StripeAccountCapability>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.CompanyId, e.CapabilityName }).IsUnique();
            entity.HasIndex(e => e.Status);
        });

        // Configure WebhookEvent entity
        modelBuilder.Entity<WebhookEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StripeEventId).IsUnique();
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.Processed);
        });

        // Configure RefundRecord entity
        modelBuilder.Entity<RefundRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StripeRefundId).IsUnique();
            entity.HasIndex(e => e.BookingId);
        });

        // Configure DisputeRecord entity
        modelBuilder.Entity<DisputeRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.Amount).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => e.StripeDisputeId).IsUnique();
            entity.HasIndex(e => e.BookingId);
            entity.HasIndex(e => e.Status);
        });

        // Configure DisputeEvidenceFile entity
        modelBuilder.Entity<DisputeEvidenceFile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.HasIndex(e => e.DisputeId);
            
            entity.HasOne(e => e.Dispute)
                .WithMany(d => d.EvidenceFiles)
                .HasForeignKey(e => e.DisputeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure RefundAnalytics entity
        modelBuilder.Entity<RefundAnalytics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.TotalRefundAmount).HasPrecision(10, 2);
            entity.Property(e => e.SecurityDepositRefundAmount).HasPrecision(10, 2);
            entity.Property(e => e.RentalAdjustmentAmount).HasPrecision(10, 2);
            entity.Property(e => e.CancellationRefundAmount).HasPrecision(10, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.CompanyId, e.PeriodStart, e.PeriodEnd }).IsUnique();
        });

        // Configure DisputeAnalytics entity
        modelBuilder.Entity<DisputeAnalytics>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.TotalDisputedAmount).HasPrecision(10, 2);
            entity.Property(e => e.TotalLostAmount).HasPrecision(10, 2);
            entity.Property(e => e.AvgResolutionDays).HasPrecision(5, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("now()");
            entity.HasIndex(e => new { e.CompanyId, e.PeriodStart, e.PeriodEnd }).IsUnique();
        });

        // Configure AutoInsuranceCard entity
        modelBuilder.Entity<AutoInsuranceCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.OcrConfidence).HasPrecision(5, 2);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.ExpirationDate);
            entity.HasIndex(e => e.PolicyNumber);
            
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure LicenseScan entity
        modelBuilder.Entity<LicenseScan>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.ScanDate).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.ValidationErrors).HasColumnType("text[]");
            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.CustomerLicenseId);
            entity.HasIndex(e => new { e.CompanyId, e.ScanDate });
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.CustomerLicense)
                .WithMany()
                .HasForeignKey(e => e.CustomerLicenseId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Update Company configuration for Stripe properties
        modelBuilder.Entity<Company>()
            .Property(e => e.PlatformFeePercentage)
            .HasPrecision(5, 2);

        modelBuilder.Entity<Company>()
            .Property(e => e.StripeRequirementsCurrentlyDue)
            .HasColumnType("text[]");

        modelBuilder.Entity<Company>()
            .Property(e => e.StripeRequirementsEventuallyDue)
            .HasColumnType("text[]");

        modelBuilder.Entity<Company>()
            .Property(e => e.StripeRequirementsPastDue)
            .HasColumnType("text[]");

        // Update Booking configuration for new properties
        modelBuilder.Entity<Booking>()
            .Property(e => e.PlatformFeeAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .Property(e => e.NetAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Booking>()
            .HasIndex(e => e.PaymentIntentId);

        modelBuilder.Entity<Booking>()
            .HasIndex(e => e.SetupIntentId);

        modelBuilder.Entity<Booking>()
            .HasIndex(e => e.StripeCustomerId);

        modelBuilder.Entity<Booking>()
            .HasIndex(e => e.StripeTransferId);

        modelBuilder.Entity<Booking>()
            .HasIndex(e => e.SecurityDepositPaymentIntentId);

        // Update Payment configuration for new properties
        modelBuilder.Entity<Payment>()
            .Property(e => e.PlatformFeeAmount)
            .HasPrecision(10, 2);

        modelBuilder.Entity<Payment>()
            .HasIndex(e => e.DestinationAccountId);

        modelBuilder.Entity<Payment>()
            .HasIndex(e => e.TransferGroup);

        modelBuilder.Entity<Payment>()
            .HasIndex(e => e.StripeTransferId);

        // Update CustomerLicense configuration for CompanyId
        modelBuilder.Entity<CustomerLicense>()
            .HasOne(cl => cl.Company)
            .WithMany()
            .HasForeignKey(cl => cl.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CustomerLicense>()
            .HasIndex(cl => cl.CompanyId);

        // Configure FindersList entity
        modelBuilder.Entity<FindersList>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasDefaultValueSql("uuid_generate_v4()");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.FindersListJson).HasColumnName("finders_list").HasColumnType("jsonb").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("CURRENT_TIMESTAMP");
            
            // StateCodes is a [NotMapped] computed property, so it's automatically ignored by EF Core
            // It deserializes from FindersListJson when accessed
            
            entity.HasIndex(e => e.CompanyId).IsUnique();
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Configure Rental Agreements
        modelBuilder.ApplyConfiguration(new RentalAgreementConfiguration());
        modelBuilder.ApplyConfiguration(new RentalAgreementAuditLogConfiguration());
        
        // Configure CompanyMetaCredentials
        modelBuilder.Entity<CompanyMetaCredentials>(entity =>
        {
            entity.ToTable("company_meta_credentials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.UserAccessToken).HasColumnName("user_access_token").IsRequired();
            entity.Property(e => e.TokenExpiresAt).HasColumnName("token_expires_at").IsRequired();
            entity.Property(e => e.PageId).HasColumnName("page_id").HasMaxLength(50);
            entity.Property(e => e.PageName).HasColumnName("page_name").HasMaxLength(255);
            entity.Property(e => e.PageAccessToken).HasColumnName("page_access_token");
            entity.Property(e => e.CatalogId).HasColumnName("catalog_id").HasMaxLength(50);
            entity.Property(e => e.PixelId).HasColumnName("pixel_id").HasMaxLength(50);
            entity.Property(e => e.InstagramAccountId).HasColumnName("instagram_account_id").HasMaxLength(50);
            entity.Property(e => e.InstagramUsername).HasColumnName("instagram_username").HasMaxLength(100);
            
            // Configure JSONB for AvailablePages
            entity.Property(e => e.AvailablePages)
                .HasColumnName("available_pages")
                .HasColumnType("jsonb")
                .HasConversion(
                    v => v == null ? null : System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                    v => v == null || string.IsNullOrWhiteSpace(v) ? null : System.Text.Json.JsonDocument.Parse(v, default(System.Text.Json.JsonDocumentOptions)));
            
            // Map enum to string (same pattern as CustomerType, VehicleStatus, etc.)
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
            entity.Property(e => e.LastTokenRefresh).HasColumnName("last_token_refresh");
            
            // Auto-publish settings
            entity.Property(e => e.AutoPublishFacebook).HasColumnName("auto_publish_facebook").HasDefaultValue(false);
            entity.Property(e => e.AutoPublishInstagram).HasColumnName("auto_publish_instagram").HasDefaultValue(false);
            entity.Property(e => e.AutoPublishIncludePrice).HasColumnName("auto_publish_include_price").HasDefaultValue(true);
            entity.Property(e => e.AutoPublishHashtags).HasColumnName("auto_publish_hashtags");
            
            // Deep Link settings
            entity.Property(e => e.DeepLinkBaseUrl).HasColumnName("deep_link_base_url").HasMaxLength(500);
            entity.Property(e => e.DeepLinkVehiclePattern).HasColumnName("deep_link_vehicle_pattern").HasMaxLength(500);
            entity.Property(e => e.DeepLinkBookingPattern).HasColumnName("deep_link_booking_pattern").HasMaxLength(500);
            
            // Indexes
            entity.HasIndex(e => e.CompanyId).IsUnique();
            entity.HasIndex(e => e.Status).HasDatabaseName("idx_company_meta_credentials_status");
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure VehicleSocialPost
        modelBuilder.Entity<VehicleSocialPost>(entity =>
        {
            entity.ToTable("vehicle_social_posts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.VehicleId).HasColumnName("vehicle_id"); // Nullable for model posts
            entity.Property(e => e.VehicleModelId).HasColumnName("vehicle_model_id"); // For model posts
            // Map enum to string (same pattern as CustomerType, VehicleStatus, etc.)
            entity.Property(e => e.Platform)
                .HasColumnName("platform")
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();
            entity.Property(e => e.PostId).HasColumnName("post_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Permalink).HasColumnName("permalink").HasMaxLength(500);
            entity.Property(e => e.Caption).HasColumnName("caption");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url").HasMaxLength(1000);
            entity.Property(e => e.DailyRate).HasColumnName("daily_rate").HasColumnType("decimal(10,2)");
            entity.Property(e => e.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();
            
            // Indexes
            entity.HasIndex(e => new { e.CompanyId, e.VehicleId });
            entity.HasIndex(e => new { e.CompanyId, e.VehicleModelId });
            entity.HasIndex(e => new { e.CompanyId, e.Platform }).HasFilter("is_active = true");
            entity.HasIndex(e => new { e.VehicleId, e.Platform }).HasFilter("is_active = true");
            entity.HasIndex(e => new { e.VehicleModelId, e.Platform }).HasFilter("is_active = true");
            
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Model)
                .WithMany()
                .HasForeignKey(e => e.VehicleModelId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure ScheduledPost
        modelBuilder.Entity<ScheduledPost>(entity =>
        {
            entity.ToTable("scheduled_posts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.VehicleId).HasColumnName("vehicle_id");
            entity.Property(e => e.VehicleIds).HasColumnName("vehicle_ids").HasColumnType("uuid[]");
            entity.Property(e => e.PostType).HasColumnName("post_type").IsRequired();
            entity.Property(e => e.Platform).HasColumnName("platform").HasConversion<int>().IsRequired(); // int4 in database
            entity.Property(e => e.Caption).HasColumnName("caption");
            entity.Property(e => e.ScheduledFor).HasColumnName("scheduled_for").IsRequired();
            entity.Property(e => e.IncludePrice).HasColumnName("include_price");
            entity.Property(e => e.DailyRate).HasColumnName("daily_rate").HasColumnType("decimal(10,2)");
            entity.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(3);
            entity.Property(e => e.CustomHashtags).HasColumnName("custom_hashtags").HasColumnType("text[]");
            entity.Property(e => e.Status).HasColumnName("status").IsRequired();
            entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
            entity.Property(e => e.PostId).HasColumnName("post_id").HasMaxLength(100);
            entity.Property(e => e.Permalink).HasColumnName("permalink");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.RetryCount).HasColumnName("retry_count").HasDefaultValue(0);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.ScheduledFor);
            entity.HasIndex(e => new { e.Status, e.ScheduledFor }).HasFilter("status = 0");

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Vehicle)
                .WithMany()
                .HasForeignKey(e => e.VehicleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure CompanyAutoPostSettings
        modelBuilder.Entity<CompanyAutoPostSettings>(entity =>
        {
            entity.ToTable("company_auto_post_settings");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.IsEnabled).HasColumnName("is_enabled").HasDefaultValue(false);
            entity.Property(e => e.PostOnVehicleAdded).HasColumnName("post_on_vehicle_added").HasDefaultValue(true);
            entity.Property(e => e.PostOnVehicleUpdated).HasColumnName("post_on_vehicle_updated").HasDefaultValue(false);
            entity.Property(e => e.PostOnVehicleAvailable).HasColumnName("post_on_vehicle_available").HasDefaultValue(false);
            entity.Property(e => e.PostOnPriceChange).HasColumnName("post_on_price_change").HasDefaultValue(false);
            entity.Property(e => e.IncludePriceInPosts).HasColumnName("include_price_in_posts").HasDefaultValue(true);
            entity.Property(e => e.DefaultHashtags).HasColumnName("default_hashtags").HasColumnType("text[]");
            entity.Property(e => e.DefaultCallToAction).HasColumnName("default_call_to_action");
            entity.Property(e => e.CrossPostToFacebook).HasColumnName("cross_post_to_facebook").HasDefaultValue(false);
            entity.Property(e => e.MinHoursBetweenPosts).HasColumnName("min_hours_between_posts").HasDefaultValue(4);
            entity.Property(e => e.LastAutoPostAt).HasColumnName("last_auto_post_at");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(e => e.CompanyId).IsUnique();

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SocialPostTemplate
        modelBuilder.Entity<SocialPostTemplate>(entity =>
        {
            entity.ToTable("social_post_templates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasColumnName("description").HasMaxLength(500);
            entity.Property(e => e.CaptionTemplate).HasColumnName("caption_template").IsRequired();
            entity.Property(e => e.Hashtags).HasColumnName("hashtags").HasColumnType("text[]");
            entity.Property(e => e.CallToAction).HasColumnName("call_to_action");
            entity.Property(e => e.IncludePrice).HasColumnName("include_price").HasDefaultValue(true);
            entity.Property(e => e.ApplicableCategories).HasColumnName("applicable_categories").HasColumnType("text[]");
            entity.Property(e => e.IsDefault).HasColumnName("is_default").HasDefaultValue(false);
            entity.Property(e => e.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => new { e.CompanyId, e.IsActive });

            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure SocialPostAnalytics
        modelBuilder.Entity<SocialPostAnalytics>(entity =>
        {
            entity.ToTable("social_post_analytics");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            entity.Property(e => e.CompanyId).HasColumnName("company_id").IsRequired();
            entity.Property(e => e.SocialPostId).HasColumnName("social_post_id").IsRequired();
            entity.Property(e => e.PostId).HasColumnName("post_id").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Platform).HasColumnName("platform").HasConversion<int>().IsRequired(); // int4 in database
            entity.Property(e => e.Impressions).HasColumnName("impressions").HasDefaultValue(0);
            entity.Property(e => e.Reach).HasColumnName("reach").HasDefaultValue(0);
            entity.Property(e => e.Engagement).HasColumnName("engagement").HasDefaultValue(0);
            entity.Property(e => e.Likes).HasColumnName("likes").HasDefaultValue(0);
            entity.Property(e => e.Comments).HasColumnName("comments").HasDefaultValue(0);
            entity.Property(e => e.Shares).HasColumnName("shares").HasDefaultValue(0);
            entity.Property(e => e.Saves).HasColumnName("saves").HasDefaultValue(0);
            entity.Property(e => e.Clicks).HasColumnName("clicks").HasDefaultValue(0);
            entity.Property(e => e.ProfileVisits).HasColumnName("profile_visits").HasDefaultValue(0);
            entity.Property(e => e.RecordedAt).HasColumnName("recorded_at").IsRequired();

            entity.HasIndex(e => e.CompanyId);
            entity.HasIndex(e => e.SocialPostId);
            entity.HasIndex(e => e.RecordedAt);

            entity.HasOne(e => e.SocialPost)
                .WithMany()
                .HasForeignKey(e => e.SocialPostId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
