using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela final: a instalacao terminou e o usuario decide se abre o
/// aplicativo agora.
/// </summary>
public sealed class OpenNowViewModel : StepViewModelBase
{
    private readonly IProcessLauncher _launcher;
    private readonly IInstallationService _installation;
    private readonly RelayCommand _openNowCommand;
    private readonly RelayCommand _notNowCommand;

    private bool _createDesktopShortcut;
    private string? _launchMessage;
    private string _executablePath = string.Empty;

    public OpenNowViewModel(
        MainViewModel main,
        IProcessLauncher launcher,
        IInstallationService installation)
        : base(main)
    {
        _launcher = launcher;
        _installation = installation;

        _openNowCommand = new RelayCommand(OpenNow);
        _notNowCommand = new RelayCommand(NotNow);
    }

    public string Title => Strings.Get("OpenNow_Title");

    public string Description => Strings.Get("OpenNow_Body");

    public string OpenNowText => Strings.Get("OpenNow_Open");

    public string NotNowText => Strings.Get("OpenNow_Not");

    public string ShortcutText => Strings.Get("OpenNow_Shortcut");

    /// <summary>
    /// A imagem e uma captura real do aplicativo, tirada do proprio
    /// executavel, e nao uma ilustracao.
    /// </summary>
    public string PreviewSource => "pack://application:,,,/FHZIPA_INJECT_Setup;component/Assets/app-preview.png";

    public RelayCommand OpenNowCommand => _openNowCommand;

    public RelayCommand NotNowCommand => _notNowCommand;

    public bool CreateDesktopShortcut
    {
        get => _createDesktopShortcut;
        set => SetProperty(ref _createDesktopShortcut, value);
    }

    public string? LaunchMessage
    {
        get => _launchMessage;
        private set
        {
            if (SetProperty(ref _launchMessage, value))
            {
                OnPropertyChanged(nameof(HasLaunchMessage));
            }
        }
    }

    public bool HasLaunchMessage => !string.IsNullOrEmpty(LaunchMessage);

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    /// <summary>
    /// Prepara a tela com o resultado da instalacao.
    /// </summary>
    public void Prepare(string executablePath, string installDirectory)
    {
        _executablePath = executablePath;
        CreateDesktopShortcut = false;
    }

    private void OpenNow()
    {
        // O atalho e criado de verdade quando a opcao esta marcada.
        if (CreateDesktopShortcut)
        {
            try
            {
                _installation.CreateDesktopShortcut(_executablePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                LaunchMessage = Strings.Get("OpenNow_ShortcutFailed");
            }
        }

        var outcome = _launcher
            .LaunchAsync(_executablePath, [], Main.Token)
            .GetAwaiter()
            .GetResult();

        if (!outcome.Succeeded)
        {
            LaunchMessage = Strings.Get("Success_LaunchFailed");
            return;
        }

        Main.Close();
    }

    private void NotNow() => Main.Close();

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;
}
