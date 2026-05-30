using System.Security.Claims;
using System.Text;
using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using DoorApp.Familab.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Api;

/// <summary>Maps the public, auth and admin HTTP endpoints onto the web application.</summary>
internal static class DoorEndpoints
{
    public static void MapDoorEndpoints(this WebApplication app)
    {
        MapPublic(app);
        MapAuth(app);
        MapAdmin(app);
    }

    private static void MapPublic(WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/health"));

        app.MapGet("/health", (IHealthService health) =>
            Results.Content(HtmlTemplates.HealthPage(health.GetSnapshot(), isDisplay: false), "text/html; charset=utf-8"));

        app.MapGet("/display", (IHealthService health) =>
            Results.Content(HtmlTemplates.HealthPage(health.GetSnapshot(), isDisplay: true), "text/html; charset=utf-8"));

        // Machine-readable health (bonus over the Python HTML-only page).
        app.MapGet("/api/health", (IHealthService health) =>
        {
            var s = health.GetSnapshot();
            return Results.Ok(new
            {
                version = s.Version,
                now = s.Now,
                machine = s.MachineName,
                door = s.Door.DisplayStatus,
                doorIsOpen = s.Door.IsOpen,
                uptimeSeconds = s.UptimeSeconds,
                lastNfcSuccess = s.LastNfcSuccess,
                lastNfcError = s.LastNfcError,
                disk = s.Disk
            });
        });
    }

