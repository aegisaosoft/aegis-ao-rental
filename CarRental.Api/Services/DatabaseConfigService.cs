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

namespace CarRental.Api.Services;

public interface IDatabaseConfigService
{
    string GetConnectionString();
    DatabaseSettings GetDatabaseSettings();
}

public class DatabaseConfigService : IDatabaseConfigService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseConfigService> _logger;

    public DatabaseConfigService(IConfiguration configuration, ILogger<DatabaseConfigService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public string GetConnectionString()
    {
        var databaseConfig = _configuration.GetSection("Database");
        
        var host = databaseConfig["Host"] ?? "localhost";
        var port = databaseConfig["Port"] ?? "5432";
        var database = databaseConfig["Database"] ?? "car_rental";
        var username = databaseConfig["Username"] ?? "postgres";
        var password = databaseConfig["Password"] ?? "password";
        var pooling = databaseConfig["Pooling"] ?? "true";
        var minPoolSize = databaseConfig["MinPoolSize"] ?? "0";
        var maxPoolSize = databaseConfig["MaxPoolSize"] ?? "100";
        var connectionLifetime = databaseConfig["ConnectionLifetime"] ?? "0";
        var sslMode = databaseConfig["SSLMode"] ?? "Prefer";

        var connectionString = $"Host={host};Port={port};Database={database};Username={username};Password={password};Pooling={pooling};MinPoolSize={minPoolSize};MaxPoolSize={maxPoolSize};ConnectionLifetime={connectionLifetime};SSL Mode={sslMode};";

        _logger.LogInformation("Generated connection string for database: {Database} on host: {Host}", database, host);
        return connectionString;
    }

    public DatabaseSettings GetDatabaseSettings()
    {
        var settings = new DatabaseSettings();
        _configuration.GetSection("DatabaseSettings").Bind(settings);
        return settings;
    }
}

public class DatabaseSettings
{
    public string Provider { get; set; } = "PostgreSQL";
    public int CommandTimeout { get; set; } = 30;
    public bool EnableSensitiveDataLogging { get; set; } = false;
    public bool EnableDetailedErrors { get; set; } = false;
    public bool EnableRetryOnFailure { get; set; } = true;
    public int MaxRetryCount { get; set; } = 3;
    public string MaxRetryDelay { get; set; } = "00:00:30";
    public bool EnableServiceProviderCaching { get; set; } = true;
    public string EnableQuerySplittingBehavior { get; set; } = "SplitQuery";
}
