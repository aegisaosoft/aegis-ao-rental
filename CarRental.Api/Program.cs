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
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using CarRental.Api.Data;
using CarRental.Api.Services;
using CarRental.Api.Filters;
using CarRental.Api.Middleware;
using CarRental.Api.Extensions;
using CarRental.Api.HostedServices;
using CarRental.Api.Models;

// Enable legacy timestamp behavior for Npgsql to handle DateTimes
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Write to console immediately for Azure App Service debugging
Console.WriteLine("[Program] Application starting...");
Console.WriteLine($"[Program] Environment: {Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not set"}");
Console.WriteLine($"[Program] PORT: {Environment.GetEnvironmentVariable("PORT") ?? "Not set"}");
Console.WriteLine($"[Program] Current Directory: {Directory.GetCurrentDirectory()}");

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers(options =>
{
    // Add standardized response filter to wrap all responses
    options.Filters.Add<StandardizedResponseFilter>();
})
.AddJsonOptions(options =>
{
    // Configure JSON serialization to handle circular references
    options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    // Accept camelCase from frontend and convert to PascalCase for C# DTOs
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
});

// Configure file upload limits
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000; // 500 MB
    options.ValueLengthLimit = 524_288_000;
    options.MultipartHeadersLengthLimit = 524_288_000;
});

// Configure JSON request body size limit for large base64 image uploads
// A 2MB image becomes ~2.67MB in base64, so we need at least 10MB to handle multiple images
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 10_000_000; // 10MB
});
// Register Swagger services for all environments
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
        
        // Ignore schema errors that might prevent Swagger from generating
        c.IgnoreObsoleteActions();
        c.IgnoreObsoleteProperties();
        c.CustomSchemaIds(type => type.FullName ?? type.Name);
        
        // Handle errors during schema generation
        c.UseAllOfToExtendReferenceSchemas();
        c.UseOneOfForPolymorphism();
        c.UseAllOfForInheritance();
        
        // Add error handling filter
        c.SchemaFilter<CarRental.Api.Filters.SwaggerErrorHandlingFilter>();
        
        // Map types that might cause issues to simple object types
        c.MapType<object>(() => new Microsoft.OpenApi.Models.OpenApiSchema { Type = "object" });
        
        // No longer need SwaggerFileOperationFilter since we use [FromForm] attributes
    });

// Add Database Configuration Service
builder.Services.AddScoped<IDatabaseConfigService, DatabaseConfigService>();

// Add Entity Framework with configuration
builder.Services.AddDbContext<CarRentalDbContext>((serviceProvider, options) =>
{
    try
    {
        var dbConfigService = serviceProvider.GetRequiredService<IDatabaseConfigService>();
        var connectionString = dbConfigService.GetConnectionString();
        var dbSettings = dbConfigService.GetDatabaseSettings();

        // Log database configuration (without sensitive data)
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("[Database] Configuring database connection...");
        var connStr = dbConfigService.GetConnectionString();
        var hostPart = connStr.Split(';').FirstOrDefault(s => s.StartsWith("Host="));
        var dbPart = connStr.Split(';').FirstOrDefault(s => s.StartsWith("Database="));
        logger.LogInformation("[Database] Host: {Host}, Database: {Database}, Port: {Port}, SSLMode: {SSLMode}", 
            hostPart?.Replace("Host=", "") ?? "Unknown", 
            dbPart?.Replace("Database=", "") ?? "Unknown",
            connStr.Split(';').FirstOrDefault(s => s.StartsWith("Port="))?.Replace("Port=", "") ?? "Unknown",
            connStr.Split(';').FirstOrDefault(s => s.StartsWith("SSL Mode="))?.Replace("SSL Mode=", "") ?? "Unknown");

        // Note: MetaCredentialStatus and SocialPlatform enums are stored as strings (VARCHAR)
        // using HasConversion<string>() in DbContext, same pattern as CustomerType.
        // No PostgreSQL native enum types are used, so no MapEnum needed.
        
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
        
        // Success message removed
    }
    catch (Exception ex)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Failed to configure database context");
        throw;
    }
});

