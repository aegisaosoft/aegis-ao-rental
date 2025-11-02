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

    public DbSet<RentalCompany> Companies { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<VehicleCategory> VehicleCategories { get; set; }
    public DbSet<Vehicle> Vehicles { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<CompanyLocation> CompanyLocations { get; set; }
    public DbSet<Reservation> Reservations { get; set; }
    public DbSet<Rental> Rentals { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<CustomerPaymentMethod> CustomerPaymentMethods { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<BookingToken> BookingTokens { get; set; }
    public DbSet<EmailNotification> EmailNotifications { get; set; }
    public DbSet<BookingConfirmation> BookingConfirmations { get; set; }
    public DbSet<CompanyEmailStyle> CompanyEmailStyles { get; set; }
    public DbSet<AdditionalService> AdditionalServices { get; set; }
    public DbSet<CompanyService> CompanyServices { get; set; }
    public DbSet<BookingService> BookingServices { get; set; }
    public DbSet<CustomerLicense> CustomerLicenses { get; set; }
    public DbSet<Model> Models { get; set; }
    public DbSet<VehicleModel> VehicleModels { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure UUID generation
        modelBuilder.Entity<RentalCompany>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

        modelBuilder.Entity<Customer>()
            .Property(e => e.Id)
            .HasDefaultValueSql("uuid_generate_v4()");

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

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Customer)
            .WithMany(c => c.Reservations)
            .HasForeignKey(r => r.CustomerId)
            .HasPrincipalKey(c => c.Id)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Vehicle)
            .WithMany(v => v.Reservations)
            .HasForeignKey(r => r.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Reservation>()
            .HasOne(r => r.Company)
            .WithMany(c => c.Reservations)
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

        // Configure CompanyService entity (junction table)
        modelBuilder.Entity<CompanyService>(entity =>
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
    }
}
