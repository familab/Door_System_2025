using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Email/domain whitelist evaluation. Direct port of Python auth.is_email_whitelisted,
/// including support for "*.suffix" and ".suffix" domain patterns.
/// </summary>
public sealed class AccessRulesService : IAccessRulesService
{
    private readonly IOptionsMonitor<DoorOptions> _options;

    public AccessRulesService(IOptionsMonitor<DoorOptions> options)
    {
        _options = options;
    }

    public bool IsEmailAllowed(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        email = email.Trim().ToLowerInvariant();
        var auth = _options.CurrentValue.Auth;
        var allowedEmails = Normalize(auth.WhitelistEmails);
        var allowedDomains = Normalize(auth.WhitelistDomains);

        // No whitelist configured => allow any verified email (matches Python).
        if (allowedEmails.Count == 0 && allowedDomains.Count == 0)
        {
            return true;
        }

        if (allowedEmails.Any(e => string.Equals(e, email, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var at = email.IndexOf('@');
        if (at < 0)
        {
            return false;
        }

        var domain = email[(at + 1)..].ToLowerInvariant();
        foreach (var raw in allowedDomains)
        {
            var entry = raw.ToLowerInvariant();
            if (entry.Length == 0)
            {
                continue;
            }

            if (entry.StartsWith("*.", StringComparison.Ordinal))
            {
                var suffix = entry[2..];
                if (domain == suffix || domain.EndsWith("." + suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (entry.StartsWith('.'))
            {
                var suffix = entry[1..];
                if (domain == suffix || domain.EndsWith("." + suffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            else if (domain == entry)
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Normalize(IEnumerable<string>? values) =>
        values is null
            ? new List<string>()
            : values.Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0).ToList();
}
