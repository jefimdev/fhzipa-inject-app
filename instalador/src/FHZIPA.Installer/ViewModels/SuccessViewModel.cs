using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de sucesso: a instalacao terminou.
/// </summary>
public sealed class SuccessViewModel : StepViewModelBase
{
    private readonly IProcessLauncher _launcher;
    private readonly RelayCommand _openCommand;
    private readonly RelayCommand _finishCommand;

    private string _versionText = string.Empty;
    private string _installedText = string.Empty;
    private string? _launchMessage;

    public SuccessViewModel(MainViewModel main, IProcessLauncher launcher)
        : base(main)
    {
        _launcher = launcher;

        _openCommand = new RelayCommand(Open);
        _finishCommand = new RelayCommand(Finish);
    }

    public string Title => Strings.Get("Success_Title");

    public string Body => Strings.Get("Success_Body");

    public string OpenButtonText => Strings.Get("Success_Open");

    public string FinishButtonText => Strings.Get("Success_Finish");

    public string ConfirmText => Strings.Get("Success_Confirm");

    public string VersionText
    {
        get => _versionText;
        private set => SetProperty(ref _versionText, value);
    }

    public string InstalledText
    {
        get => _installedText;
        private set => SetProperty(ref _installedText, value);
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

    /// <summary>
    /// O botao grande leva para a tela que pergunta se o aplicativo deve
    /// ser aberto agora.
    /// </summary>
    public RelayCommand OpenCommand => _openCommand;

    /// <summary>
    /// O botao escuro encerra o instalador.
    /// </summary>
    public RelayCommand FinishCommand => _finishCommand;

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    /// <summary>
    /// Prepara a tela com o resultado da instalacao.
    /// </summary>
    public void Prepare(string version, string installDirectory, string executablePath)
    {
        VersionText = Strings.Get("Success_Version", version);
        InstalledText = Strings.Get("Success_Installed", installDirectory);
    }

    private void Open()
    {
        Main.Show(Main.OpenNow, showNext: false, showCancel: false);
        Main.OpenNow.Prepare(
            Path.Combine(Main.Directory.InstallDirectory, "fhzipa-inject.exe"),
            Main.Directory.InstallDirectory);
    }

    private void Finish() => Main.Close();

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;
}
