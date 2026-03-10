namespace NuNuGet.Commands;

using Microsoft.Extensions.Logging;
using NuGet.Commands;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Signing;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuNuGet.Models;
using NuNuGet.Options;
using System.CommandLine;
using System.Text.Json;

using Logging = Microsoft.Extensions.Logging;

internal static class PackageEntryExtensions
{
    public static LibraryDependency ToLibraryDependency(this PackageEntry package)
    {
        return new()
        {
            LibraryRange = new(package.Id, VersionRange.Parse(package.Version), LibraryDependencyTarget.Package)
        };
    }
}

/// <summary>
/// A command that installs packages from a packages.list.json file to the global packages folder, using a lock file to ensure repeatable restores and to write out the resolved package graph.
/// </summary>
internal sealed class InstallCommand : Command
{
    private ILoggerFactory LoggerFactory { get; }

    private Logging.ILogger Logger { get; set; } = Logging.Abstractions.NullLogger.Instance;

    private string WorkingDirectory { get; } = Directory.GetCurrentDirectory();

    private FileInfo? ConfigFile { get; set; }

    private FileInfo? ListFile { get; set; }

    private string LockFile { get; set; } = string.Empty;

    private string ProjectName { get; } = "NuNuGet";

    /// <summary>
    /// Initializes a new instance of the <see cref="InstallCommand"/> class.
    /// </summary>
    /// <param name="loggerFactory">The <see cref="ILoggerFactory"/> to create loggers.</param>
    public InstallCommand(ILoggerFactory loggerFactory)
        : base("install", "Install packages from a packages.list.json file to the global packages folder")
    {
        this.LoggerFactory = loggerFactory;

        this.Add(ConfigFileOption.Instance);
        this.Add(ListFileOption.Instance);
        this.Add(LockFileOption.Instance);
        this.Add(VerboseOption.Instance);

        this.SetAction(this.Invoke);
    }

    private void ParseOptions(ParseResult parseResult)
    {
        this.LockFile = parseResult.GetValue(LockFileOption.Instance) ?? throw new InvalidOperationException("The --lockFile option is required.");
        if (!Path.IsPathFullyQualified(this.LockFile))
        {
            this.LockFile = Path.Combine(this.WorkingDirectory, this.LockFile);
        }
        this.LockFile = Path.GetFullPath(this.LockFile, this.WorkingDirectory);

        this.ConfigFile = parseResult.GetValue(ConfigFileOption.Instance) ?? throw new InvalidOperationException("The --configFile option is required.");
        if (!this.ConfigFile.Exists)
        {
            throw new InvalidOperationException("Config file does not exist.");
        }

        this.ListFile = parseResult.GetValue(ListFileOption.Instance) ?? throw new InvalidOperationException("The --listFile option is required.");
        if (!this.ListFile.Exists)
        {
            throw new InvalidOperationException("Package list file does not exist.");
        }
    }

    private ISettings LoadSettings()
    {
        return Settings.LoadSpecificSettings(this.ConfigFile!.DirectoryName!, this.ConfigFile!.Name);
    }

    private static PackageList LoadPackageList(FileInfo listFile)
    {
        using FileStream packageListStream = listFile.OpenRead();
        PackageList? packageList = JsonSerializer.Deserialize(packageListStream, PackageListJsonContext.Default.PackageList);

        return packageList ?? throw new InvalidOperationException("Failed to deserialize PackageList file.");
    }

    private PackageSpec BuildPackageSpec(PackageList packageList, string globalPackagesPath)
    {
        PackageSpec packageSpec = new()
        {
            Name = this.ProjectName,
            FilePath = Path.Combine(this.WorkingDirectory, this.ProjectName + ".csproj"),
            RestoreMetadata = new ProjectRestoreMetadata
            {
                ProjectName = this.ProjectName,
                ProjectUniqueName = this.ProjectName,
                CacheFilePath = null,
                ProjectPath = Path.Combine(this.WorkingDirectory, this.ProjectName + ".csproj"),
                OutputPath = Path.Combine(this.WorkingDirectory, "obj"),
                PackagesPath = globalPackagesPath,
                ProjectStyle = ProjectStyle.PackageReference,
                RestoreLockProperties = new RestoreLockProperties(
                    restorePackagesWithLockFile: "True",
                    nuGetLockFilePath: this.LockFile,
                    restoreLockedMode: true),
            },
        };

        packageSpec.TargetFrameworks.Add(new TargetFrameworkInformation
        {
            FrameworkName = NuGetFramework.Parse(packageList.TargetFramework),
            Dependencies = [.. packageList.Packages.Select(p => p.ToLibraryDependency())]
        });

        return packageSpec;
    }

