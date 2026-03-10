namespace NuNuGet.Models;

using System.Text.Json.Serialization;

/// <summary>
/// Represents a package entry in the package list.
/// </summary>
internal sealed class PackageEntry
{
    /// <summary>
    /// Gets or sets the NuGet package identifier.
    /// </summary>
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    /// <summary>
    /// Gets or sets the floating version string (e.g., "1.8.2109", "[1.0,2.0)", "1.*").
    /// </summary>
    [JsonPropertyName("version")]
    public required string Version { get; set; }
}

/// <summary>
/// Represents the root structure of a package list JSON file.
/// </summary>
internal sealed class PackageList
{
    /// <summary>
    /// Gets or sets the target framework string (e.g., "net8.0", "netstandard2.0").
    /// </summary>
    [JsonPropertyName("targetFramework")]
    public required string TargetFramework { get; set; }

    /// <summary>
    /// Gets or sets the list of packages.
    /// </summary>
    [JsonPropertyName("packages")]
    public required IReadOnlyList<PackageEntry> Packages { get; set; }
}

/// <summary>
/// Source-generated JSON serializer context for AOT-friendly serialization.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PackageList))]
[JsonSerializable(typeof(PackageEntry))]
[JsonSerializable(typeof(IReadOnlyList<PackageEntry>))]
internal partial class PackageListJsonContext : JsonSerializerContext;
