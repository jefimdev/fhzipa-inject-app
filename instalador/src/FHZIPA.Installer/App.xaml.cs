using System.Windows;
using System.Windows.Threading;
using FHZIPA.Installer.Infrastructure;
using FHZIPA.Installer.Security;
using FHZIPA.Installer.Services;
using FHZIPA.Installer.ViewModels;
using FHZIPA.Installer.Views;
using Microsoft.Extensions.DependencyInjection;

namespace FHZIPA.Installer;

/// <summary>
/// O ponto de partida do instalador.
/// </summary>
public partial class App : Application
{
    private SingleInstanceGuard? _guard;
    private Mutex? _uninstallMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

#if DEBUG
        EnableBindingDiagnostics();
#endif

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var arguments = e.Args;

        // O modo de limpeza: a copia do instalador espera o processo que a
        // chamou terminar e apaga a pasta de instalacao. Sem shell.
        if (arguments.Length >= 3 && arguments[0].Equals("--remove-folder", StringComparison.OrdinalIgnoreCase))
        {
            RunRemoveFolder(arguments[1], arguments[2]);
            Shutdown(0);
            return;
        }

        // O desinstalador e o proprio instalador, chamado com --uninstall.
        if (arguments.Any(argument => argument.Equals("--uninstall", StringComparison.OrdinalIgnoreCase)))
        {
            RunUninstaller();
            Shutdown(0);
            return;
        }

