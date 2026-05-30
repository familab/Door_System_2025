namespace DoorApp.Familab.Application.Abstractions;

/// <summary>
/// Access policy rules. Currently governs which Google accounts may sign in to the
/// admin area (email/domain whitelist), ported from Python auth.is_email_whitelisted.
/// </summary>
public interface IAccessRulesService
{
    /// <summary>
    /// Returns true if the email is allowed admin access. When no whitelist is
    /// configured, all verified emails are allowed (matches Python behaviour).
    /// </summary>
    bool IsEmailAllowed(string email);
}
