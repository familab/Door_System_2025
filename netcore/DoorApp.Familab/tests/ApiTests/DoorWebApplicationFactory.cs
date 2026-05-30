using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace DoorApp.Familab.Tests.ApiTests;

/// <summary>
/// WebApplicationFactory that points storage at a throwaway temp SQLite file and
/// forces the hardware stubs, so API tests run without a Raspberry Pi.
/// </summary>
public sealed class DoorWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"doorapp-api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Door:Hardware:UseRealHardware"] = "false",
                ["Door:Storage:Provider"] = "Sqlite",
                ["Door:Storage:SqlitePath"] = _dbPath,
                ["Door:Auth:MasterUsername"] = "admin",
                // pbkdf2 of "changeme"
                ["Door:Auth:MasterPasswordHash"] =
                    "pbkdf2_sha256$100000$ABEiM0RVZneImqu8zd7v8A==$sb87PE5SVx+GvGG9tirFWcus4aBq3U/HPcKV5H66298=",
                ["Door:Auth:Google:Enabled"] = "false"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try { if (File.Exists(_dbPath)) File.Delete(_dbPath); } catch { /* best effort */ }
        }
    }
}
