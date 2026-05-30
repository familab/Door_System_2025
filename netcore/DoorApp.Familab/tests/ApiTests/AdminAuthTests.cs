using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace DoorApp.Familab.Tests.ApiTests;

public class AdminAuthTests : IClassFixture<DoorWebApplicationFactory>
{
    private readonly DoorWebApplicationFactory _factory;

    public AdminAuthTests(DoorWebApplicationFactory factory) => _factory = factory;

    private HttpClient NoRedirectClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Admin_requires_authentication()
    {
        var client = NoRedirectClient();
        var response = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/login", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Wrong_master_password_is_rejected()
    {
        var client = NoRedirectClient();
        var response = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "wrong",
            ["next"] = "/admin"
        }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // re-renders login with error
        Assert.Contains("Invalid username or password", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Master_password_login_grants_admin_access()
    {
        var client = NoRedirectClient();

        var login = await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "changeme",
            ["next"] = "/admin"
        }));

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/admin", login.Headers.Location?.OriginalString);

        // The shared cookie container now carries the session cookie.
        var admin = await client.GetAsync("/admin");
        admin.EnsureSuccessStatusCode();
        Assert.Contains("Admin Dashboard", await admin.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Authenticated_user_can_toggle_door()
    {
        var client = NoRedirectClient();
        await client.PostAsync("/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = "admin",
            ["password"] = "changeme",
            ["next"] = "/admin"
        }));

        var toggle = await client.PostAsync("/admin/door/toggle", content: null);
        toggle.EnsureSuccessStatusCode();
        var body = await toggle.Content.ReadAsStringAsync();
        Assert.Contains("unlocked", body);

        var state = await client.GetAsync("/admin/state");
        Assert.Contains("\"isOpen\":true", await state.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_page_is_public()
    {
        var client = NoRedirectClient();
        var response = await client.GetAsync("/login");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Login", await response.Content.ReadAsStringAsync());
    }
}
