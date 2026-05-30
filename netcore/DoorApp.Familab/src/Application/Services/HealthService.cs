using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using DoorApp.Familab.Application.Abstractions;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using DoorApp.Familab.Domain.Models;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Application.Services;

/// <summary>
/// Assembles the health snapshot. Ports the value collection done in the Python
/// server.state module (uptime, local IPs, disk space) and routes_public.
/// </summary>
public sealed class HealthService : IHealthService
{
    private readonly IDoorControlService _door;
    private readonly IRuntimeStatus _status;
    private readonly IVersionProvider _version;
    private readonly ISystemClock _clock;
    private readonly IOptionsMonitor<DoorOptions> _options;

    public HealthService(
        IDoorControlService door,
        IRuntimeStatus status,
        IVersionProvider version,
        ISystemClock clock,
        IOptionsMonitor<DoorOptions> options)
    {
        _door = door;
        _status = status;
        _version = version;
        _clock = clock;
        _options = options;
    }

    public HealthSnapshot GetSnapshot()
    {
        var uptime = _clock.Now - _status.StartedAt;
        return new HealthSnapshot
        {
            Version = _version.Version,
            Now = _clock.Now,
            MachineName = Environment.MachineName,
            LocalIps = GetLocalIps(),
            Door = _door.State,
            Uptime = FormatUptime(uptime),
            UptimeSeconds = (long)uptime.TotalSeconds,
            LastNfcSuccess = _status.LastNfcSuccess,
            LastNfcError = _status.LastNfcError,
            LastDataConnection = _status.LastDataConnection,
            LastBadgeRefresh = _status.LastBadgeRefresh,
            LastStorageError = _status.LastStorageError,
            Disk = GetDiskSpace(),
            RefreshIntervalSeconds = _options.CurrentValue.Health.RefreshIntervalSeconds
        };
    }

    /// <summary>Formats a duration as "1d 2h 3m 4s" (matches Python get_uptime).</summary>
    public static string FormatUptime(TimeSpan uptime)
    {
        var parts = new List<string>();
        if (uptime.Days > 0) parts.Add($"{uptime.Days}d");
        if (uptime.Hours > 0) parts.Add($"{uptime.Hours}h");
        if (uptime.Minutes > 0) parts.Add($"{uptime.Minutes}m");
        parts.Add($"{uptime.Seconds}s");
        return string.Join(" ", parts);
    }

    private static IReadOnlyList<string> GetLocalIps()
    {
        var ips = new SortedSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                foreach (var addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        continue;
                    }

                    var ip = addr.Address.ToString();
                    // Exclude loopback and Docker-style 172.* (matches Python filter).
                    if (!ip.StartsWith("127.", StringComparison.Ordinal) && !ip.StartsWith("172.", StringComparison.Ordinal))
                    {
                        ips.Add(ip);
                    }
                }
            }
        }
        catch
        {
            // Best effort, same as Python.
        }

        return ips.ToList();
    }

    private static DiskSpace GetDiskSpace()
    {
        try
        {
            var root = Path.GetPathRoot(AppContext.BaseDirectory) ?? "/";
            var drive = new DriveInfo(root);
            double totalMb = drive.TotalSize / (1024d * 1024d);
            double freeMb = drive.AvailableFreeSpace / (1024d * 1024d);
            double usedMb = totalMb - freeMb;
            double pct = totalMb > 0 ? usedMb / totalMb * 100d : 0d;
            return new DiskSpace(freeMb, totalMb, usedMb, pct);
        }
        catch
        {
            return new DiskSpace(0, 0, 0, 0);
        }
    }
}
