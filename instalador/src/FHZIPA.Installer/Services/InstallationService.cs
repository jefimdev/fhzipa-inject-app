using System.Security.Cryptography;
using FHZIPA.Installer.Installation;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Security;

namespace FHZIPA.Installer.Services;

/// <summary>
/// O motor de instalacao. Ele faz as operacoes de verdade: copiar os
/// arquivos, criar os atalhos, registrar o desinstalador e, se algo
/// falhar, desfazer o que ja foi feito.
/// </summary>
public interface IInstallationService
{
    /// <summary>
    /// Instala o aplicativo.
    /// </summary>
    Task<InstallOutcome> InstallAsync(
        string payloadPath,
        string installDirectory,
        InstallOptions options,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken);

    /// <summary>
    /// Desfaz uma instalacao, usando o registro do que foi feito.
    /// </summary>
    Task<RollbackOutcome> RollbackAsync(InstallationJournal journal, CancellationToken cancellationToken);

    /// <summary>
    /// Remove uma instalacao existente.
    /// </summary>
    Task<UninstallOutcome> UninstallAsync(string installDirectory, CancellationToken cancellationToken);

    /// <summary>
    /// Cria o atalho da area de trabalho. E o que a opcao da tela final
    /// usa quando a pessoa decide marcar a caixa.
    /// </summary>
    void CreateDesktopShortcut(string executablePath);

    /// <summary>
    /// Diz se o desinstalador esta registrado em Aplicativos instalados.
    /// </summary>
    bool IsRegistered();
}

/// <summary>
/// As opcoes da instalacao.
/// </summary>
public sealed record InstallOptions(
    string ExecutableName,
    bool CreateDesktopShortcut,
    bool CreateStartMenuShortcut,
    string? ExpectedSha256,
    string UninstallRegistryKey,
    string UninstallCommand)
{
    /// <summary>
    /// O nome do produto como aparece em Aplicativos instalados.
    /// </summary>
    public string ProductName { get; init; } = "FHZIPA INJECT";

    /// <summary>
    /// A versao do produto, a mesma que vai no registro.
    /// </summary>
    public string ProductVersion { get; init; } = "1.0.0";

    /// <summary>
    /// Quem publica o produto, como aparece em Aplicativos instalados.
    /// </summary>
    public string Publisher { get; init; } = "FHZIPA INJECT";
}

/// <summary>
/// O resultado de uma instalacao.
/// </summary>
public sealed record InstallOutcome(bool Succeeded, InstallerError? Error, string InstallDirectory);

/// <summary>
/// O resultado do rollback.
/// </summary>
public sealed record RollbackOutcome(bool Succeeded, bool RecoveryRequired, IReadOnlyList<string> Undone);

/// <summary>
/// O resultado da desinstalacao.
/// </summary>
public sealed record UninstallOutcome(bool Succeeded, IReadOnlyList<string> Removed);

/// <summary>
/// O motor de instalacao.
/// </summary>
public sealed class InstallationService : IInstallationService
{
    /// <summary>
    /// A chave do desinstalador em Aplicativos instalados.
    /// </summary>
    public const string UninstallRegistryKeyName =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FHZIPA INJECT";

    private readonly ILogService _log;

    public InstallationService(ILogService log) => _log = log;

