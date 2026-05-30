using System.Reflection;
using DoorApp.Familab.Domain;

namespace DoorApp.Familab.Infrastructure.Versioning;

/// <summary>
/// Reads the version embedded in AssemblyInfo.cs (AssemblyInformationalVersion).
/// This is the .NET equivalent of the Python version.py / __version__ value.
/// </summary>
public sealed class AssemblyVersionProvider : IVersionProvider
{
    public AssemblyVersionProvider()
    {
        var assembly = typeof(AssemblyVersionProvider).Assembly;
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        // Strip any build-metadata suffix appended by the SDK (e.g. "+<sha>").
        if (!string.IsNullOrEmpty(informational))
        {
            var plus = informational.IndexOf('+');
            Version = plus >= 0 ? informational[..plus] : informational;
        }
        else
        {
            Version = assembly.GetName().Version?.ToString() ?? "0.0.0-unknown";
        }
    }

    public string Version { get; }
}
