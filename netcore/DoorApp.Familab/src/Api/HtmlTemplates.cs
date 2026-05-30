using System.Net;
using System.Text;
using DoorApp.Familab.Domain.Models;

namespace DoorApp.Familab.Api;

/// <summary>
/// Server-rendered HTML pages. Mirrors the dark-themed pages produced by the Python
/// routes_public / routes_admin / routes_metrics / routes_auth modules.
/// </summary>
internal static class HtmlTemplates
{
    private const string Style = """
        <style>
            body { font-family: monospace; margin: 20px; background: #1e1e1e; color: #d4d4d4; }
            h1, h2 { color: #4ec9b0; }
            a { color: #9cdcfe; text-decoration: none; }
            a:hover { text-decoration: underline; }
            table { border-collapse: collapse; width: 100%; max-width: 960px; margin-top: 8px; }
            th, td { border: 1px solid #555; padding: 8px 10px; text-align: left; }
            th { background: #2d2d30; color: #4ec9b0; }
            tr:nth-child(even) { background: #252526; }
            .status-ok { color: #4ec9b0; font-weight: bold; }
            .status-warning { color: #dcdcaa; font-weight: bold; }
            .status-error { color: #f48771; font-weight: bold; }
            .timestamp { color: #9cdcfe; }
            .card { max-width: 460px; background: #252526; border: 1px solid #555; border-radius: 8px; padding: 16px; }
            label { display: block; margin-top: 10px; }
            input { width: 100%; background: #1e1e1e; color: #d4d4d4; border: 1px solid #555; padding: 8px; border-radius: 4px; box-sizing: border-box; }
            .btn { display: inline-block; margin-top: 12px; background:#4ec9b0; color:#1e1e1e; padding:8px 12px; border:none; border-radius:4px; cursor:pointer; text-decoration: none; }
            .btn.google { background: #9cdcfe; }
            .btn.warn { background: #dcdcaa; }
            .error { margin-top: 10px; padding: 8px; background: #4a2d2d; color: #f48771; border-radius: 4px; }
            .note { margin-top: 12px; color: #c9c9c9; }
            .toast { position: fixed; right: 20px; bottom: 20px; padding: 10px 14px; border-radius: 6px; display: none; z-index: 9999; }
            .toast.success { background: #4ec9b0; color: #1e1e1e; }
            .toast.error { background: #f48771; color: #fff; }
        </style>
        """;

