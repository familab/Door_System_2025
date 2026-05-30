using System.Text.Json;
using DoorApp.Familab.Application.Options;
using DoorApp.Familab.Domain;
using Microsoft.Extensions.Options;

namespace DoorApp.Familab.Infrastructure.Storage;

/// <summary>
/// JSON-file-backed authorised-badge store (configurable alternative to SQLite).
/// The file holds a flat array of lowercase UID strings.
/// </summary>
public sealed class JsonBadgeStore : IBadgeStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public JsonBadgeStore(IOptions<DoorOptions> options)
        : this(options.Value.Storage.BadgeJsonPath)
    {
    }

    public JsonBadgeStore(string path)
    {
        _path = Path.GetFullPath(path);
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }
    }

    public async Task<bool> ContainsAsync(string uid, CancellationToken cancellationToken = default)
    {
        var set = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return set.Contains(Normalize(uid));
    }

    public async Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var set = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return set.OrderBy(x => x, StringComparer.Ordinal).ToList();
    }

    public async Task<bool> AddAsync(string uid, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var set = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!set.Add(Normalize(uid)))
            {
                return false;
            }
            await SaveAsync(set, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveAsync(string uid, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var set = await LoadAsync(cancellationToken).ConfigureAwait(false);
            if (!set.Remove(Normalize(uid)))
            {
                return false;
            }
            await SaveAsync(set, cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> ReplaceAllAsync(IEnumerable<string> uids, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var set = new HashSet<string>(uids.Select(Normalize).Where(u => u.Length > 0), StringComparer.Ordinal);
            await SaveAsync(set, cancellationToken).ConfigureAwait(false);
            return set.Count;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<HashSet<string>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        await using var stream = File.OpenRead(_path);
        var items = await JsonSerializer.DeserializeAsync<List<string>>(stream, cancellationToken: cancellationToken)
                    .ConfigureAwait(false) ?? new List<string>();
        return new HashSet<string>(items.Select(Normalize), StringComparer.Ordinal);
    }

    private async Task SaveAsync(IEnumerable<string> set, CancellationToken cancellationToken)
    {
        var temp = _path + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, set.OrderBy(x => x, StringComparer.Ordinal).ToList(),
                new JsonSerializerOptions { WriteIndented = true }, cancellationToken).ConfigureAwait(false);
        }
        File.Move(temp, _path, overwrite: true);
    }

    private static string Normalize(string uid) => (uid ?? string.Empty).Trim().ToLowerInvariant();
}