    public async Task<InstallOutcome> InstallAsync(
        string payloadPath,
        string installDirectory,
        InstallOptions options,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken)
    {
        var journal = new InstallationJournal { InstallDirectory = installDirectory };
        var stateDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FHZIPA INJECT");

        try
        {
            Directory.CreateDirectory(stateDirectory);
            journal.Save(stateDirectory);

            progress?.Report(new InstallerProgress
            {
                OperationName = "Instalando",
                CurrentStage = "Preparando",
                CurrentMessage = "Preparando o diretorio de instalacao",
                IsIndeterminate = true,
            });

            if (!Directory.Exists(installDirectory))
            {
                Directory.CreateDirectory(installDirectory);
                journal.CreatedDirectories.Add(installDirectory);
            }

            cancellationToken.ThrowIfCancellationRequested();

            // A verificacao de integridade vem antes de qualquer escrita.
            if (!string.IsNullOrWhiteSpace(options.ExpectedSha256))
            {
                progress?.Report(new InstallerProgress
                {
                    OperationName = "Instalando",
                    CurrentStage = "Validando",
                    CurrentMessage = "Conferindo o resumo do aplicativo",
                    IsIndeterminate = true,
                });

                var actual = await ComputeSha256Async(payloadPath, cancellationToken).ConfigureAwait(false);
                if (!string.Equals(actual, options.ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    var error = InstallerError.Create(
                        InstallerErrorCode.IntegrityCheckFailed,
                        $"Esperado {options.ExpectedSha256}, encontrado {actual}.");

                    progress?.Report(new InstallerProgress
                    {
                        OperationName = "Instalando",
                        CurrentStage = "Validando",
                        CurrentMessage = error.Description,
                        ErrorCode = error.CodeText,
                        ErrorMessage = error.Description,
                    });

                    return new InstallOutcome(false, error, installDirectory);
                }
            }

            progress?.Report(new InstallerProgress
            {
                OperationName = "Instalando",
                CurrentStage = "Instalando",
                CurrentMessage = "Copiando os arquivos do aplicativo",
                IsIndeterminate = true,
            });

            var target = Path.Combine(installDirectory, options.ExecutableName);
            if (File.Exists(target))
            {
                var backup = target + ".fhzipa-antigo";
                File.Copy(target, backup, overwrite: true);
                journal.BackedUpFiles.Add(backup);
            }

            await CopyAsync(payloadPath, target, progress, cancellationToken).ConfigureAwait(false);
            journal.InstalledFiles.Add(target);

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new InstallerProgress
            {
                OperationName = "Instalando",
                CurrentStage = "Criando atalhos",
                CurrentMessage = "Criando os atalhos",
                IsIndeterminate = true,
            });

            foreach (var shortcut in BuildShortcuts(options, installDirectory))
            {
                try
                {
                    CreateShortcut(shortcut.Path, shortcut.Target);
                    journal.CreatedShortcuts.Add(shortcut.Path);
                }
                catch (IOException exception)
                {
                    _log.Warn("Nao foi possivel criar o atalho {0}: {1}", shortcut.Path, exception.Message);
                }
            }

            progress?.Report(new InstallerProgress
            {
                OperationName = "Instalando",
                CurrentStage = "Registrando o aplicativo",
                CurrentMessage = "Registrando o aplicativo em Aplicativos instalados",
                IsIndeterminate = true,
            });

            WriteUninstallRegistry(options, installDirectory, journal);

            progress?.Report(new InstallerProgress
            {
                OperationName = "Instalando",
                CurrentStage = "Escrevendo o desinstalador",
                CurrentMessage = "Escrevendo o desinstalador",
                IsIndeterminate = true,
            });

            WriteUninstaller(installDirectory, options, journal);

            journal.Completed = true;
            InstallationJournal.Clear(stateDirectory);

            progress?.Report(new InstallerProgress
            {
                OperationName = "Instalando",
                CurrentStage = "Finalizando",
                CurrentMessage = "Instalacao concluida",
                IsIndeterminate = false,
                TotalBytes = 1,
                BytesProcessed = 1,
            });

            return new InstallOutcome(true, null, installDirectory);
        }
        catch (OperationCanceledException)
        {
            _log.Warn("A instalacao foi cancelada.");
            var rollback = await RollbackAsync(journal, cancellationToken).ConfigureAwait(false);
            return new InstallOutcome(
                false,
                InstallerError.Create(InstallerErrorCode.CancelledByUser),
                installDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Error("Falha na instalacao: {0}", exception.Message);
            await RollbackAsync(journal, CancellationToken.None).ConfigureAwait(false);

            var code = exception is UnauthorizedAccessException
                ? InstallerErrorCode.InsufficientPermissions
                : InstallerErrorCode.CopyFailed;

            return new InstallOutcome(false, InstallerError.Create(code, exception.Message), installDirectory);
        }
    }

    public async Task<RollbackOutcome> RollbackAsync(
        InstallationJournal journal,
        CancellationToken cancellationToken)
    {
        var undone = new List<string>();
        var recoveryRequired = false;

        foreach (var file in journal.InstalledFiles.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                    undone.Add(file);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log.Error("Nao foi possivel remover {0}: {1}", file, exception.Message);
                recoveryRequired = true;
            }
        }

        foreach (var backup in journal.BackedUpFiles.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(backup))
                {
                    var original = backup.Replace(".fhzipa-antigo", string.Empty, StringComparison.Ordinal);
                    File.Move(backup, original, overwrite: true);
                    undone.Add(original);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log.Error("Nao foi possivel restaurar {0}: {1}", backup, exception.Message);
                recoveryRequired = true;
            }
        }

