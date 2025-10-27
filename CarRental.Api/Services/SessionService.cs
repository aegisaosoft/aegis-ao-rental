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

using System.Security.Claims;

namespace CarRental.Api.Services;

public interface ISessionService
{
    void SetCustomerSession(string token, CustomerSession session);
    CustomerSession? GetCustomerSession(string token);
    void RemoveCustomerSession(string token);
    void ClearExpiredSessions();
}

public class CustomerSession
{
    public string CustomerId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public DateTime LoginTime { get; set; }
    public DateTime LastActivity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SessionService : ISessionService
{
    private readonly Dictionary<string, CustomerSession> _sessions = new();
    private readonly ILogger<SessionService> _logger;
    private readonly Timer _cleanupTimer;

    public SessionService(ILogger<SessionService> logger)
    {
        _logger = logger;
        // Clean up expired sessions every 5 minutes
        _cleanupTimer = new Timer(ClearExpiredSessions, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }

    public void SetCustomerSession(string token, CustomerSession session)
    {
        _sessions[token] = session;
        _logger.LogInformation("Session created for customer {CustomerId} with role {Role}", 
            session.CustomerId, session.Role);
    }

    public CustomerSession? GetCustomerSession(string token)
    {
        if (_sessions.TryGetValue(token, out var session))
        {
            session.LastActivity = DateTime.UtcNow;
            return session;
        }
        return null;
    }

    public void RemoveCustomerSession(string token)
    {
        if (_sessions.Remove(token))
        {
            _logger.LogInformation("Session removed for token");
        }
    }

    public void ClearExpiredSessions()
    {
        var expiredTokens = _sessions
            .Where(kvp => DateTime.UtcNow - kvp.Value.LastActivity > TimeSpan.FromHours(24))
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var token in expiredTokens)
        {
            _sessions.Remove(token);
        }

        if (expiredTokens.Count > 0)
        {
            _logger.LogInformation("Cleared {Count} expired sessions", expiredTokens.Count);
        }
    }

    public void ClearExpiredSessions(object? state)
    {
        ClearExpiredSessions();
    }
}
