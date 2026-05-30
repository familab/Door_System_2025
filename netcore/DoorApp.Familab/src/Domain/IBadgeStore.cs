namespace DoorApp.Familab.Domain;

/// <summary>
/// Persistence for the set of authorised badge UIDs. Backed by SQLite or a JSON file.
/// Replaces the Python local CSV / Google Sheets badge list.
/// </summary>
public interface IBadgeStore
{
    /// <summary>Returns true if the (case-insensitive) UID is authorised.</summary>
    Task<bool> ContainsAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>All authorised UIDs (lowercase).</summary>
    Task<IReadOnlyList<string>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Add a UID to the authorised set. Returns true if newly added.</summary>
    Task<bool> AddAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>Remove a UID. Returns true if it existed.</summary>
    Task<bool> RemoveAsync(string uid, CancellationToken cancellationToken = default);

    /// <summary>Replace the entire authorised set atomically. Returns the new count.</summary>
    Task<int> ReplaceAllAsync(IEnumerable<string> uids, CancellationToken cancellationToken = default);
}
