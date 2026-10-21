using System.Text;
using Microsoft.Extensions.Logging;

namespace FHZIPA.Installer.Services;

/// <summary>
/// O log do instalador. Grava em arquivo, com rotacao, e nunca recebe
/// credenciais.
/// </summary>
public interface ILogService
{
    /// <summary>
    /// Grava uma linha de log.
    /// </summary>
    void Info(string message, params object?[] args);

    void Warn(string message, params object?[] args);

    void Error(string message, params object?[] args);

    /// <summary>
    /// A pasta onde os logs ficam.
    /// </summary>
    string LogDirectory { get; }

    /// <summary>
    /// O arquivo de log que esta sendo escrito. A tela de erro mostra
    /// este caminho para que a pessoa consiga diagnosticar o problema.
    /// </summary>
    string LogFilePath { get; }
}

/// <summary>
/// O log em arquivo, com rotacao por tamanho. Por baixo ele usa o
/// Microsoft.Extensions.Logging, o que mantem o formato estruturado.
/// </summary>
public sealed class FileLogService : ILogService, IDisposable
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private const int MaxFiles = 5;

    private readonly object _gate = new();
    private readonly string _file;
    private readonly ILogger _logger;
    private readonly ILoggerFactory _factory;

    public FileLogService(string directory)
    {
        LogDirectory = directory;
        Directory.CreateDirectory(LogDirectory);
        _file = Path.Combine(LogDirectory, "instalador.log");

        _factory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(LogLevel.Information)
            .AddProvider(new FileLoggerProvider(this)));

        _logger = _factory.CreateLogger("FHZIPA.Installer");
    }

    public string LogDirectory { get; }

    public string LogFilePath => _file;

    public void Info(string message, params object?[] args) => _logger.LogInformation(message, args);

    public void Warn(string message, params object?[] args) => _logger.LogWarning(message, args);

    public void Error(string message, params object?[] args) => _logger.LogError(message, args);

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Escreve a linha no arquivo, girando quando ele passa do limite.
    /// </summary>
    internal void Write(string level, string category, string message, Exception? exception)
    {
        try
        {
            var line = new StringBuilder()
                .Append(DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append(" [")
                .Append(level)
                .Append("] ")
                .Append(category)
                .Append(": ")
                .Append(message);

            if (exception is not null)
            {
                line.Append(" | ").Append(exception.GetType().Name).Append(": ").Append(exception.Message);
            }

            line.Append(Environment.NewLine);

            lock (_gate)
            {
                Rotate();
                File.AppendAllText(_file, line.ToString(), Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // Um log que falha nao pode derrubar a instalacao.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void Rotate()
    {
        if (!File.Exists(_file) || new FileInfo(_file).Length < MaxBytes)
        {
            return;
        }

        for (var index = MaxFiles - 1; index >= 1; index--)
        {
            var from = Path.Combine(LogDirectory, $"instalador.{index}.log");
            var to = Path.Combine(LogDirectory, $"instalador.{index + 1}.log");
            if (File.Exists(from))
            {
                File.Move(from, to, overwrite: true);
            }
        }

        var oldest = Path.Combine(LogDirectory, $"instalador.{MaxFiles}.log");
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        File.Move(_file, Path.Combine(LogDirectory, "instalador.1.log"), overwrite: true);
    }
}

/// <summary>
/// O provedor que leva as mensagens do Microsoft.Extensions.Logging para o
/// arquivo do instalador.
/// </summary>
internal sealed class FileLoggerProvider : ILoggerProvider
{
    private readonly FileLogService _service;

    public FileLoggerProvider(FileLogService service) => _service = service;

    public ILogger CreateLogger(string categoryName) => new FileLogger(_service, categoryName);

    public void Dispose()
    {
    }
}

/// <summary>
/// O registrador que escreve no arquivo do instalador.
/// </summary>
internal sealed class FileLogger : ILogger
{
    private readonly FileLogService _service;
    private readonly string _category;

    public FileLogger(FileLogService service, string category)
    {
        _service = service;
        _category = category;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        _service.Write(ShortLevel(logLevel), _category, formatter(state, exception), exception);
    }

    private static string ShortLevel(LogLevel level) => level switch
    {
        LogLevel.Trace or LogLevel.Debug => "DBG",
        LogLevel.Information => "INF",
        LogLevel.Warning => "WRN",
        LogLevel.Error => "ERR",
        LogLevel.Critical => "CRT",
        _ => "INF",
    };
}

/// <summary>
/// O log que tambem escreve na saida do aplicativo, para o diagnostico
/// durante o desenvolvimento.
/// </summary>
public sealed class ConsoleLogService : ILogService
{
    public string LogDirectory => string.Empty;

    public string LogFilePath => "(saida no console)";

    public void Info(string message, params object?[] args) =>
        Console.WriteLine($"INFO  {message}");

    public void Warn(string message, params object?[] args) =>
        Console.WriteLine($"WARN  {message}");

    public void Error(string message, params object?[] args) =>
        Console.WriteLine($"ERROR {message}");
}