// Add JWT Service
builder.Services.AddScoped<IJwtService, JwtService>();

// Add Session Service
builder.Services.AddSingleton<ISessionService, SessionService>();

// Add Stripe Service
builder.Services.AddScoped<IStripeService, StripeService>();

// Add Stripe Connect Service
builder.Services.AddScoped<IStripeConnectService, StripeConnectService>();

// Add Encryption Service
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// Add Settings Service
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Add Driver License parsing services
builder.Services.Configure<CarRental.Api.Configurations.DocumentAiConfiguration>(
    builder.Configuration.GetSection("DocumentAi"));
builder.Services.AddSingleton<CarRental.Api.Configurations.IDocumentAiConfiguration>(serviceProvider =>
    serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<CarRental.Api.Configurations.DocumentAiConfiguration>>().Value);
builder.Services.AddScoped<CarRental.Api.Services.Interfaces.IBarcodeParserService, CarRental.Api.Services.BarcodeParserService>();

// Add Azure DNS Service (mandatory - requires Azure configuration)
builder.Services.AddScoped<IAzureDnsService, AzureDnsService>();

// Add Azure Blob Storage Service with smart fallback to local storage
// Register both storage services
builder.Services.AddScoped<AzureBlobStorageService>();
builder.Services.AddScoped<LocalFileStorageService>();

// Register the smart service that automatically chooses between Azure and local
builder.Services.AddScoped<IAzureBlobStorageService, SmartBlobStorageService>();

Console.WriteLine("✅ Smart Blob Storage Service registered - automatic fallback between Azure and local storage");

// Add Translation Service
builder.Services.AddScoped<ITranslationService, GoogleTranslationService>();

// Add Company Management Service
builder.Services.AddScoped<ICompanyManagementService, CompanyManagementService>();

// Add Company Service for domain-based multi-tenancy
builder.Services.AddScoped<ICompanyService, CarRental.Api.Services.CompanyService>();

// Add Rental Agreement Service
builder.Services.AddScoped<IRentalAgreementService, RentalAgreementService>();

// Add Memory Cache for company domain mapping
builder.Services.AddMemoryCache();

// Add Data Protection services
builder.Services.AddDataProtection();

// Add Health Checks
// Use simple health check for Azure - database check can timeout during startup
builder.Services.AddHealthChecks()
    .AddCheck("startup", () => HealthCheckResult.Healthy("Application is running"), tags: new[] { "ready" });
    // Removed DbContextCheck to avoid timeout during startup - database is lazy-loaded anyway

// Add Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Add Multi-Tenant Email Services
builder.Services.AddSingleton<EmailLocalizationService>();
builder.Services.AddScoped<ITenantBrandingService, TenantBrandingService>();
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();
builder.Services.AddScoped<MultiTenantEmailService>();

// Add background services
builder.Services.AddHostedService<SecurityDepositCollectionService>();
builder.Services.AddHostedService<MetaTokenRefreshBackgroundService>();

// Add Meta OAuth Integration Services
// Now reads settings from database (via SettingsService) with fallback to configuration
builder.Services.AddScoped<IMetaOAuthService, MetaOAuthService>();
builder.Services.AddScoped<ICompanyMetaCredentialsRepository, CompanyMetaCredentialsRepository>();
builder.Services.AddScoped<IVehicleSocialPostRepository, VehicleSocialPostRepository>();
builder.Services.AddScoped<IAutoPublishService, AutoPublishService>();

// Add Instagram Campaign Service
builder.Services.AddScoped<IInstagramCampaignService, InstagramCampaignService>();

// Add Instagram DM Booking Assistant Services
builder.Services.AddScoped<IInstagramMessagingService, InstagramMessagingService>();
builder.Services.AddScoped<IBookingAssistantService, BookingAssistantService>();

// Add Driver License Processing Services
builder.Services.AddScoped<BarcodeParserService>();
builder.Services.AddHttpClient(); // For Google Cloud Document AI integration

