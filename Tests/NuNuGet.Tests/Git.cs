namespace NuNuGet.Tests;

using System;

public static class Git
{
    /// <summary>
    /// Gets the repository root.
    /// </summary>
    /// <param name="fromPath">The path to retrieve the repository root from.</param>
    /// <returns>The path to the root of the repository.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the repository root cannot be determined.</exception>
    public static string GetRepositoryRoot(string fromPath)
    {
        ProcessManagement processManagement = new ProcessManagement()
        {
            WorkingDirectory = fromPath,
        };

        ProcessResult result = processManagement.Run("git", "rev-parse --show-toplevel");

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Unable to determine the repository root: {result.StandardOutput}\n {result.StandardError}\n");
        }

        return result.StandardOutput.TrimEnd('\r', '\n');
    }
}
