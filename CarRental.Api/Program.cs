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
using System.Linq;
using System.Collections.Generic;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using CarRental.Api.Data;
using CarRental.Api.Services;
using CarRental.Api.Filters;
using CarRental.Api.Middleware;
using CarRental.Api.Extensions;
using CarRental.Api.HostedServices;

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
        
        logger.LogInformation("[Database] Database context configured successfully. CommandTimeout: {Timeout}s, RetryOnFailure: {RetryEnabled}, MaxRetryCount: {MaxRetry}", 
            dbSettings.CommandTimeout, dbSettings.EnableRetryOnFailure, dbSettings.MaxRetryCount);
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

// Add Encryption Service
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();

// Add Settings Service
builder.Services.AddScoped<ISettingsService, SettingsService>();

// Add Translation Service
builder.Services.AddScoped<ITranslationService, GoogleTranslationService>();

// Add Company Management Service
builder.Services.AddScoped<ICompanyManagementService, CompanyManagementService>();

// Add Company Service for domain-based multi-tenancy
builder.Services.AddScoped<ICompanyService, CompanyService>();

// Add Memory Cache for company domain mapping
builder.Services.AddMemoryCache();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("startup", () => HealthCheckResult.Healthy("Application is running"), tags: new[] { "ready" })
    .AddDbContextCheck<CarRentalDbContext>(tags: new[] { "ready" });

// Add Email Service
builder.Services.AddScoped<IEmailService, EmailService>();

// Add Email Template Service
builder.Services.AddScoped<IEmailTemplateService, EmailTemplateService>();

// Add background services
builder.Services.AddHostedService<SecurityDepositCollectionService>();

// Add JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"] ?? "e8Xgin/OtynoYVm8o7jiNjB9/Fke1Q6RxjH3hJsRpTE=";
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
        
        // Add event handlers to log authentication failures
        options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Authentication failed: {Error}", context.Exception.Message);
                logger.LogWarning("Exception type: {ExceptionType}", context.Exception.GetType().Name);
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
                    logger.LogWarning("Token issuer is invalid");
                }
                else if (context.Exception is Microsoft.IdentityModel.Tokens.SecurityTokenInvalidAudienceException)
                {
                    logger.LogWarning("Token audience is invalid");
                }
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogWarning("JWT Challenge triggered: {Error}, {ErrorDescription}", context.Error, context.ErrorDescription);
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
                "https://miamilifecars.aegis-rental.com",
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

// Register HttpClient factory
builder.Services.AddHttpClient();

var app = builder.Build();

// Log environment information early
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
startupLogger.LogInformation("=== Application Startup ===");
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

// 4. CORS (before authentication)
startupLogger.LogInformation("Configuring CORS...");
app.UseCors("AllowAll");

// 5. Authentication & Authorization
startupLogger.LogInformation("Configuring authentication and authorization...");
app.UseAuthentication();
app.UseAuthorization();

// 6. Company Middleware - must come after Authentication/Authorization
// This middleware resolves company from domain/subdomain and sets it in HttpContext
startupLogger.LogInformation("Adding company middleware...");
app.UseCompanyMiddleware();

// 7. Swagger (enabled in all environments for API documentation)
try
{
    startupLogger.LogInformation("Configuring Swagger...");
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
    startupLogger.LogWarning(ex, "Swagger failed to initialize");
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
    // Allow the service to be ready even if database check fails (database is lazy-loaded)
    ResultStatusCodes = new Dictionary<HealthStatus, int>
    {
        { HealthStatus.Healthy, 200 },
        { HealthStatus.Degraded, 200 }, // Return 200 even if degraded (database might be temporarily unavailable)
        { HealthStatus.Unhealthy, 503 } // Only return 503 if truly unhealthy
    },
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
startupLogger.LogInformation("Health check endpoints mapped successfully");

// 9. Map Controllers (always last)
startupLogger.LogInformation("Mapping controllers...");
app.MapControllers();
startupLogger.LogInformation("Controllers mapped successfully");
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
                startupLogger.LogInformation("[Database] Connection test successful");
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
    startupLogger.LogInformation("Starting Kestrel web server...");
    startupLogger.LogInformation("Application is ready to accept requests");
    app.Run();
}
catch (Exception ex)
{
    startupLogger.LogCritical(ex, "Application failed to start. Error: {Message}", ex.Message);
    throw;
}