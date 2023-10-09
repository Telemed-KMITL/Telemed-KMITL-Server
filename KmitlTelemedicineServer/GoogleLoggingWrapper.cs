using ILogger = Google.Apis.Logging.ILogger;

namespace KmitlTelemedicineServer;

internal class GoogleLoggingWrapper : ILogger
{
    private readonly ILoggerFactory _factory;

    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public GoogleLoggingWrapper(WebApplication app)
    {
        _factory = app.Services.GetRequiredService<ILoggerFactory>();
        _logger = app.Logger;
    }

    private GoogleLoggingWrapper(ILoggerFactory factory, Microsoft.Extensions.Logging.ILogger logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public ILogger ForType(Type type)
    {
        return new GoogleLoggingWrapper(_factory, _logger);
    }

    public ILogger ForType<TOther>()
    {
        return new GoogleLoggingWrapper(_factory, _factory.CreateLogger<TOther>());
    }

    public void Debug(string message, params object[] formatArgs)
    {
        _logger.LogDebug(message, formatArgs);
    }

    public void Info(string message, params object[] formatArgs)
    {
        _logger.LogInformation(message, formatArgs);
    }

    public void Warning(string message, params object[] formatArgs)
    {
        _logger.LogWarning(message, formatArgs);
    }

    public void Error(Exception exception, string message, params object[] formatArgs)
    {
        _logger.LogError(exception, message, formatArgs);
    }

    public void Error(string message, params object[] formatArgs)
    {
        _logger.LogError(message, formatArgs);
    }

    public bool IsDebugEnabled => _logger.IsEnabled(LogLevel.Debug);
}