    private static void MapAuth(WebApplication app)
    {
        app.MapGet("/login", (HttpContext ctx, IOptions<DoorOptions> options, string? next, string? error) =>
        {
            var nextPath = SanitizeNext(next);
            var googleEnabled = options.Value.Auth.Google.Enabled
                                && !string.IsNullOrEmpty(options.Value.Auth.Google.ClientId);
            return Results.Content(HtmlTemplates.LoginPage(error, nextPath, googleEnabled), "text/html; charset=utf-8");
        });

        app.MapPost("/login", async (HttpContext ctx, IOptions<DoorOptions> options, AuthThrottle throttle) =>
        {
            var ip = ClientIp(ctx);
            var form = await ctx.Request.ReadFormAsync();
            var username = form["username"].ToString();
            var password = form["password"].ToString();
            var nextPath = SanitizeNext(form["next"].ToString());

            if (throttle.IsThrottled(ip))
            {
                return Results.Content(
                    HtmlTemplates.LoginPage("Too many failed attempts. Please wait before trying again.", nextPath,
                        options.Value.Auth.Google.Enabled),
                    "text/html; charset=utf-8");
            }

            var auth = options.Value.Auth;
            var ok = string.Equals(username, auth.MasterUsername, StringComparison.Ordinal)
                     && MasterPasswordHasher.Verify(password, auth.MasterPasswordHash);

            if (!ok)
            {
                throttle.RecordFailure(ip);
                return Results.Content(
                    HtmlTemplates.LoginPage("Invalid username or password", nextPath, auth.Google.Enabled),
                    "text/html; charset=utf-8");
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, username),
                new(ClaimTypes.Email, username),
                new("auth_method", "master_password")
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return Results.Redirect(nextPath);
        });

        app.MapGet("/login/google", (string? next, IOptions<DoorOptions> options) =>
        {
            if (!options.Value.Auth.Google.Enabled)
            {
                return Results.Redirect("/login?error=" + Uri.EscapeDataString("Google OAuth is not enabled"));
            }

            var props = new AuthenticationProperties { RedirectUri = SanitizeNext(next) };
            return Results.Challenge(props, new[] { GoogleDefaults.AuthenticationScheme });
        });

        app.MapGet("/logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect("/login");
        });
    }

    private static void MapAdmin(WebApplication app)
    {
        var admin = app.MapGroup("/admin").RequireAuthorization();

        admin.MapGet("", (HttpContext ctx, IHealthService health) =>
            Results.Content(HtmlTemplates.AdminPage(health.GetSnapshot(), CurrentEmail(ctx)), "text/html; charset=utf-8"));

        admin.MapGet("/state", (IDoorControlService door) =>
        {
            var state = door.State;
            return Results.Ok(new { isOpen = state.IsOpen, status = state.DisplayStatus, updatedAt = state.UpdatedAt });
        });

        admin.MapPost("/door/toggle", async (HttpContext ctx, IDoorControlService door) =>
        {
            var badgeId = AuditIdentity(ctx);
            var newState = await door.ToggleAsync(badgeId);
            return Results.Ok(new { success = true, state = newState, message = $"Door is now {newState}" });
        });

        admin.MapPost("/door/open", async (HttpContext ctx, IDoorControlService door, IActionLogService log) =>
        {
            var badgeId = AuditIdentity(ctx);
            await log.RecordAsync("Manual Unlock (1 hour)", badgeId);
            await door.UnlockAsync(null, badgeId);
            return Results.Ok(new { success = true, state = "unlocked", message = "Door unlocked" });
        });

        admin.MapPost("/door/close", async (HttpContext ctx, IDoorControlService door, IActionLogService log) =>
        {
            var badgeId = AuditIdentity(ctx);
            await log.RecordAsync("Manual Lock", badgeId);
            await door.LockAsync(badgeId);
            return Results.Ok(new { success = true, state = "locked", message = "Door locked" });
        });

        admin.MapGet("/analytics", async (HttpContext ctx, IAnalyticsService analytics, string? start, string? end) =>
        {
            var (from, to) = ResolveRange(start, end);
            var summary = await analytics.SummariseAsync(from, to);
            var events = await analytics.GetEventsAsync(from, to);
            return Results.Content(HtmlTemplates.AnalyticsPage(summary, events, CurrentEmail(ctx)), "text/html; charset=utf-8");
        });

        admin.MapGet("/analytics.csv", async (IAnalyticsService analytics, string? start, string? end) =>
        {
            var (from, to) = ResolveRange(start, end);
            var events = await analytics.GetEventsAsync(from, to);
            var csv = BuildCsv(events);
            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", $"metrics-{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.csv");
        });
    }

    // ----- helpers -------------------------------------------------------

    private static (DateTimeOffset from, DateTimeOffset to) ResolveRange(string? start, string? end)
    {
        var now = DateTimeOffset.Now;
        var defaultStart = new DateTimeOffset(now.Year, 1, 1, 0, 0, 0, now.Offset);
        var from = DateTimeOffset.TryParse(start, out var s) ? s : defaultStart;
        var to = DateTimeOffset.TryParse(end, out var e)
            ? new DateTimeOffset(e.Year, e.Month, e.Day, 23, 59, 59, e.Offset)
            : new DateTimeOffset(now.Year, now.Month, now.Day, 23, 59, 59, now.Offset);
        if (from > to)
        {
            (from, to) = (to, from);
        }
        return (from, to);
    }

    private static string BuildCsv(IReadOnlyList<AccessEvent> events)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ts,event_type,badge_id,status,raw_message");
        foreach (var e in events)
        {
            sb.Append(Csv(e.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"))).Append(',')
              .Append(Csv(e.EventType)).Append(',')
              .Append(Csv(e.BadgeId ?? string.Empty)).Append(',')
              .Append(Csv(e.Status)).Append(',')
              .Append(Csv(e.RawMessage)).Append('\n');
        }
        return sb.ToString();
    }

    private static string Csv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }

    private static string SanitizeNext(string? next)
    {
        if (string.IsNullOrEmpty(next) || !next.StartsWith('/') || next.StartsWith("//", StringComparison.Ordinal))
        {
            return "/admin";
        }
        return next;
    }

    private static string? CurrentEmail(HttpContext ctx) =>
        ctx.User?.FindFirstValue(ClaimTypes.Email) ?? ctx.User?.Identity?.Name;

    private static string ClientIp(HttpContext ctx)
    {
        var xff = ctx.Request.Headers["X-Forwarded-For"].ToString();
        if (!string.IsNullOrEmpty(xff))
        {
            return xff.Split(',')[0].Trim();
        }
        return ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    /// <summary>Build a structured audit identity string for manual actions (user/ip).</summary>
    private static string AuditIdentity(HttpContext ctx)
    {
        var parts = new List<string>();
        var email = CurrentEmail(ctx);
        if (!string.IsNullOrEmpty(email))
        {
            parts.Add($"user={email}");
        }
        parts.Add($"client={ClientIp(ctx)}");
        return string.Join(";", parts);
    }
}