// Add Social Media Scheduler Background Service (for scheduled posts)
builder.Services.AddHostedService<SocialMediaSchedulerService>();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var jwtSettingsLegacy = builder.Configuration.GetSection("JwtSettings");
var key = jwtSettings["Key"] ?? jwtSettingsLegacy["SecretKey"] ?? "e8Xgin/OtynoYVm8o7jiNjB9/Fke1Q6RxjH3hJsRpTE=";
var issuer = jwtSettings["Issuer"] ?? jwtSettingsLegacy["Issuer"] ?? "CarRentalAPI";
var audience = jwtSettings["Audience"] ?? jwtSettingsLegacy["Audience"] ?? "CarRentalClients";

// Log the key (first 20 chars only for security)
Console.WriteLine($"[Program] JWT secret key (first 20 chars): {key?.Substring(0, Math.Min(20, key?.Length ?? 0))}...");
Console.WriteLine($"[Program] JWT secret key length: {key?.Length ?? 0} characters");

// Convert key to bytes using shared helper method from JwtService
// This ensures consistency with token generation/validation logic
byte[] keyBytes;
try
{
    keyBytes = JwtService.DecodeJwtKey(key, null); // No logger available at this point, use Console
    Console.WriteLine($"[Program] JWT secret key decoded successfully, key length: {keyBytes.Length} bytes");
}
catch (Exception ex)
{
    Console.WriteLine($"[Program] ERROR: Failed to decode JWT secret key.");
    Console.WriteLine($"[Program] Key value (first 50 chars): {key?.Substring(0, Math.Min(50, key?.Length ?? 0))}");
    Console.WriteLine($"[Program] Error: {ex.Message}");
    throw;
}

// Configure Authentication with multiple schemes
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero
    };
    
    // Add event handlers to log authentication failures
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            // Extract token - handle both "Bearer {token}" and just "{token}" formats
            if (!string.IsNullOrEmpty(authHeader) && !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                // Token sent without Bearer prefix - extract it manually
                context.Token = authHeader.Trim();
            }
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            logger.LogWarning("JWT Authentication failed for path {Path}: {Error}", context.Request.Path, context.Exception.Message);
            logger.LogWarning("Exception type: {ExceptionType}", context.Exception.GetType().Name);
            logger.LogWarning("Exception details: {Exception}", context.Exception.ToString());
            if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenExpiredException)
            {
                logger.LogWarning("Token is expired");
            }
            else if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenInvalidSignatureException)
            {
                logger.LogWarning("Token signature is invalid");
            }
            else if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenInvalidIssuerException)
            {
                logger.LogWarning("Token issuer is invalid. Expected: {ExpectedIssuer}", issuer);
            }
            else if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException)
            {
                logger.LogWarning("Token audience is invalid. Expected: {ExpectedAudience}", audience);
            }
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            logger.LogWarning("JWT Challenge triggered for path {Path}: Error={Error}, ErrorDescription={ErrorDescription}", 
                context.Request.Path, context.Error, context.ErrorDescription);
            logger.LogWarning("JWT Challenge: Authorization header present={HasHeader}, Value={HeaderValue}", 
                !string.IsNullOrEmpty(authHeader), 
                authHeader != null && authHeader.Length > 50 ? authHeader.Substring(0, 50) + "..." : authHeader);
            if (context.AuthenticateFailure != null)
            {
                logger.LogWarning("JWT Challenge: AuthenticateFailure={Failure}", context.AuthenticateFailure.ToString());
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Add CORS - Allow specific origins with credentials support
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        // Get allowed origins from configuration or use defaults
        var allowedOrigins = builder.Configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>()
            ?? new[]
            {
                "https://admin.aegis-rental.com",
                "https://copacabana.aegis-rental.com",
                "http://localhost:3000",
                "https://localhost:3000",
                "http://localhost:4000",
                "https://localhost:4000",
                "http://localhost:5000",
                "https://localhost:5000"
            };

        var staticOrigins = new HashSet<string>(allowedOrigins, StringComparer.OrdinalIgnoreCase);

        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(origin =>
              {
                  if (string.IsNullOrWhiteSpace(origin))
                      return false;

                  if (staticOrigins.Contains(origin))
                      return true;

                  if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
                      return false;

                  var host = uri.Host;
                  if (host.Equals("aegis-rental.com", StringComparison.OrdinalIgnoreCase))
                      return true;

                  if (host.EndsWith(".aegis-rental.com", StringComparison.OrdinalIgnoreCase))
                      return true;

                  return false;
              });
    });
});