        foreach (var shortcut in journal.CreatedShortcuts.AsEnumerable().Reverse())
        {
            try
            {
                if (File.Exists(shortcut))
                {
                    File.Delete(shortcut);
                    undone.Add(shortcut);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log.Error("Nao foi possivel remover o atalho {0}: {1}", shortcut, exception.Message);
                recoveryRequired = true;
            }
        }

        foreach (var directory in journal.CreatedDirectories.AsEnumerable().Reverse())
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                {
                    Directory.Delete(directory);
                    undone.Add(directory);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                _log.Error("Nao foi possivel remover {0}: {1}", directory, exception.Message);
                recoveryRequired = true;
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
        return new RollbackOutcome(!recoveryRequired, recoveryRequired, undone);
    }

    public async Task<UninstallOutcome> UninstallAsync(
        string installDirectory,
        CancellationToken cancellationToken)
    {
        var removed = new List<string>();

        try
        {
            if (!Directory.Exists(installDirectory))
            {
                return new UninstallOutcome(true, removed);
            }

            var running = Environment.ProcessPath;
            var self = string.IsNullOrEmpty(running)
                ? string.Empty
                : Path.GetFullPath(running);

            var selfIsInside = false;

            foreach (var file in Directory.EnumerateFiles(installDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();

                // O executavel em uso nao pode ser apagado agora. A pasta
                // e removida logo depois, quando o processo sai.
                if (string.Equals(Path.GetFullPath(file), self, StringComparison.OrdinalIgnoreCase))
                {
                    selfIsInside = true;
                    continue;
                }

                try
                {
                    File.Delete(file);
                    removed.Add(file);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    _log.Error("Nao foi possivel remover {0}: {1}", file, exception.Message);
                }
            }

            if (selfIsInside)
            {
                // O instalador esta rodando de dentro da pasta que seria
                // apagada. Uma copia dele, na pasta temporaria, espera este
                // processo terminar e faz a limpeza. Sem shell.
                ScheduleFolderRemoval(installDirectory);
                removed.Add(installDirectory);
                return new UninstallOutcome(true, removed);
            }

            Directory.Delete(installDirectory, recursive: true);
            removed.Add(installDirectory);
            return new UninstallOutcome(true, removed);
        }
        catch (OperationCanceledException)
        {
            return new UninstallOutcome(false, removed);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Error("Falha ao desinstalar: {0}", exception.Message);
            return new UninstallOutcome(false, removed);
        }
    }

    /// <summary>
    /// Deixa uma copia do instalador na pasta temporaria para apagar a pasta
    /// de instalacao depois que este processo sair.
    /// </summary>
    private void ScheduleFolderRemoval(string installDirectory)
    {
        try
        {
            var source = Environment.ProcessPath;
            if (string.IsNullOrEmpty(source) || !File.Exists(source))
            {
                return;
            }

            var workDirectory = Path.Combine(Path.GetTempPath(), "FHZIPA INJECT", "uninstall");
            Directory.CreateDirectory(workDirectory);

            var copy = Path.Combine(
                workDirectory,
                "cleanup-" + Guid.NewGuid().ToString("N") + ".exe");
            File.Copy(source, copy, true);

            var info = new System.Diagnostics.ProcessStartInfo
            {
                FileName = copy,
                Arguments = "--remove-folder \"" + installDirectory + "\" " +
                    Environment.ProcessId.ToString(),
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = workDirectory,
            };

            System.Diagnostics.Process.Start(info);
            _log.Info("A remocao da pasta {0} foi agendada.", installDirectory);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Nao foi possivel agendar a remocao da pasta: {0}", exception.Message);
        }
    }

    public void CreateDesktopShortcut(string executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            throw new FileNotFoundException("O aplicativo nao foi encontrado.", executablePath);
        }

        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var link = Path.Combine(desktop, "FHZIPA INJECT.lnk");

        CreateShortcut(link, executablePath);
        _log.Info("Atalho da area de trabalho criado: {0}", link);
    }

    public bool IsRegistered()
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(UninstallRegistryKeyName);
        return key?.GetValue("UninstallString") is string value && !string.IsNullOrWhiteSpace(value);
    }

