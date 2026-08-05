using System.Reflection;

namespace Catalog.Api;

internal static class ServiceMetadata
{
    public const string Name = "catalog-api";

    public static string Version { get; } = CreateVersion();

    private static string CreateVersion()
    {
        var version = typeof(ServiceMetadata).Assembly.GetName().Version
            ?? throw new InvalidOperationException("The Catalog API assembly version is unavailable.");

        return $"{version.Major}.{version.Minor}.{version.Build}";
    }
}
