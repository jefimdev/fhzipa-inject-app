using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Security;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de escolha do diretorio de instalacao.
/// </summary>
public sealed class DirectoryViewModel : StepViewModelBase
{
    private readonly IDialogService _dialogs;
    private readonly IElevationService _elevation;
    private readonly RelayCommand _browseCommand;

    private string _installDirectory;
    private string? _pathProblem;
    private bool _createDesktopShortcut = true;
    private bool _createStartMenuShortcut = true;
    private long _availableBytes;
    private long _requiredBytes;

    public DirectoryViewModel(
        MainViewModel main,
        IDialogService dialogs,
        IElevationService elevation,
        string defaultDirectory)
        : base(main)
    {
        _dialogs = dialogs;
        _elevation = elevation;

        _installDirectory = string.IsNullOrWhiteSpace(defaultDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "FHZIPA INJECT")
            : defaultDirectory;

        _browseCommand = new RelayCommand(Browse);
        RefreshDiskInfo();
    }

    public string Title => Strings.Get("Directory_Title");

    public string Description => Strings.Get("Directory_Description");

    public string PathLabel => Strings.Get("Directory_Label");

    public string BrowseText => Strings.Get("Directory_Browse");

    public string NeededText => Strings.Get("Directory_Needed", MainViewModel.FormatBytes(_requiredBytes));

    public string AvailableText => Strings.Get("Directory_Available", MainViewModel.FormatBytes(_availableBytes));

    public string DesktopShortcutText => Strings.Get("Directory_DesktopShortcut");

    public string StartMenuShortcutText => Strings.Get("Directory_StartMenuShortcut");

    public string ExistingText => Strings.Get("Directory_Existing");

    public string ExistingTitle => Strings.Get("Directory_ExistingTitle");

    public string NextText => Strings.Get("Btn_Advance");

    public RelayCommand BrowseCommand => _browseCommand;

    public bool HasExistingInstall =>
        Directory.Exists(InstallDirectory) &&
        Directory.EnumerateFiles(InstallDirectory, "*.exe").Any();

    public string InstallDirectory
    {
        get => _installDirectory;
        set
        {
            if (SetProperty(ref _installDirectory, value))
            {
                Validate();
                RefreshDiskInfo();
                OnPropertyChanged(nameof(HasExistingInstall));
                OnPropertyChanged(nameof(NeededText));
                OnPropertyChanged(nameof(AvailableText));
                Main.RaiseCommandStates();
            }
        }
    }

    public bool CreateDesktopShortcut
    {
        get => _createDesktopShortcut;
        set => SetProperty(ref _createDesktopShortcut, value);
    }

    public bool CreateStartMenuShortcut
    {
        get => _createStartMenuShortcut;
        set => SetProperty(ref _createStartMenuShortcut, value);
    }

    public string? PathProblem
    {
        get => _pathProblem;
        private set
        {
            if (SetProperty(ref _pathProblem, value))
            {
                OnPropertyChanged(nameof(HasProblem));
            }
        }
    }

    public bool HasProblem => !string.IsNullOrEmpty(PathProblem);

    /// <summary>
    /// Verdadeiro quando o caminho escolhido esta em uma area que o
    /// Windows so libera para o administrador. Nao e um erro: o
    /// instalador pede a autorizacao no momento de avancar.
    /// </summary>
    public bool RequiresElevation => _elevation.RequiresElevation(InstallDirectory);

    /// <summary>
    /// O aviso de que o Windows vai pedir a autorizacao.
    /// </summary>
    public string ElevationNotice => Strings.Get("Directory_ElevationNotice");

    public bool HasElevationNotice => RequiresElevation && !_elevation.IsElevated;

    public override bool CanContinue => !HasProblem;

    public override bool CanGoBack => true;

    /// <summary>
    /// Ajusta o caminho inicial, quando o instalador foi aberto ja com um
    /// diretorio escolhido.
    /// </summary>
    public void PresetDirectory(string directory)
    {
        if (!string.IsNullOrWhiteSpace(directory))
        {
            InstallDirectory = directory;
        }
    }

    private void Browse()
    {
        var chosen = _dialogs.PickFolder(InstallDirectory);
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            InstallDirectory = chosen;
        }
    }

    private void Validate()
    {
        if (!PathValidator.IsValidInstallPath(InstallDirectory, out var problem))
        {
            PathProblem = problem;
            return;
        }

        // Uma pasta protegida nao e um erro: o instalador pede a
        // autorizacao do Windows no momento de avancar. So e problema
        // quando nem a elevacao resolve.
        if (!PathValidator.IsWritable(InstallDirectory) && !RequiresElevation)
        {
            PathProblem = Strings.Get("Directory_Invalid", "O Windows nao autorizou a escrita nesta pasta.");
            return;
        }

        PathProblem = null;
        OnPropertyChanged(nameof(RequiresElevation));
        OnPropertyChanged(nameof(HasElevationNotice));
        OnPropertyChanged(nameof(ElevationNotice));
    }

    private void RefreshDiskInfo()
    {
        _requiredBytes = 250L * 1024 * 1024;

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(InstallDirectory));
            if (!string.IsNullOrEmpty(root))
            {
                _availableBytes = new DriveInfo(root).AvailableFreeSpace;
            }
            else
            {
                _availableBytes = 0;
            }
        }
        catch (IOException)
        {
            _availableBytes = 0;
        }
        catch (UnauthorizedAccessException)
        {
            _availableBytes = 0;
        }
    }

    public override async Task ContinueAsync()
    {
        if (HasProblem)
        {
            return;
        }

        // A pasta de programas do Windows exige administrador. A elevacao
        // e pedida aqui, no momento exato em que ela e necessaria.
        //
        // Na previa nada e instalado, entao nao ha motivo para pedir
        // privilegio de administrador ao usuario.
        if (_elevation.RequiresElevation(InstallDirectory) && !Main.IsPreview)
        {
            var accepted = _dialogs.Confirm(
                Strings.Get("Elevation_Confirm_Title"),
                Strings.Get("Elevation_Confirm_Body"),
                Strings.Get("Btn_Install"),
                Strings.Get("Btn_Cancel"));

            if (!accepted)
            {
                return;
            }

            var arguments = BuildElevatedArguments();
            if (_elevation.RestartElevated(arguments))
            {
                Main.Close();
                return;
            }

            // A autorizacao foi recusada. Sem ela a instalacao nao pode
            // continuar, e o motivo fica claro na tela.
            Main.ShowError(
                InstallerError.Create(
                    InstallerErrorCode.InsufficientPermissions,
                    Strings.Get("Elevation_Declined")),
                canRetry: true,
                retry: ContinueAsync);
            return;
        }

        await Main.RunPipelineAsync().ConfigureAwait(true);
    }

    public override Task BackAsync()
    {
        Main.SetState(InstallerState.Welcome);
        Main.Show(Main.Welcome, Strings.Get("Btn_Install"));
        return Task.CompletedTask;
    }

    private string BuildElevatedArguments()
    {
        var shortcut = CreateDesktopShortcut ? "1" : "0";
        var startMenu = CreateStartMenuShortcut ? "1" : "0";
        return $"--install-dir=\"{InstallDirectory}\" --desktop-shortcut={shortcut} --start-menu-shortcut={startMenu}";
    }
}