    private static string Enc(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Ts(DateTimeOffset? value) =>
        value is null ? "Never" : value.Value.ToString("yyyy-MM-dd HH:mm:ss");

    public static string HealthPage(HealthSnapshot s, bool isDisplay)
    {
        var refresh = s.RefreshIntervalSeconds > 0
            ? $"<meta http-equiv=\"refresh\" content=\"{s.RefreshIntervalSeconds}\">"
            : string.Empty;
        var doorClass = s.Door.IsOpen ? "status-warning" : "status-ok";
        var nfcErr = s.LastNfcError ?? "None";
        var storageErr = s.LastStorageError ?? "None";

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\">");
        sb.Append("<title>Door Controller ").Append(isDisplay ? "Display" : "Health").Append("</title>");
        sb.Append(refresh).Append(Style).Append("</head><body>");
        sb.Append("<h1>Door Controller Health Status</h1>");
        sb.Append("<p class=\"timestamp\">Version: ").Append(Enc(s.Version)).Append("</p>");
        sb.Append("<p class=\"timestamp\">Current Date: ").Append(Ts(s.Now)).Append("</p>");
        sb.Append("<p class=\"timestamp\">Machine: ").Append(Enc(s.MachineName)).Append("</p>");
        sb.Append("<p class=\"timestamp\">Local IPs: ").Append(Enc(s.LocalIps.Count > 0 ? string.Join(", ", s.LocalIps) : "None")).Append("</p>");
        sb.Append("<table><tr><th>Metric</th><th>Value</th></tr>");
        sb.Append("<tr><td>Door Status</td><td class=\"").Append(doorClass).Append("\">").Append(s.Door.DisplayStatus).Append("</td></tr>");
        sb.Append("<tr><td>Door Status Updated</td><td>").Append(Ts(s.Door.UpdatedAt)).Append("</td></tr>");
        sb.Append("<tr><td>Application Uptime</td><td class=\"status-ok\" id=\"uptimeDisplay\" data-uptime-seconds=\"")
            .Append(s.UptimeSeconds).Append("\">").Append(Enc(s.Uptime)).Append("</td></tr>");
        sb.Append("<tr><td>Last Data Connection</td><td>").Append(Ts(s.LastDataConnection)).Append("</td></tr>");
        sb.Append("<tr><td>Last Badge Refresh</td><td>").Append(Ts(s.LastBadgeRefresh)).Append("</td></tr>");
        sb.Append("<tr><td>PN532 Last Success</td><td>").Append(Ts(s.LastNfcSuccess)).Append("</td></tr>");
        sb.Append("<tr><td>PN532 Last Error</td><td class=\"").Append(nfcErr == "None" ? "status-ok" : "status-error").Append("\">").Append(Enc(nfcErr)).Append("</td></tr>");
        sb.Append("<tr><td>Storage Last Error</td><td class=\"").Append(storageErr == "None" ? "status-ok" : "status-error").Append("\">").Append(Enc(storageErr)).Append("</td></tr>");
        sb.Append("<tr><td>Disk Free Space</td><td>")
            .Append($"{s.Disk.FreeMb:F2} MB / {s.Disk.TotalMb:F2} MB ({s.Disk.PercentUsed:F1}% used)")
            .Append("</td></tr>");
        sb.Append("</table>");
        sb.Append(UptimeScript());
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static string LoginPage(string? error, string nextPath, bool googleEnabled)
    {
        var errorHtml = string.IsNullOrEmpty(error) ? string.Empty : $"<div class=\"error\">{Enc(error)}</div>";
        var googleButton = googleEnabled
            ? $"<a class=\"btn google\" href=\"/login/google?next={Uri.EscapeDataString(nextPath)}\">Sign in with Google</a>"
            : "<div class=\"note\">Google Sign-In is not configured.</div>";

        return $$"""
            <!DOCTYPE html><html><head><meta charset="utf-8"><title>Door Controller Login</title>{{Style}}</head>
            <body><h1>Login</h1><div class="card">
            {{errorHtml}}
            <form method="POST" action="/login">
              <input type="hidden" name="next" value="{{Enc(nextPath)}}">
              <label>Username</label>
              <input name="username" autocomplete="username" required />
              <label>Password</label>
              <input name="password" type="password" autocomplete="current-password" required />
              <button class="btn" type="submit">Sign in</button>
            </form>
            <div style="margin-top:14px;">{{googleButton}}</div>
            </div></body></html>
            """;
    }

    public static string AdminPage(HealthSnapshot s, string? userEmail)
    {
        var userDisplay = string.IsNullOrEmpty(userEmail) ? string.Empty : $" ({Enc(userEmail)})";
        var doorClass = s.Door.IsOpen ? "status-warning" : "status-ok";
        var toggleLabel = s.Door.IsOpen ? "Lock Door" : "Unlock Door";

        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Admin - Door Controller</title>").Append(Style).Append("</head><body>");
        sb.Append("<h1>Admin Dashboard</h1>");
        sb.Append("<p class=\"timestamp\">Version: ").Append(Enc(s.Version))
            .Append(" &nbsp;|&nbsp; <a href=\"/health\">Health</a> &nbsp;|&nbsp; <a href=\"/display\">Display</a> &nbsp;|&nbsp; <a href=\"/admin/analytics\">Analytics</a> &nbsp;|&nbsp; <a href=\"/logout\">Logout</a>")
            .Append(userDisplay).Append("</p>");
        sb.Append("<p class=\"timestamp\">Machine: ").Append(Enc(s.MachineName))
            .Append(" &nbsp; Local IPs: ").Append(Enc(s.LocalIps.Count > 0 ? string.Join(", ", s.LocalIps) : "None")).Append("</p>");
        sb.Append("<p class=\"timestamp\"><button id=\"toggleDoorBtn\" class=\"btn warn\"><span id=\"toggleDoorLabel\">")
            .Append(toggleLabel).Append("</span></button></p>");
        sb.Append("<div id=\"toast\" class=\"toast\"></div>");
        sb.Append("<table><tr><th>Metric</th><th>Value</th></tr>");
        sb.Append("<tr><td>Door Status</td><td class=\"").Append(doorClass).Append("\" id=\"doorStatus\">").Append(s.Door.DisplayStatus).Append("</td></tr>");
        sb.Append("<tr><td>Door Status Updated</td><td>").Append(Ts(s.Door.UpdatedAt)).Append("</td></tr>");
        sb.Append("<tr><td>Application Uptime</td><td class=\"status-ok\" id=\"uptimeDisplay\" data-uptime-seconds=\"")
            .Append(s.UptimeSeconds).Append("\">").Append(Enc(s.Uptime)).Append("</td></tr>");
        sb.Append("<tr><td>PN532 Last Success</td><td>").Append(Ts(s.LastNfcSuccess)).Append("</td></tr>");
        sb.Append("<tr><td>PN532 Last Error</td><td>").Append(Enc(s.LastNfcError ?? "None")).Append("</td></tr>");
        sb.Append("<tr><td>Disk Free Space</td><td>")
            .Append($"{s.Disk.FreeMb:F2} MB / {s.Disk.TotalMb:F2} MB ({s.Disk.PercentUsed:F1}% used)").Append("</td></tr>");
        sb.Append("</table>");
        sb.Append(AdminScript());
        sb.Append(UptimeScript());
        sb.Append("</body></html>");
        return sb.ToString();
    }

    public static string AnalyticsPage(AnalyticsSummary summary, IReadOnlyList<AccessEvent> recent, string? userEmail)
    {
        var userDisplay = string.IsNullOrEmpty(userEmail) ? string.Empty : $" ({Enc(userEmail)})";
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html><head><meta charset=\"utf-8\"><title>Door Metrics</title>").Append(Style).Append("</head><body>");
        sb.Append("<h1>Door Metrics</h1>");
        sb.Append("<p><a href=\"/admin\">← Admin</a> | <a href=\"/admin/analytics.csv\">Export CSV</a> | <a href=\"/logout\">Logout</a>").Append(userDisplay).Append("</p>");
        sb.Append("<p class=\"timestamp\">Range: ").Append(Ts(summary.RangeStart)).Append(" → ").Append(Ts(summary.RangeEnd)).Append("</p>");

        sb.Append("<table><tr><th>Metric</th><th>Value</th></tr>");
        sb.Append("<tr><td>Total Badge Scans</td><td>").Append(summary.TotalScans).Append("</td></tr>");
        sb.Append("<tr><td>Granted</td><td class=\"status-ok\">").Append(summary.GrantedScans).Append("</td></tr>");
        sb.Append("<tr><td>Denied</td><td class=\"status-error\">").Append(summary.DeniedScans).Append("</td></tr>");
        sb.Append("<tr><td>Door Open Events</td><td>").Append(summary.DoorOpenCount).Append("</td></tr>");
        sb.Append("<tr><td>Manual Actions</td><td>").Append(summary.ManualActions).Append("</td></tr>");
        sb.Append("<tr><td>Errors</td><td>").Append(summary.ErrorCount).Append("</td></tr>");
        sb.Append("<tr><td>Uptime (seconds)</td><td>").Append(summary.UptimeSeconds).Append("</td></tr>");
        sb.Append("</table>");

        sb.Append("<h2>Top Badge Users</h2><table><tr><th>Badge</th><th>Granted Scans</th></tr>");
        if (summary.TopBadges.Count == 0)
        {
            sb.Append("<tr><td colspan=\"2\">(none)</td></tr>");
        }
        foreach (var b in summary.TopBadges)
        {
            sb.Append("<tr><td>").Append(Enc(b.BadgeId)).Append("</td><td>").Append(b.Count).Append("</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h2>Scans Per Day</h2><table><tr><th>Day</th><th>Scans</th></tr>");
        if (summary.ScansPerDay.Count == 0)
        {
            sb.Append("<tr><td colspan=\"2\">(none)</td></tr>");
        }
        foreach (var d in summary.ScansPerDay)
        {
            sb.Append("<tr><td>").Append(Enc(d.Day)).Append("</td><td>").Append(d.Count).Append("</td></tr>");
        }
        sb.Append("</table>");

        sb.Append("<h2>Recent Events (Latest 100)</h2><table><tr><th>Time</th><th>Event</th><th>Badge</th><th>Status</th></tr>");
        foreach (var e in recent.TakeLast(100).Reverse())
        {
            sb.Append("<tr><td>").Append(Ts(e.Timestamp)).Append("</td><td>").Append(Enc(e.EventType))
                .Append("</td><td>").Append(Enc(e.BadgeId ?? string.Empty)).Append("</td><td>").Append(Enc(e.Status)).Append("</td></tr>");
        }
        sb.Append("</table></body></html>");
        return sb.ToString();
    }

    private static string AdminScript() => """
        <script>
        (function(){
          const toastEl = document.getElementById('toast');
          function showToast(msg, kind){ toastEl.textContent = msg; toastEl.className = 'toast ' + (kind||'success'); toastEl.style.display='block'; setTimeout(()=>{toastEl.style.display='none';},3500); }
          document.getElementById('toggleDoorBtn').addEventListener('click', async function(){
            this.disabled = true;
            try {
              const r = await fetch('/admin/door/toggle', { method:'POST', credentials:'same-origin' });
              const j = await r.json();
              showToast(j.message || (r.ok?'OK':'Error'), r.ok ? 'success':'error');
              if (r.ok) setTimeout(()=>location.reload(), 600);
            } catch(e){ showToast('Request failed','error'); }
            this.disabled = false;
          });
        })();
        </script>
        """;

    private static string UptimeScript() => """
        <script>
        (function(){
          const el = document.getElementById('uptimeDisplay');
          if(!el) return;
          let secs = parseInt(el.getAttribute('data-uptime-seconds'))||0;
          function fmt(t){const d=Math.floor(t/86400),h=Math.floor((t%86400)/3600),m=Math.floor((t%3600)/60),s=t%60;const p=[];if(d>0)p.push(d+'d');if(h>0)p.push(h+'h');if(m>0)p.push(m+'m');p.push(s+'s');return p.join(' ');}
          setInterval(()=>{secs+=1; el.textContent=fmt(secs);},1000);
        })();
        </script>
        """;
}