        _guard = new SingleInstanceGuard();
        if (!_guard.TryAcquire())
        {
            MessageBox.Show(
                "Outro instalador do FHZIPA INJECT ja esta aberto neste momento.",
                "FHZIPA INJECT",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown(2);
            return;
        }

        var services = BuildServices();

        var configuration = AppConfiguration.Load(Path.Combine(AppContext.BaseDirectory, "installer.config.json"));
        var log = services.GetRequiredService<ILogService>();
        log.Info("Instalador iniciado. Versao {0}", configuration.Application.Version);
        log.Info("Configuracao: padrao='{0}'", configuration.Installer.DefaultDirectory);

        var problems = configuration.Validate();
        if (problems.Count > 0)
        {
            foreach (var problem in problems)
            {
                log.Error("Configuracao invalida: {0}", problem);
            }

            MessageBox.Show(
                "A configuracao deste instalador esta invalida e ele nao pode continuar:\n\n" +
                string.Join("\n", problems) +
                "\n\nReinstale o instalador a partir do pacote original.",
                "FHZIPA INJECT",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(3);
            return;
        }

        var window = new MainWindow(log) { DataContext = services.GetRequiredService<MainViewModel>() };
        MainWindow = window;
        window.Show();

        var parsed = ParseArguments(arguments);
        log.Info("Argumentos recebidos: {0}", string.Join(" | ", arguments));
        log.Info("Argumentos interpretados: {0}", string.Join(" | ", parsed.Select(pair => $"{pair.Key}={pair.Value}")));

        if (window.DataContext is MainViewModel main)
        {
            if (parsed.ContainsKey("preview"))
            {
                main.IsPreview = true;
                log.Warn("MODO DE PREVIA: nenhuma instalacao sera feita de verdade.");
            }

            StartWithArgumentsAsync(main, parsed);
        }
    }

    /// <summary>
    /// O modo de limpeza: espera o processo indicado terminar e apaga a
    /// pasta de instalacao. E a copia do instalador, que roda da pasta
    /// temporaria e por isso consegue apagar o diretorio.
    /// </summary>
    private static void RunRemoveFolder(string directory, string parentPid)
    {
        try
        {
            if (int.TryParse(parentPid, out var pid))
            {
                using var parent = System.Diagnostics.Process.GetProcessById(pid);
                parent.WaitForExit();
            }
        }
        catch (ArgumentException)
        {
            // O processo que me chamou ja encerrou.
        }

        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A pasta pode ficar para tras, mas os arquivos importantes ja
            // foram removidos.
        }

        try
        {
            var self = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(self))
            {
                File.Delete(self);
            }
        }
        catch (Exception)
        {
            // A pasta temporaria e limpa pelo sistema.
        }
    }

    /// <summary>
    /// Aplica os argumentos da linha de comando. O instalador pode ser
    /// chamado ja com o diretorio escolhido, que e o que acontece quando
    /// ele e reaberto com permissao de administrador.
    /// </summary>
    private async void StartWithArgumentsAsync(MainViewModel main, Dictionary<string, string> parsed)
    {
        if (parsed.TryGetValue("desktop-shortcut", out var desktop))
        {
            main.Directory.CreateDesktopShortcut = desktop == "1";
        }

        if (parsed.TryGetValue("start-menu-shortcut", out var startMenu))
        {
            main.Directory.CreateStartMenuShortcut = startMenu == "1";
        }

        if (!parsed.TryGetValue("install-dir", out var directory) || string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        main.Directory.PresetDirectory(directory);
        await main.Directory.ContinueAsync().ConfigureAwait(true);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _guard?.Dispose();
        _uninstallMutex?.Dispose();
        base.OnExit(e);
    }

    /// <summary>
    /// Grava os problemas de associacao de dados em arquivo. So existe
    /// na compilacao de desenvolvimento.
    /// </summary>
    [System.Diagnostics.Conditional("DEBUG")]
    private static void EnableBindingDiagnostics()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FHZIPA INJECT",
            "Logs",
            "bindings.log");

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        System.Diagnostics.PresentationTraceSources.Refresh();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Clear();
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(
            new System.Diagnostics.TextWriterTraceListener(path));
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
            System.Diagnostics.SourceLevels.Warning | System.Diagnostics.SourceLevels.Error;
    }

    /// <summary>
    /// Monta os servicos do instalador.
    /// </summary>
    private static ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();

        var configuration = AppConfiguration.Load(Path.Combine(AppContext.BaseDirectory, "installer.config.json"));
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FHZIPA INJECT",
            "Logs");

        services.AddSingleton(configuration);
        services.AddSingleton<ILogService>(_ => new FileLogService(logDirectory));
        services.AddSingleton<IRequirementChecker, RequirementChecker>();
        services.AddSingleton<IInstallationService, InstallationService>();
        services.AddSingleton<IPayloadService, EmbeddedPayloadService>();
        services.AddSingleton<IProcessLauncher, ProcessLauncher>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IElevationService, ElevationService>();
        services.AddSingleton<IDownloadService>(provider => new DownloadService(
            provider.GetRequiredService<ILogService>(),
            TimeSpan.FromSeconds(configuration.Downloads.TimeoutSeconds)));

        services.AddSingleton<IViewLocator, ViewLocator>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Le os argumentos da linha de comando.
    /// </summary>
    private static Dictionary<string, string> ParseArguments(string[] arguments)
    {
        var parsed = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var argument in arguments)
        {
            if (!argument.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var body = argument[2..];
            var separator = body.IndexOf('=', StringComparison.Ordinal);

            // Uma opcao sem valor e uma chave liga/desliga: "--preview".
            if (separator < 0)
            {
                if (body.Length > 0)
                {
                    parsed[body] = "1";
                }

                continue;
            }

            if (separator == 0)
            {
                continue;
            }

            var key = body[..separator];
            var value = body[(separator + 1)..].Trim('"');
            parsed[key] = value;
        }

        return parsed;
    }

    /// <summary>
    /// O modo desinstalador: remove os arquivos, os atalhos e o registro.
    /// Os dados do usuario sao preservados.
    /// </summary>
    private void RunUninstaller()
    {
        _uninstallMutex = new Mutex(initiallyOwned: false, "FHZIPA.Installer.Uninstall");

        var answer = MessageBox.Show(
            "O FHZIPA INJECT e os atalhos serao removidos deste computador. Os seus dados e configuracoes ficam preservados. Deseja continuar?",
            "Desinstalar o FHZIPA INJECT",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);

        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var directory = AppContext.BaseDirectory;
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FHZIPA INJECT",
                "Logs");
            var log = new FileLogService(logDirectory);
            var service = new InstallationService(log);

            var shortcuts = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "FHZIPA INJECT.lnk"),
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Microsoft", "Windows", "Start Menu", "Programs", "FHZIPA INJECT", "FHZIPA INJECT.lnk"),
            };

            foreach (var shortcut in shortcuts)
            {
                try
                {
                    if (File.Exists(shortcut))
                    {
                        File.Delete(shortcut);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    log.Warn("Nao foi possivel remover o atalho {0}: {1}", shortcut, exception.Message);
                }
            }

            using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                       @"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true))
            {
                key?.DeleteSubKeyTree("FHZIPA INJECT", throwOnMissingSubKey: false);
            }

            var outcome = service
                .UninstallAsync(directory, CancellationToken.None)
                .GetAwaiter()
                .GetResult();

            if (outcome.Succeeded)
            {
                log.Info("Desinstalacao concluida.");
                MessageBox.Show(
                    "O FHZIPA INJECT foi removido deste computador.",
                    "FHZIPA INJECT",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "Nao foi possivel remover todos os arquivos. Feche o aplicativo e tente de novo.",
                    "FHZIPA INJECT",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"A desinstalacao falhou: {exception.Message}",
                "FHZIPA INJECT",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Um erro que escapa da interface nao derruba o instalador no meio de
    /// uma operacao sem explicar o que aconteceu.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FHZIPA INJECT",
            "Logs");

        try
        {
            new FileLogService(logDirectory).Error("Erro nao tratado: {0}", e.Exception);
        }
        catch (Exception)
        {
            // Um log que falha nao muda o que o usuario ve.
        }

        MessageBox.Show(
            $"Ocorreu um erro inesperado:\n\n{e.Exception.Message}\n\nO detalhe esta no arquivo de log.",
            "FHZIPA INJECT",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }
}
