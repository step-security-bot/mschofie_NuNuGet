namespace NuNuGet.Tests;

using System;
using System.Diagnostics;
using System.Text;

internal readonly struct ProcessResult
{
    public int ExitCode { get; init; }

    public string StandardOutput { get; init; }

    public string StandardError { get; init; }
}

internal sealed class ProcessManagement
{
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public TimeSpan Timeout { get; set; } = DefaultTimeout;

    public string WorkingDirectory { get; set; } = Environment.CurrentDirectory;

    public IDictionary<string, string?> EnvironmentVariables { get; } = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Runs a process with the given arguments.
    /// </summary>
    /// <param name="exePath">Executable path</param>
    /// <param name="arguments">Process arguments</param>
    /// <returns>The result of the process execution.</returns>
    /// <exception cref="TimeoutException">Thrown when the process does not exit within the configured <see cref="Timeout"/>.</exception>
    public ProcessResult Run(string exePath, string arguments)
    {
        ProcessStartInfo startInfo = new(exePath)
        {
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = this.WorkingDirectory,
        };

        foreach ((string name, string? value) in this.EnvironmentVariables)
        {
            if (value is null)
            {
                _ = startInfo.Environment.Remove(name);
            }
            else
            {
                startInfo.Environment[name] = value;
            }
        }

        StringBuilder outputBuffer = new();
        StringBuilder errorBuffer = new();

        using Process process = Process.Start(startInfo)!;

        // Both Process.ErrorDataReceived and Process.OutputDataReceived are raised with data that is missing the
        // line ending characters. The error and output streams are also tokenized on *both* '\r' and '\n', so if
        // an application uses '\r\n' line endings, then (for example) "Hello World!\r\n" will raise two events -
        // one the token from the start of the line up to the '\r', and a second for the token between the '\r'
        // and '\n'. Both events are also raised with 'null' at the end of processing.
        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _ = errorBuffer.AppendLine(e.Data);
            }
        };

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                _ = outputBuffer.AppendLine(e.Data);
            }
        };

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        bool exited = process.WaitForExit(this.Timeout);
        if (!exited)
        {
            throw new TimeoutException($"Process did not exit within the timeout of {this.Timeout.TotalSeconds} seconds: ProcessName={process.ProcessName} HasExited={process.HasExited}");
        }

        // Process.WaitForExit(int) will not wait for asynchronous processing to complete. If that overload
        // returns true - to show that the process has exited - call Process.WaitForExit() to block on the
        // completion of asynchronous processing.
        process.WaitForExit();

        return new()
        {
            ExitCode = process.ExitCode,
            StandardOutput = outputBuffer.ToString(),
            StandardError = errorBuffer.ToString(),
        };
    }
}