    private void WriteLockFile(PackagesLockFile packagesLockFile)
    {
        string newLockFileContent = PackagesLockFileFormat.Render(packagesLockFile);

        if (File.Exists(this.LockFile))
        {
            FileInfo existingLockFile = new FileInfo(this.LockFile);

            if (existingLockFile.Length == newLockFileContent.Length
                && newLockFileContent == File.ReadAllText(this.LockFile))
            {
                // Nothing has changed, no need to update the lock file on disk.
                return;
            }
        }

        File.WriteAllText(this.LockFile, newLockFileContent);
    }

    private async Task<int> Invoke(ParseResult parseResult, CancellationToken cancellationToken)
    {
        if (parseResult.GetValue(VerboseOption.Instance))
        {
            this.Logger = this.LoggerFactory.CreateLogger<InstallCommand>();
        }

        this.ParseOptions(parseResult);

        ISettings settings = this.LoadSettings();
        string globalPackagesPath = SettingsUtility.GetGlobalPackagesFolder(settings);

        // Load and deserialize the package list JSON file
        PackageList packageList = LoadPackageList(this.ListFile!);

        PackageSpec packageSpec = this.BuildPackageSpec(packageList, globalPackagesPath);
        DependencyGraphSpec dependencyGraphSpec = new();
        dependencyGraphSpec.AddProject(packageSpec);
        dependencyGraphSpec.AddRestore(packageSpec.RestoreMetadata.ProjectUniqueName);

        IEnumerable<Lazy<INuGetResourceProvider>> providers = Repository.Provider.GetCoreV3();
        RestoreCommandProvidersCache restoreCommandProvidersCache = new();
        SourceCacheContext cacheContext = new();
        PackageSourceProvider sourceProvider = new(settings);
        LockFileBuilderCache lockFileBuilderCache = new();
        List<SourceRepository> repositories = [.. sourceProvider.LoadPackageSources().Where(s => s.IsEnabled).Select(packageSource => new SourceRepository(packageSource, providers))];
        NuGetLoggerAdapter nugetLogger = new(this.Logger);
        ClientPolicyContext clientPolicyContext = ClientPolicyContext.GetClientPolicy(settings, nugetLogger);
        RestoreCommandProviders dependencyProviders = restoreCommandProvidersCache.GetOrCreate(
            globalPackagesPath: globalPackagesPath,
            fallbackPackagesPaths: [],
            sources: repositories,
            cacheContext: cacheContext,
            log: nugetLogger
            );

        PackageSourceMapping packageSourceMapping = PackageSourceMapping.GetPackageSourceMapping(settings);
        RestoreRequest request = new(packageSpec, dependencyProviders, cacheContext, clientPolicyContext, packageSourceMapping, nugetLogger, lockFileBuilderCache)
        {
            AllowNoOp = true,
            ProjectStyle = ProjectStyle.PackageReference,
            DependencyGraphSpec = dependencyGraphSpec,
        };

        // Run the RestoreCommand
        RestoreCommand command = new(request);
        RestoreResult result = await command.ExecuteAsync(cancellationToken);

        if (!result.Success)
        {
            // If result.LogMessages contains a 'NU1004', then the ListFile has deviated from the LockFile.
            bool staleLockFile = result.LogMessages.Any(m => m.Code == NuGetLogCode.NU1004);
            if (staleLockFile)
            {
                await Console.Error.WriteLineAsync("Restore failed due to a mismatch between the package list and the lock file. Delete the lock file to force a rebuild.");
                return 100;
            }

            throw new InvalidOperationException("Restore failed:\n" + string.Join("\n", result.LogMessages.Select(m => m.Message)));
        }

        // Write warnings to Console.Error
        foreach (var warning in result.LogMessages.Where(m => m.Level == NuGet.Common.LogLevel.Warning))
        {
            await Console.Error.WriteLineAsync(warning.Message);
            // Look for audit errors?
        }

        // Write output files
        //
        // Note: Ideally, this would write out the 'cache file' as well, but that is currently not supported by the
        // public API. The cache file is used to speed up subsequent restores by caching information about the remote
        // sources, so it is not strictly necessary to write it out for the install command to function correctly.

        // If the result has a lock file, write it. Otherwise load the existing one in order to write the package list.
        PackagesLockFile? packagesLockFile = null;
        if (result.LockFile is not null)
        {
            packagesLockFile = new PackagesLockFileBuilder().CreateNuGetLockFile(result.LockFile);

            this.WriteLockFile(packagesLockFile);
        }
        else
        {
            packagesLockFile = PackagesLockFileFormat.Read(this.LockFile, nugetLogger);
        }

        // Write out the metadata for consumption
        Console.WriteLine($"GlobalPackagesPath: {globalPackagesPath}");

        // Write the reverse-topological install order
        foreach (var (id, version) in Traversal.ReverseTopological(packagesLockFile))
        {
            Console.WriteLine($"Package: {id}/{version}");
        }

        return 0;
    }
}
