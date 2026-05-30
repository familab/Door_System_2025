using System.Net;
using Xunit;

namespace DoorApp.Familab.Tests.ApiTests;

public class PublicEndpointTests : IClassFixture<DoorWebApplicationFactory>
{
    private readonly DoorWebApplicationFactory _factory;

    public PublicEndpointTests(DoorWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_page_is_public_and_renders()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("Door Controller Health Status", body);
        Assert.Contains("CLOSED/LOCKED", body);
    }

    [Fact]
    public async Task Display_page_is_public()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/display");
        response.EnsureSuccessStatusCode();
        Assert.Contains("Door Controller Health Status", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Api_health_returns_json_status()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"doorIsOpen\":false", json);
        Assert.Contains("\"version\"", json);
    }

    [Fact]
    public async Task Root_redirects_to_health()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/health", response.Headers.Location?.OriginalString);
    }
}
