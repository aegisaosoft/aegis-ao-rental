using CarRental.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace CarRental.Api.Services;

public interface ISettingsService
{
    Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default);
    Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default);
    Task<bool> HasValueAsync(string key, CancellationToken cancellationToken = default);
}

public class SettingsService : ISettingsService
{
    private static readonly HashSet<string> EncryptedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "stripe.secretKey",
        "stripe.publishableKey",
        "stripe.webhookSecret",
        "azure.clientSecret"
    };

    private readonly CarRentalDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<SettingsService> _logger;

    public SettingsService(CarRentalDbContext context, IEncryptionService encryptionService, ILogger<SettingsService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task<string?> GetValueAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var setting = await _context.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (setting == null)
            {
                return null;
            }

            if (ShouldEncrypt(key))
            {
                try
                {
                    return _encryptionService.Decrypt(setting.Value);
                }
                catch (Exception ex) when (ex is FormatException || ex is CryptographicException)
                {
                    _logger.LogWarning(ex, "Setting {Key} appears to be stored in plaintext. Re-encrypting.", key);
                    try
                    {
                        await SetValueAsync(key, setting.Value, cancellationToken);
                    }
                    catch (Exception innerEx)
                    {
                        _logger.LogError(innerEx, "Failed to re-encrypt plaintext setting {Key}", key);
                    }
                    return setting.Value;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to decrypt setting {Key}", key);
                    return null;
                }
            }

            return setting.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read setting {Key}", key);
            throw;
        }
    }

    public async Task SetValueAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        try
        {
            var storedValue = value;
            if (!string.IsNullOrWhiteSpace(storedValue) && ShouldEncrypt(key))
            {
                storedValue = _encryptionService.Encrypt(storedValue.Trim());
            }

            var existing = await _context.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (existing != null)
                {
                    _context.Settings.Remove(existing);
                    await _context.SaveChangesAsync(cancellationToken);
                }
                return;
            }

            if (existing == null)
            {
                var setting = new Models.Setting
                {
                    Key = key,
                    Value = storedValue!,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.Settings.Add(setting);
            }
            else
            {
                existing.Value = storedValue!;
                existing.UpdatedAt = DateTime.UtcNow;
                _context.Settings.Update(existing);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set setting {Key}", key);
            throw;
        }
    }

    public async Task<bool> HasValueAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await GetValueAsync(key, cancellationToken);
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool ShouldEncrypt(string key) => EncryptedKeys.Contains(key);
}