// Register HttpClient factory for HTTP services
builder.Services.AddHttpClient();

var app = builder.Build();

// Write to console immediately (before logger is available)
Console.WriteLine("[Program] Application built successfully");
Console.WriteLine($"[Program] Environment: {app.Environment.EnvironmentName}");

// Log environment information early
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("=== Application Startup ===");
Console.WriteLine("[Program] Logger initialized");
startupLogger.LogInformation("Build timestamp: {Timestamp}", DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC"));
startupLogger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);
startupLogger.LogInformation("Content Root: {ContentRoot}", app.Environment.ContentRootPath);
startupLogger.LogInformation("Application Name: {ApplicationName}", app.Environment.ApplicationName);

// Configure the HTTP request pipeline in correct order
startupLogger.LogInformation("Configuring HTTP request pipeline...");

// 0. Forwarded Headers (must be first for proxy support)
startupLogger.LogInformation("Setting up forwarded headers...");
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost,
    // Trust all proxies in Azure (Azure App Service acts as a reverse proxy)
    RequireHeaderSymmetry = false
});

// 1. Exception handling middleware (first to catch all exceptions)
startupLogger.LogInformation("Adding exception handling middleware...");
app.UseMiddleware<ExceptionHandlingMiddleware>();

// 2. HTTPS Redirection (early in pipeline, but skip in Azure App Service)
// Azure App Service handles HTTPS termination, so we only redirect in non-Azure environments
var websiteInstanceId = builder.Configuration["WEBSITE_INSTANCE_ID"];
if (string.IsNullOrEmpty(websiteInstanceId))
{
    startupLogger.LogInformation("Configuring HTTPS redirection...");
    app.UseHttpsRedirection();
}
else
{
    startupLogger.LogInformation("Skipping HTTPS redirection (Azure App Service environment detected)");
}

// 3. Static files for uploads (serve from wwwroot/public)
startupLogger.LogInformation("Configuring static files...");
var publicPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "public");
if (!Directory.Exists(publicPath))
{
    Directory.CreateDirectory(publicPath);
    startupLogger.LogInformation("Created public directory at: {PublicPath}", publicPath);
}
startupLogger.LogInformation("Static files will be served from: {PublicPath} at path /public", publicPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(publicPath),
    RequestPath = "/public",
    ServeUnknownFileTypes = true, // Allow serving files with unknown extensions
    DefaultContentType = "application/octet-stream"
});

// 3b. Static files for customer license images (serve from wwwroot/customers)
var customersPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "customers");
if (!Directory.Exists(customersPath))
{
    Directory.CreateDirectory(customersPath);
    startupLogger.LogInformation("Created customers directory at: {CustomersPath}", customersPath);
}
startupLogger.LogInformation("Customer license images will be served from: {CustomersPath} at path /customers", customersPath);
startupLogger.LogInformation("ContentRootPath: {ContentRootPath}", builder.Environment.ContentRootPath);
startupLogger.LogInformation("WebRootPath: {WebRootPath}", builder.Environment.WebRootPath);
startupLogger.LogInformation("Customers path (ContentRootPath/wwwroot/customers): {CustomersPath}", customersPath);
var customersStaticFileOptions = new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(customersPath),
    RequestPath = "/customers",
    ServeUnknownFileTypes = true, // Allow serving image files
    DefaultContentType = "application/octet-stream",
    OnPrepareResponse = ctx =>
    {
        // Set proper content type for image files
        var fileExtension = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
        var contentType = fileExtension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
        ctx.Context.Response.ContentType = contentType;
        
        // Add CORS headers for images
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");
        
        startupLogger.LogInformation("Serving static file: {File} with Content-Type: {ContentType}", ctx.File.Name, contentType);
    }
};
app.UseStaticFiles(customersStaticFileOptions);

