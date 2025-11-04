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
using CarRental.Api.Filters;
using CarRental.Api.Middleware;
using CarRental.Api.Extensions;

// Enable legacy timestamp behavior for Npgsql to handle DateTimes
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Add standardized response filter to wrap all responses
    options.Filters.Add<StandardizedResponseFilter>();
});

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
    
    // Add JWT Bearer authentication to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token in the text input below.",
        Name = "Authorization",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
    
    // No longer need SwaggerFileOperationFilter since we use [FromForm] attributes
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

    // Note: We don't call UseModel() here, which forces EF Core to build the model at runtime
    // instead of trying to use potentially corrupted compiled models. This prevents 
    // "Token not valid Type token" errors that occur when compiled models are mismatched.

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

// Add Company Service for domain-based multi-tenancy
builder.Services.AddScoped<ICompanyService, CompanyService>();

// Add Memory Cache for company domain mapping
builder.Services.AddMemoryCache();

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

// Configure the HTTP request pipeline in correct order
// 1. Exception handling middleware (first to catch all exceptions)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. HTTPS Redirection (early in pipeline)
app.UseHttpsRedirection();

// 3. Static files for uploads
app.UseStaticFiles();

// 4. CORS (before authentication)
app.UseCors("AllowAll");

// 5. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 6. Company Middleware - must come after Authentication/Authorization
// This middleware resolves company from domain/subdomain and sets it in HttpContext
app.UseCompanyMiddleware();

// 7. Swagger (only in Development - file uploads cause issues in production)
if (app.Environment.IsDevelopment())
{
    try
    {
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "Car Rental API v1");
            c.RoutePrefix = "swagger"; // Access at /swagger
            c.DocumentTitle = "Car Rental API Documentation";
        });
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Swagger failed to initialize");
    }
}

// 8. Map Controllers (always last)
app.MapControllers();

app.Run();