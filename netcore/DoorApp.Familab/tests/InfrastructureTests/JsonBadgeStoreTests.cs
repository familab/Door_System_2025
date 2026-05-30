using DoorApp.Familab.Infrastructure.Storage;
using Xunit;

namespace DoorApp.Familab.Tests.InfrastructureTests;

public sealed class JsonBadgeStoreTests : IDisposable
{
    private readonly string _path;

    public JsonBadgeStoreTests()
    {
        _path = Path.Combine(Path.GetTempPath(), $"doorapp-badges-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public async Task Add_persists_across_instances()
    {
        var store = new JsonBadgeStore(_path);
        Assert.True(await store.AddAsync("Cafe123"));

        var reopened = new JsonBadgeStore(_path);
        Assert.True(await reopened.ContainsAsync("cafe123"));
    }

    [Fact]
    public async Task ReplaceAll_dedupes_and_normalizes()
    {
        var store = new JsonBadgeStore(_path);
        var count = await store.ReplaceAllAsync(new[] { "AA", "aa", " bb " });
        Assert.Equal(2, count);
        var all = await store.GetAllAsync();
        Assert.Contains("aa", all);
        Assert.Contains("bb", all);
    }

    public void Dispose()
    {
        try { if (File.Exists(_path)) File.Delete(_path); } catch { /* best effort */ }
    }
}