// 3c. Static files for temporary wizard license images (serve from wwwroot/wizard)
var wizardPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "wizard");
if (!Directory.Exists(wizardPath))
{
    Directory.CreateDirectory(wizardPath);
    startupLogger.LogInformation("Created wizard directory at: {WizardPath}", wizardPath);
}
startupLogger.LogInformation("Wizard license images will be served from: {WizardPath} at path /wizard", wizardPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(wizardPath),
    RequestPath = "/wizard",
    ServeUnknownFileTypes = true, // Allow serving image files
    DefaultContentType = "application/octet-stream"
});

// 3d. Static files for Azure Blob Storage containers (local fallback)
// Note: All media files are primarily stored in Azure Blob Storage
// Local files in wwwroot/{container} serve as fallback for development/local deployment

var containerConfigs = new[]
{
    new { Container = "agreements", Description = "Agreement PDFs" },
    new { Container = "companies", Description = "Company logos, banners, videos" },
    new { Container = "customer-licenses", Description = "Driver license images" },
    new { Container = "models", Description = "Vehicle model images" }
};

foreach (var config in containerConfigs)
{
    var containerPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot", config.Container);
    if (!Directory.Exists(containerPath))
    {
        Directory.CreateDirectory(containerPath);
        startupLogger.LogInformation("Created {Container} directory at: {ContainerPath}", config.Container, containerPath);
    }
    startupLogger.LogInformation("{Description} will be served from: {ContainerPath} at path /{Container} (fallback)",
        config.Description, containerPath, config.Container);

    // Configure static files for this container
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(containerPath),
        RequestPath = $"/{config.Container}",
        ServeUnknownFileTypes = true, // Allow serving all file types
        DefaultContentType = "application/octet-stream",
        OnPrepareResponse = ctx =>
        {
            // Set appropriate content type based on file extension
            var ext = Path.GetExtension(ctx.File.Name).ToLowerInvariant();
            var contentType = ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".svg" => "image/svg+xml",
                ".pdf" => "application/pdf",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                ".mov" => "video/quicktime",
                ".avi" => "video/x-msvideo",
                _ => "application/octet-stream"
            };

            ctx.Context.Response.Headers.Append("Content-Type", contentType);
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
            ctx.Context.Response.Headers.Append("Access-Control-Allow-Methods", "GET, HEAD, OPTIONS");

            // Cache for 1 hour
            ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=3600");

            startupLogger.LogInformation("Serving static file: {File} as {ContentType}", ctx.File.Name, contentType);
        }
    });
}

// 4. CORS (before authentication)
startupLogger.LogInformation("Configuring CORS...");
app.UseCors("AllowAll");

// 5. Routing (after static files, before authentication)
startupLogger.LogInformation("Configuring routing...");
app.UseRouting();

// 6. Authentication & Authorization
startupLogger.LogInformation("Configuring authentication and authorization...");
app.UseAuthentication();
app.UseAuthorization();

// 7. Company Middleware - must come after Authentication/Authorization
// This middleware resolves company from domain/subdomain and sets it in HttpContext
startupLogger.LogInformation("Adding company middleware...");
app.UseCompanyMiddleware();

// 7. Swagger (enabled for all environments for debugging)
try
{
    startupLogger.LogInformation("Configuring Swagger (Environment: {Environment})...", app.Environment.EnvironmentName);
    
    // Add middleware to catch Swagger generation errors
    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/swagger/v1/swagger.json"))
        {
            try
            {
                await next();
            }
            catch (Exception ex)
            {
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(ex, "Error generating Swagger JSON: {Message}", ex.Message);
                logger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
                
                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new
                {
                    error = "Failed to generate Swagger document",
                    message = ex.Message,
                    details = app.Environment.IsDevelopment() ? ex.ToString() : "See server logs for details"
                }));
            }
        }
        else
        {
            await next();
        }
    });
    
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Car Rental API v1");
        c.RoutePrefix = "swagger"; // Access at /swagger
        c.DocumentTitle = "Car Rental API Documentation";
    });
    startupLogger.LogInformation("Swagger configured successfully");
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "Swagger failed to initialize: {Message}", ex.Message);
    startupLogger.LogError(ex, "Stack trace: {StackTrace}", ex.StackTrace);
    // Don't throw - allow app to continue without Swagger
}

