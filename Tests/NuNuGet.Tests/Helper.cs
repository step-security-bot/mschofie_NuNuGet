namespace NuNuGet.Tests;

using System;
using System.IO;
using System.Text.Json;

internal static class Helper
{
    public static void DeleteFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void RemoveFolder(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    public static void CreateFolder(string path)
    {
        if (!Directory.Exists(path))
        {
            _ = Directory.CreateDirectory(path);
        }
    }

    public static void WriteObject<T>(string path, T obj)
    {
        using FileStream fs = File.Create(path);
        JsonSerializer.Serialize(fs, obj);
    }

    public static void WriteFile(string path, string contents)
    {
        using StreamWriter outputFile = new StreamWriter(path);
        outputFile.Write(contents);
    }

    public static void Touch(string path)
    {
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow);
    }

    public static string? FindOnPath(string executableName)
    {
        if (string.IsNullOrWhiteSpace(executableName))
        {
            throw new ArgumentException("Executable name cannot be null or empty.", nameof(executableName));
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathVariable))
        {
            return null;
        }

        foreach (var dir in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                string fullPath = Path.Combine(dir.Trim(), executableName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch
            {
                // Ignore invalid paths
            }
        }

        return null;
    }
}
