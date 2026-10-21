using System.Diagnostics;

namespace FHZIPA.Installer.Services;

/// <summary>
/// A abertura do aplicativo instalado.
/// </summary>
public interface IProcessLauncher
{
    /// <summary>
    /// Abre o aplicativo instalado e devolve o que aconteceu.
    /// </summary>
    Task<LaunchOutcome> LaunchAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken);
}

/// <summary>
/// O resultado de uma abertura.
/// </summary>
public sealed record LaunchOutcome(bool Succeeded, string? Error);

/// <summary>
/// A abertura do aplicativo, com as verificacoes antes de rodar.
/// </summary>
public sealed class ProcessLauncher : IProcessLauncher
{
    private readonly ILogService _log;

    public ProcessLauncher(ILogService log) => _log = log;

    public Task<LaunchOutcome> LaunchAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return Task.FromResult(new LaunchOutcome(false, "O aplicativo nao foi encontrado."));
        }

        if (!executablePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            // Um arquivo sem extensao de executavel nao e aberto, mesmo que
            // o nome sugira outra coisa.
            return Task.FromResult(new LaunchOutcome(false, "O arquivo do aplicativo nao e um executavel."));
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
                UseShellExecute = true,
            };

            // Os argumentos vem do proprio instalador, nunca de entrada do
            // usuario ou de um arquivo baixado.
            foreach (var argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            Process.Start(info);
            _log.Info("Aplicativo iniciado: {0}", executablePath);
            return Task.FromResult(new LaunchOutcome(true, null));
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _log.Error("Nao foi possivel abrir o aplicativo: {0}", exception.Message);
            return Task.FromResult(new LaunchOutcome(false, exception.Message));
        }
    }
}
