using System.Text.RegularExpressions;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Normalizes a raw event description into a simplified token. Direct port of
/// Python metrics_storage.normalize_event_type.
/// </summary>
public static partial class EventTypeNormalizer
{
    [GeneratedRegex(@"\(.*\)")]
    private static partial Regex ParentheticalRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"\W+")]
    private static partial Regex NonWordRegex();

    public static string Normalize(string? rawEvent)
    {
        if (rawEvent is null)
        {
            return "unknown";
        }

        var et = rawEvent.ToLowerInvariant();
        et = ParentheticalRegex().Replace(et, string.Empty).Trim();
        et = WhitespaceRegex().Replace(et, " ");

        if (et.Length == 0)
        {
            return "unknown";
        }

        if (et.Contains("manual lock")) return "manual_lock";
        if (et.Contains("manual unlock")) return "manual_unlock";
        if (et.Contains("scan") || et.Contains("badge")) return "scan";
        if (et.Contains("open") || et.Contains("unlocked")) return "open";
        if (et.Contains("close") || et.Contains("closed") || et.Contains("locked")) return "close";

        var key = NonWordRegex().Replace(et, "_").Trim('_');
        return key.Length > 0 ? key : et;
    }
}
