using System.Collections.Concurrent;
using System.Security.Cryptography;
using Custom5v5.Api.Interfaces;
using Microsoft.Extensions.Configuration;

namespace Custom5v5.Api.Services;

public sealed class OAuthStateStore : IOAuthStateStore
{
    private readonly ConcurrentDictionary<string, StateEntry> _states = new();
    private readonly TimeSpan _ttl;

    public OAuthStateStore(IConfiguration config)
    {
        // configurable, default 5 minutes
        var minutes = config.GetValue<int?>("Auth:Discord:StateTtlMinutes") ?? 5;
        _ttl = TimeSpan.FromMinutes(Math.Clamp(minutes, 1, 60));
    }

    public string CreateState(string? returnUrl)
    {
        CleanupExpiredIfNeeded();

        var state = GenerateSecureTokenUrlSafe(32);
        var entry = new StateEntry(returnUrl, DateTime.UtcNow.Add(_ttl));

        _states[state] = entry;
        return state;
    }

    public OAuthStateData? ConsumeState(string state)
    {
        CleanupExpiredIfNeeded();

        if (!_states.TryRemove(state, out var entry))
            return null;

        if (entry.ExpiresAtUtc <= DateTime.UtcNow)
            return null;

        return new OAuthStateData(entry.ReturnUrl);
    }

    private int _cleanupCounter = 0;

    private void CleanupExpiredIfNeeded()
    {
        // cheap periodic cleanup (every ~100 calls)
        if (Interlocked.Increment(ref _cleanupCounter) % 100 != 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var (key, entry) in _states)
        {
            if (entry.ExpiresAtUtc <= now)
                _states.TryRemove(key, out _);
        }
    }

    private static string GenerateSecureTokenUrlSafe(int bytesLength)
    {
        var bytes = RandomNumberGenerator.GetBytes(bytesLength);
        // Base64Url without padding
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private sealed record StateEntry(string? ReturnUrl, DateTime ExpiresAtUtc);
}