// 8. Health Checks (before controllers, for Azure probes)
startupLogger.LogInformation("Mapping health check endpoints...");
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        });
        await context.Response.WriteAsync(result);
    }
});
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    AllowCachingResponses = false, // Don't cache health check responses
    // Always return 200 if app is running (database is lazy-loaded, don't fail on DB timeout)
    ResultStatusCodes = new Dictionary<HealthStatus, int>
    {
        { HealthStatus.Healthy, 200 },
        { HealthStatus.Degraded, 200 }, // Return 200 even if degraded
        { HealthStatus.Unhealthy, 200 } // Return 200 even if unhealthy - app is still running
    },
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = "Healthy", // Always return Healthy for ready check - app is running
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                exception = e.Value.Exception?.Message
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// 9. Map Controllers (always last)
startupLogger.LogInformation("Mapping controllers...");
// 9. Map controllers (must be after routing)
app.MapControllers();
startupLogger.LogInformation("HTTP request pipeline configuration complete");

// Test database connection before starting the server (non-blocking)
startupLogger.LogInformation("Starting database connection test (non-blocking)...");
_ = Task.Run(async () =>
{
    try
    {
        startupLogger.LogInformation("[Database] Testing database connection...");
        await Task.Delay(2000); // Brief delay to ensure services are ready
        using (var scope = app.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CarRentalDbContext>();
            var canConnect = await dbContext.Database.CanConnectAsync();
            if (canConnect)
            {
            }
            else
            {
                startupLogger.LogWarning("[Database] Connection test returned false");
            }
        }
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "[Database] Connection test failed: {Message}", ex.Message);
        // Don't throw - let the app try to start anyway
    }
});

try
{
    // Log port information for Azure App Service debugging
    // Note: ASP.NET Core automatically uses the PORT environment variable in Azure App Service
    // We don't need to manually configure URLs - Kestrel handles this automatically
    var port = Environment.GetEnvironmentVariable("PORT");
    var urls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    
    Console.WriteLine("[Program] About to start Kestrel...");
    Console.WriteLine($"[Program] PORT: {port ?? "Not set"}");
    Console.WriteLine($"[Program] ASPNETCORE_URLS: {urls ?? "Not set"}");

    // Diagnostic info for Azure SkiaSharp issues
    var runtime = Environment.OSVersion;
    var architecture = Environment.ProcessorCount;
    Console.WriteLine($"[Program] Runtime: {runtime}");
    Console.WriteLine($"[Program] Processor count: {architecture}");
    Console.WriteLine($"[Program] Is64BitProcess: {Environment.Is64BitProcess}");
    Console.WriteLine($"[Program] Is64BitOperatingSystem: {Environment.Is64BitOperatingSystem}");
    
    startupLogger.LogInformation("Starting Kestrel web server...");
    startupLogger.LogInformation("PORT environment variable: {Port}", port ?? "Not set");
    startupLogger.LogInformation("ASPNETCORE_URLS environment variable: {Urls}", urls ?? "Not set");
    startupLogger.LogInformation("Application is ready to accept requests");
    
    Console.WriteLine("[Program] Calling app.Run()...");
    app.Run();
}
catch (Exception ex)
{
    // Write to console in case logger fails
    Console.WriteLine($"[Program] CRITICAL ERROR: {ex.Message}");
    Console.WriteLine($"[Program] Stack trace: {ex.StackTrace}");
    
    startupLogger.LogCritical(ex, "Application failed to start. Error: {Message}", ex.Message);
    startupLogger.LogCritical(ex, "Stack trace: {StackTrace}", ex.StackTrace);
    throw;
}