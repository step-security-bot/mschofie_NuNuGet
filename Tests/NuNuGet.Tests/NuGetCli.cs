namespace NuNuGet.Tests;

using System;
using System.IO;
using System.Text.Json;

using static NuNuGet.Tests.Helper;

/// <summary>
/// Abstraction for performing NuGet operations.
/// </summary>
internal interface INuGetCli
{
    /// <summary>
    /// Adds a NuGet package to the specified local package source.
    /// </summary>
    /// <param name="packagePath">The file path to the <c>.nupkg</c> file to add.</param>
    /// <param name="source">The root directory of the local NuGet package source.</param>
    public void Add(string packagePath, string source);
}

/// <summary>
/// Factory for creating the default <see cref="INuGetCli"/> implementation.
/// </summary>
internal static class NuGetCli
{
    /// <summary>
    /// Creates the default <see cref="INuGetCli"/> implementation.
    /// </summary>
    /// <returns>A new <see cref="INuGetCli"/> instance.</returns>
    public static INuGetCli Create()
    {
        return NuGetZipCli.Create();
    }
}

/// <summary>
/// Implements <see cref="INuGetCli"/> by shelling out to the <c>nuget.exe</c> CLI.
/// </summary>
internal sealed class NuGetExeCli : INuGetCli
{
    private const string ExecutableName = "nuget.exe";

    private ProcessManagement ProcessManagement { get; } = new ProcessManagement();

    private string ExecutablePath { get; }

    internal NuGetExeCli(string path)
    {
        this.ExecutablePath = path;
    }

    /// <inheritdoc />
    public void Add(string packagePath, string source)
    {
        ProcessResult result = this.ProcessManagement.Run(this.ExecutablePath, $"add \"{packagePath}\" -Source \"{source}\"");
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Failed to add package: {result.StandardOutput}\n {result.StandardError}\n");
        }
    }

    /// <summary>
    /// Creates a new <see cref="NuGetExeCli"/> by locating <c>nuget.exe</c> on the system PATH.
    /// </summary>
    /// <returns>A new <see cref="INuGetCli"/> instance backed by <c>nuget.exe</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when <c>nuget.exe</c> cannot be found on the PATH.</exception>
    public static INuGetCli Create()
    {
        string path = FindOnPath(ExecutableName) ?? throw new InvalidOperationException($"Could not find {ExecutableName} on the system PATH.");
        return new NuGetExeCli(path);
    }
}

/// <summary>
/// Implements <see cref="INuGetCli"/> by directly extracting the <c>.nupkg</c> zip archive
/// and writing the required metadata files, without depending on <c>nuget.exe</c>.
/// </summary>
internal sealed class NuGetZipCli : INuGetCli
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.General) { WriteIndented = true };

    internal NuGetZipCli()
    {
    }

    /// <inheritdoc />
    public void Add(string packagePath, string source)
    {
        // 1) Get the name and version from packagePath
        (string packageName, string packageVersion) = NuGetSupport.GetPackageNameAndVersion(packagePath);
        string lowerCasePackageName = packageName.ToLowerInvariant();

        // 2) Make '<lower case packagename>/<version>' folder
        string targetDir = Path.Combine(source, lowerCasePackageName, packageVersion);
        CreateFolder(targetDir);

        // 3) Copy the package to that folder, naming it '<lower case packagename>.<version>.nupkg'
        string nupkgDestination = Path.Combine(targetDir, $"{lowerCasePackageName}.{packageVersion}.nupkg");
        File.Copy(packagePath, nupkgDestination, overwrite: true);

        // 4) Write '<lower case packagename>.<version>.nupkg.sha512' to the folder, containing the base64-encoded SHA512 hash of the package.
        string packageHash = NuGetSupport.GetPackageSha512Hash(packagePath);
        string sha512Path = Path.Combine(targetDir, $"{lowerCasePackageName}.{packageVersion}.nupkg.sha512");
        WriteFile(sha512Path, packageHash);

        // 5) Write '.nupkg.metadata' with the JSON:
        //   {
        //     "version": 2,
        //     "contentHash": "<base64-encoded sha512 hash of the nupkg file>",
        //   }
        string metadataPath = Path.Combine(targetDir, ".nupkg.metadata");
        string metadataJson = JsonSerializer.Serialize(new { version = 2, contentHash = packageHash }, JsonOptions);
        WriteFile(metadataPath, metadataJson);

        // 6) Extract the .nuspec file from the nupkg and write it to the folder as '<lower case packagename>.nuspec'
        NuGetSupport.ExtractNuspec(packagePath, targetDir, packageName);
    }

    /// <summary>
    /// Creates a new <see cref="NuGetZipCli"/> instance.
    /// </summary>
    /// <returns>A new <see cref="INuGetCli"/> instance that uses zip extraction.</returns>
    public static INuGetCli Create()
    {
        return new NuGetZipCli();
    }
}
