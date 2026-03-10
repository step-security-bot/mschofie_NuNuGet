namespace NuNuGet;

using NuGet.ProjectModel;
using NuGet.Versioning;
using System.Collections.Frozen;

internal static class Traversal
{
    internal class LockFileIndexEntry
    {
        public required LockFileDependency LockFileDependency { get; init; }

        public int InDegree { get; set; }

        public List<string> Consumers { get; } = [];
    }

    /// <summary>
    /// Performs a reverse topological sort of the packages in the given lock file.
    /// </summary>
    /// <param name="packagesLockFile">The lock file to sort.</param>
    /// <returns>The packages in reverse topological order.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the lock file is not a single-target lock file.</exception>
    public static IEnumerable<(string, NuGetVersion)> ReverseTopological(PackagesLockFile packagesLockFile)
    {
        ArgumentNullException.ThrowIfNull(packagesLockFile);

        if (packagesLockFile.Targets.Count != 1)
        {
            throw new InvalidOperationException("Only single-target lock files are supported.");
        }

        return ReverseTopologicalIterator(packagesLockFile);
    }

    private static IEnumerable<(string, NuGetVersion)> ReverseTopologicalIterator(PackagesLockFile packagesLockFile)
    {

        // Build an index mapping packageId to it's entry in the lock file.
        //
        // Since NuGet guarantees that for a given project, for a given target framework, there is only one version of
        // a given package, we can use the packageId as the key for the index.
        IDictionary<string, LockFileIndexEntry> index = packagesLockFile.Targets.Single()
            .Dependencies
            .ToFrozenDictionary(
                dependency => dependency.Id,
                dependency => new LockFileIndexEntry
                {
                    LockFileDependency = dependency,
                    InDegree = dependency.Dependencies.Count,
                },
                StringComparer.OrdinalIgnoreCase);

        // Walk the index and populate the Consumers list for each entry.
        foreach (var (id, dependencyEntry) in index)
        {
            foreach (var childDependency in dependencyEntry.LockFileDependency.Dependencies)
            {
                index[childDependency.Id].Consumers.Add(id);
            }
        }

        // Walk the graph in reverse topological order, starting with the nodes with in-degree 0 (i.e. the leaf nodes),
        // yielding the packageId and version for each node as we visit it.
        Queue<LockFileIndexEntry> queue = new(index.Where(kvp => kvp.Value.InDegree == 0).Select(kvp => kvp.Value));
        for (; queue.Count > 0;)
        {
            LockFileIndexEntry current = queue.Dequeue();
            yield return (current.LockFileDependency.Id, current.LockFileDependency.ResolvedVersion);

            foreach (string consumer in current.Consumers)
            {
                LockFileIndexEntry consumerEntry = index[consumer];

                if (--consumerEntry.InDegree == 0)
                {
                    queue.Enqueue(consumerEntry);
                }
            }
        }
    }
}
