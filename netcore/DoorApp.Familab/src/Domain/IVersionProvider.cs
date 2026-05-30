namespace DoorApp.Familab.Domain;

/// <summary>Provides the application version embedded in the assembly (AssemblyInfo.cs).</summary>
public interface IVersionProvider
{
    /// <summary>The informational/semantic version string (e.g. "1.2.3-beta").</summary>
    string Version { get; }
}
