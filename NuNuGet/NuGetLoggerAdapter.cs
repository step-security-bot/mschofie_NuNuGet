namespace NuNuGet;

using Microsoft.Extensions.Logging;

/// <summary>
/// Adapts an <see cref="ILogger"/> to the <see cref="NuGet.Common.ILogger"/> interface.
/// </summary>
/// <param name="logger">The <see cref="ILogger"/> instance to wrap.</param>
internal sealed class NuGetLoggerAdapter(ILogger logger) : NuGet.Common.ILogger
{
    private static readonly Action<ILogger, string, Exception?> logDebug = LoggerMessage.Define<string>(LogLevel.Debug, new EventId(0, nameof(logDebug)), "{Data}");

    private static readonly Action<ILogger, string, Exception?> logVerbose = LoggerMessage.Define<string>(LogLevel.Information, new EventId(0, nameof(logVerbose)), "{Data}");

    private static readonly Action<ILogger, string, Exception?> logInformation = LoggerMessage.Define<string>(LogLevel.Information, new EventId(0, nameof(logInformation)), "{Data}");

    private static readonly Action<ILogger, string, Exception?> logMinimal = LoggerMessage.Define<string>(LogLevel.Information, new EventId(0, nameof(logMinimal)), "{Data}");

    private static readonly Action<ILogger, string, Exception?> logWarning = LoggerMessage.Define<string>(LogLevel.Warning, new EventId(0, nameof(logWarning)), "{Data}");

    private static readonly Action<ILogger, string, Exception?> logError = LoggerMessage.Define<string>(LogLevel.Error, new EventId(0, nameof(logError)), "{Data}");

    private readonly ILogger logger = logger;

    /// <inheritdoc/>
    public void LogDebug(string data) => NuGetLoggerAdapter.logDebug(this.logger, data, null);

    /// <inheritdoc/>
    public void LogVerbose(string data) => NuGetLoggerAdapter.logVerbose(this.logger, data, null);

    /// <inheritdoc/>
    public void LogInformation(string data) => NuGetLoggerAdapter.logInformation(this.logger, data, null);

    /// <inheritdoc/>
    public void LogMinimal(string data) => NuGetLoggerAdapter.logMinimal(this.logger, data, null);

    /// <inheritdoc/>
    public void LogWarning(string data) => NuGetLoggerAdapter.logWarning(this.logger, data, null);

    /// <inheritdoc/>
    public void LogError(string data) => NuGetLoggerAdapter.logError(this.logger, data, null);

    /// <inheritdoc/>
    public void LogInformationSummary(string data) => NuGetLoggerAdapter.logInformation(this.logger, data, null);

    /// <inheritdoc/>
    public void Log(NuGet.Common.LogLevel level, string data)
    {
        switch (level)
        {
            case NuGet.Common.LogLevel.Debug:
                NuGetLoggerAdapter.logDebug(this.logger, data, null);
                break;
            case NuGet.Common.LogLevel.Verbose:
                NuGetLoggerAdapter.logVerbose(this.logger, data, null);
                break;
            case NuGet.Common.LogLevel.Information:
            case NuGet.Common.LogLevel.Minimal:
                NuGetLoggerAdapter.logInformation(this.logger, data, null);
                break;
            case NuGet.Common.LogLevel.Warning:
                NuGetLoggerAdapter.logWarning(this.logger, data, null);
                break;
            case NuGet.Common.LogLevel.Error:
                NuGetLoggerAdapter.logError(this.logger, data, null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(level), level, null);
        }
    }

    /// <inheritdoc/>
    public void Log(NuGet.Common.ILogMessage message)
    {
        this.Log(message.Level, message.Message);
    }

    /// <inheritdoc/>
    public Task LogAsync(NuGet.Common.LogLevel level, string data)
    {
        this.Log(level, data);
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task LogAsync(NuGet.Common.ILogMessage message)
    {
        this.Log(message.Level, message.Message);
        return Task.CompletedTask;
    }
}
