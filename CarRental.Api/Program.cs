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
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using CarRental.Api.Data;
using CarRental.Api.Services;

// Enable legacy timestamp behavior for Npgsql to handle DateTimes
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure file upload limits
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
    options.ValueLengthLimit = 524_288_000;
    options.MultipartHeadersLengthLimit = 524_288_000;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Car Rental API",
        Version = "v1",
        Description = "API for managing car rental operations",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "Car Rental API Support"
        }
    });
});

// Add Database Configuration Service
builder.Services.AddScoped<IDatabaseConfigService, DatabaseConfigService>();

// Add Entity Framework with configuration
builder.Services.AddDbContext<CarRentalDbContext>((serviceProvider, options) =>
{
    var dbConfigService = serviceProvider.GetRequiredService<IDatabaseConfigService>();
    var connectionString = dbConfigService.GetConnectionString();
    var dbSettings = dbConfigService.GetDatabaseSettings();

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(dbSettings.CommandTimeout);
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: dbSettings.MaxRetryCount,
            maxRetryDelay: TimeSpan.Parse(dbSettings.MaxRetryDelay),
            errorCodesToAdd: null);
    });

    if (dbSettings.EnableSensitiveDataLogging)
    {
        options.EnableSensitiveDataLogging();
    }

    if (dbSettings.EnableDetailedErrors)
    {
        options.EnableDetailedErrors();
    }

    if (dbSettings.EnableServiceProviderCaching)
    {
        options.EnableServiceProviderCaching();
    }

    // Query splitting behavior is handled at query level, not configuration level
    // This setting is for reference only
});

// Add JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Add Session Service
builder.Services.AddSingleton<ISessionService, SessionService>();

// Add Stripe Service
builder.Services.AddScoped<IStripeService, StripeService>();

// Add Company Management Service
builder.Services.AddScoped<ICompanyManagementService, CompanyManagementService>();

// Add Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Add Email Template Service
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "your-super-secret-jwt-key-that-should-be-at-least-32-characters-long-for-production-use";
var issuer = jwtSettings["Issuer"] ?? "CarRentalAPI";
var audience = jwtSettings["Audience"] ?? "CarRentalClients";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin =>
        {
            if (string.IsNullOrEmpty(origin))
                return false;

            var uri = new Uri(origin);
            
            // Allow localhost for development
            if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                return true;
            
            // Allow Azure websites
            if (uri.Host.EndsWith(".azurewebsites.net", StringComparison.OrdinalIgnoreCase))
                return true;
            
            // Allow aegis-rental.com and all subdomains
            if (uri.Host.Equals("aegis-rental.com", StringComparison.OrdinalIgnoreCase) ||
                uri.Host.EndsWith(".aegis-rental.com", StringComparison.OrdinalIgnoreCase))
                return true;
            
            return false;
        })
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
// Enable Swagger for all environments (Development, Staging, Production)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Car Rental API v1");
    c.RoutePrefix = string.Empty; // Serve Swagger UI at root
    c.DocumentTitle = "Car Rental API Documentation";
    c.DisplayRequestDuration();
    c.EnableDeepLinking();
    c.EnableFilter();
    c.ShowExtensions();
    c.EnableValidator();
});

app.UseHttpsRedirection();

// Enable static files for uploads
app.UseStaticFiles();

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();