    private static async Task CopyAsync(
        string source,
        string target,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        await using var output = new FileStream(
            target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);

        var total = input.Length;
        var processed = 0L;
        var buffer = new byte[1024 * 1024];
        var lastReport = DateTimeOffset.MinValue;
        var speed = new TransferSpeedEstimator();
        speed.Start(total);

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            processed += read;
            speed.Add(read);

            var now = DateTimeOffset.UtcNow;
            if ((now - lastReport).TotalMilliseconds >= 100)
            {
                lastReport = now;
                progress?.Report(new InstallerProgress
                {
                    OperationName = "Instalando",
                    CurrentStage = "Instalando",
                    CurrentMessage = "Copiando os arquivos do aplicativo",
                    BytesProcessed = processed,
                    TotalBytes = total,
                    BytesPerSecond = speed.BytesPerSecond,
                    EstimatedTimeRemaining = speed.EstimatedRemaining(processed),
                });
            }
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    internal static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IEnumerable<(string Path, string Target)> BuildShortcuts(        InstallOptions options,
        string installDirectory)
    {
        var executable = Path.Combine(installDirectory, options.ExecutableName);

        if (options.CreateStartMenuShortcut)
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs", "FHZIPA INJECT");
            yield return (Path.Combine(folder, "FHZIPA INJECT.lnk"), executable);
        }

        if (options.CreateDesktopShortcut)
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            yield return (Path.Combine(desktop, "FHZIPA INJECT.lnk"), executable);
        }
    }

    private static void CreateShortcut(string linkPath, string target)
    {
        var directory = Path.GetDirectoryName(linkPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // O atalho e um arquivo de shell, escrito pelo proprio Windows.
        var shell = Type.GetTypeFromProgID("WScript.Shell");
        if (shell is null)
        {
            throw new InvalidOperationException("O Windows nao Disponibilizou a criacao de atalhos.");
        }

        object? instance = Activator.CreateInstance(shell);
        if (instance is null)
        {
            throw new InvalidOperationException("O Windows nao disponibilizou a criacao de atalhos.");
        }

        try
        {
            dynamic script = instance;
            dynamic shortcut = script.CreateShortcut(linkPath);
            shortcut.TargetPath = target;
            shortcut.WorkingDirectory = Path.GetDirectoryName(target);
            shortcut.Save();
        }
        finally
        {
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(instance);
        }
    }

    private static void WriteUninstallRegistry(
        InstallOptions options,
        string installDirectory,
        InstallationJournal journal)
    {
        using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(options.UninstallRegistryKey);
        if (key is null)
        {
            return;
        }

        key.SetValue("DisplayName", options.ProductName);
        key.SetValue("DisplayVersion", options.ProductVersion);
        key.SetValue("Publisher", options.Publisher);
        key.SetValue("InstallLocation", installDirectory);
        key.SetValue("UninstallString", options.UninstallCommand);
        key.SetValue("QuietUninstallString", options.UninstallCommand);
        key.SetValue(
            "DisplayIcon",
            Path.Combine(installDirectory, options.ExecutableName));
        key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
        key.SetValue("NoModify", 1);
        key.SetValue("NoRepair", 1);
        journal.CreatedRegistryKeys.Add(options.UninstallRegistryKey);
    }

    private void WriteUninstaller(
        string installDirectory,
        InstallOptions options,
        InstallationJournal journal)
    {
        // O desinstalador companion e um executavel pequeno, compilado em
        // .NET Framework, que vem junto com o instalador. Sem ele, o
        // proprio instalador assume o papel de desinstalador.
        var uninstaller = Path.Combine(installDirectory, "desinstalar.exe");

        try
        {
            var companion = Path.Combine(AppContext.BaseDirectory, "desinstalar.exe");

            if (File.Exists(companion))
            {
                File.Copy(companion, uninstaller, overwrite: true);
            }
            else
            {
                var source = Environment.ProcessPath;
                if (string.IsNullOrEmpty(source) || !File.Exists(source))
                {
                    return;
                }

                _log.Warn("O desinstalar companion nao foi encontrado, o instalador assume o desinstalador.");
                File.Copy(source, uninstaller, overwrite: true);
            }

            journal.InstalledFiles.Add(uninstaller);
        }
        catch (IOException exception)
        {
            _log.Warn("Nao foi possivel copiar o desinstalador: {0}", exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            _log.Warn("Nao foi possivel copiar o desinstalador: {0}", exception.Message);
        }
    }
}
