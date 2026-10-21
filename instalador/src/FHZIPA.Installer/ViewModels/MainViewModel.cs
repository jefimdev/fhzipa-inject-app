using System.Collections.ObjectModel;
using FHZIPA.Installer.Infrastructure;
using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;
using FHZIPA.Installer.Utilities;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// O orquestrador da instalacao. Ele guarda a maquina de estados, as
/// etapas do painel e o progresso global, e conduz a instalacao de ponta a
/// ponta.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    /// <summary>
    /// O progresso so chega a interface dez vezes por segundo. Alterar a
    /// interface a cada bloco de bytes causa salto de layout.
    /// </summary>
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);

    private readonly IRequirementChecker _requirements;
    private readonly IInstallationService _installation;
    private readonly IPayloadService _payload;
    private readonly IProcessLauncher _launcher;
    private readonly IDialogService _dialogs;
    private readonly IElevationService _elevation;
    private readonly ILogService _log;
    private readonly AppConfiguration _configuration;
    private readonly IViewLocator _viewLocator;

    private IStepViewModel _current = null!;
    private object _currentView = null!;
    private InstallerState _state = InstallerState.NotStarted;
    private bool _isBusy;
    private bool _progressVisible;
    private bool _progressIndeterminate;
    private double _progressPercent;
    private string _progressMessage = string.Empty;
    private string _progressDetail = string.Empty;
    private string _progressSpeed = string.Empty;
    private string _progressRemaining = string.Empty;
    private InstallerError? _error;
    private string _nextButtonText = string.Empty;
    private bool _showBack;
    private bool _showNext = true;
    private bool _showCancel = true;
    private Func<Task>? _retryAction;
    private bool _isWelcome = true;
    private bool _isPreview;
    private CancellationTokenSource _cancellation = new();
    private DateTimeOffset _lastProgressAt = DateTimeOffset.MinValue;
    private string _lastProgressStage = string.Empty;

    /// <summary>
    /// O progresso global em termos de trabalho real. A barra anda
    /// porque alguma coisa foi feita, e nao porque o tempo passou.
    /// </summary>
    private readonly InstallationProgressTracker _tracker = new();

    public MainViewModel(
        IRequirementChecker requirements,
        IInstallationService installation,
        IPayloadService payload,
        IProcessLauncher launcher,
        IDialogService dialogs,
        IElevationService elevation,
        ILogService log,
        AppConfiguration configuration,
        IViewLocator viewLocator)
    {
        _requirements = requirements;
        _installation = installation;
        _payload = payload;
        _launcher = launcher;
        _dialogs = dialogs;
        _elevation = elevation;
        _log = log;
        _configuration = configuration;
        _viewLocator = viewLocator;

        Welcome = new WelcomeViewModel(this);
        Directory = new DirectoryViewModel(this, dialogs, elevation, configuration.Installer.DefaultDirectory)
        {
            CreateDesktopShortcut = configuration.Installer.CreateDesktopShortcut,
            CreateStartMenuShortcut = configuration.Installer.CreateStartMenuShortcut,
        };
        Requirements = new RequirementsViewModel(this, requirements);
        Download = new DownloadViewModel(this, payload);
        Install = new InstallViewModel(this);
        Components = new ComponentsViewModel(this);
        Finalizing = new FinalizingViewModel(this, installation);
        Success = new SuccessViewModel(this, launcher);
        OpenNow = new OpenNowViewModel(this, launcher, installation);
        ErrorScreen = new ErrorViewModel(this, log);
        _tracker.PayloadBytes = payload.Size;

        Steps = [.. InstallationSteps.All.Select(step => new StepItem
        {
            Step = step.Step,
            Title = Strings.Get($"Step_{step.Step}"),
            Status = StepStatus.Pending,
        })];

        Features =
        [
            new FeatureItem
            {
                Icon = FeatureIcon.Shield,
                Title = Welcome.Feature1Title,
                Detail = Welcome.Feature1Body,
            },
            new FeatureItem
            {
                Icon = FeatureIcon.Bolt,
                Title = Welcome.Feature2Title,
                Detail = Welcome.Feature2Body,
            },
            new FeatureItem
            {
                Icon = FeatureIcon.Gear,
                Title = Welcome.Feature3Title,
                Detail = Welcome.Feature3Body,
            },
        ];

        NextCommand = new AsyncRelayCommand(ContinueAsync, () => CanContinue);
        BackCommand = new AsyncRelayCommand(BackAsync, () => CanGoBack);
        CancelCommand = new RelayCommand(Cancel, () => ShowCancel);

        SetState(InstallerState.Welcome);
        Show(Welcome, Strings.Get("Btn_Install"));
    }

    public WelcomeViewModel Welcome { get; }

    public DirectoryViewModel Directory { get; }

    public RequirementsViewModel Requirements { get; }

    public DownloadViewModel Download { get; }

    public InstallViewModel Install { get; }

    public ComponentsViewModel Components { get; }

    public FinalizingViewModel Finalizing { get; }

    public SuccessViewModel Success { get; }

    public OpenNowViewModel OpenNow { get; }

    /// <summary>
    /// A tela de erro, que explica o que aconteceu.
    /// </summary>
    public ErrorViewModel ErrorScreen { get; }

    public ObservableCollection<StepItem> Steps { get; }

    /// <summary>
    /// Verdadeiro quando o instalador esta no modo de previa. Nesse modo
    /// nada e instalado: o fluxo e simulado e a interface avisa isso o
    /// tempo todo.
    /// </summary>
    public bool IsPreview
    {
        get => _isPreview;
        set
        {
            if (SetProperty(ref _isPreview, value))
            {
                OnPropertyChanged(nameof(PreviewBannerText));
            }
        }
    }

    /// <summary>
    /// O aviso do modo de previa, mostrado no topo da janela.
    /// </summary>
    public string PreviewBannerText => Strings.Get("Preview_Banner");

    /// <summary>
    /// O titulo da janela. Na previa ele deixa claro que nada sera
    /// instalado de verdade.
    /// </summary>
    public string WindowTitle => IsPreview
        ? Strings.Get("Preview_WindowTitle")
        : Strings.Get("Window_Title");

    /// <summary>
    /// Os destaques mostrados no painel lateral na tela de boas-vindas.
    /// </summary>
    public ObservableCollection<FeatureItem> Features { get; } = [];

    public string ProductName => _configuration.Application.Name;

    /// <summary>
    /// O subtitulo da marca, com as letras espacadas.
    /// </summary>
    public string BrandSubtitle => TextSpacing.Space(Strings.Get("Brand_Subtitle"));

    public string VersionText => Strings.Get("Footer_Version", _configuration.Application.Version);

    public string FooterText => Strings.Get("Footer_Local");

    public string CancelButtonText => Strings.Get("Btn_Cancel");

    /// <summary>
    /// Verdadeiro apenas na tela de boas-vindas: e la que o painel
    /// lateral mostra os destaques em vez das etapas.
    /// </summary>
    public bool IsWelcome
    {
        get => _isWelcome;
        private set => SetProperty(ref _isWelcome, value);
    }

    public string Feature1Title => Welcome.Feature1Title;

    public string Feature1Body => Welcome.Feature1Body;

    public string Feature2Title => Welcome.Feature2Title;

    public string Feature2Body => Welcome.Feature2Body;

    public string Feature3Title => Welcome.Feature3Title;

    public string Feature3Body => Welcome.Feature3Body;

    /// <summary>
    /// A porcentagem em texto, como na referencia.
    /// </summary>
    public string ProgressPercentText =>
        ProgressPercent.ToString("0", System.Globalization.CultureInfo.CurrentCulture) + "%";

    /// <summary>
    /// A tela que esta na frente.
    /// </summary>
    public object CurrentView
    {
        get => _currentView;
        private set => SetProperty(ref _currentView, value);
    }

    /// <summary>
    /// A etapa da maquina de estados.
    /// </summary>
    public InstallerState State
    {
        get => _state;
        private set => SetProperty(ref _state, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool ProgressVisible
    {
        get => _progressVisible;
        private set => SetProperty(ref _progressVisible, value);
    }

    public bool ProgressIndeterminate
    {
        get => _progressIndeterminate;
        private set => SetProperty(ref _progressIndeterminate, value);
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string ProgressMessage
    {
        get => _progressMessage;
        private set => SetProperty(ref _progressMessage, value);
    }

    public string ProgressDetail
    {
        get => _progressDetail;
        private set => SetProperty(ref _progressDetail, value);
    }

    public InstallerError? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => Error is not null;

    public string NextButtonText
    {
        get => _nextButtonText;
        private set => SetProperty(ref _nextButtonText, value);
    }

    public string BackButtonText => Strings.Get("Btn_Back");

    public bool ShowBack
    {
        get => _showBack;
        private set => SetProperty(ref _showBack, value);
    }

    public bool ShowNext
    {
        get => _showNext;
        private set => SetProperty(ref _showNext, value);
    }

    public bool ShowCancel
    {
        get => _showCancel;
        private set
        {
            if (SetProperty(ref _showCancel, value))
            {
                RaiseCommandStates();
            }
        }
    }

    public bool CanContinue => _current.CanContinue && !IsBusy;

    public bool CanGoBack => _current.CanGoBack && !IsBusy;

    public AsyncRelayCommand NextCommand { get; }

    public AsyncRelayCommand BackCommand { get; }

    public RelayCommand CancelCommand { get; }

    /// <summary>
    /// Avisa a interface quando as acoes mudam de estado.
    /// </summary>
    public void RaiseCommandStates()
    {
        NextCommand.RaiseCanExecuteChanged();
        BackCommand.RaiseCanExecuteChanged();
        CancelCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Troca de tela e alinha os botoes do rodape com a tela nova. A tela
    /// e reaproveitada, nunca recriada.
    /// </summary>
    public void Show(
        IStepViewModel viewModel,
        string? nextText = null,
        bool showNext = true,
        bool showCancel = true)
    {
        _current = viewModel;
        IsWelcome = ReferenceEquals(viewModel, Welcome);
        CurrentView = _viewLocator.Resolve(viewModel);
        NextButtonText = nextText ?? Strings.Get("Btn_Next");
        ShowBack = viewModel.CanGoBack;
        ShowNext = showNext;
        ShowCancel = showCancel;
        RaiseCommandStates();
    }

    /// <summary>
    /// Muda a etapa da maquina de estados, recusando transicoes invalidas.
    /// </summary>
    public void SetState(InstallerState next)
    {
        if (State == next)
        {
            return;
        }

        if (!InstallerStateMachine.CanTransition(State, next))
        {
            _log.Warn("Transicao recusada: {0} para {1}", State, next);
            return;
        }

        _log.Info("Etapa: {0}", next);
        State = next;
    }

    /// <summary>
    /// Marca uma etapa do painel esquerdo.
    /// </summary>
    public void SetStep(InstallationStep step, StepStatus status, string message = "")
    {
        // Uma atualizacao que chega depois da conclusao nao mexe em uma
        // etapa ja encerrada.
        if (State is InstallerState.Completed or InstallerState.Cancelled)
        {
            return;
        }

        var item = Steps.FirstOrDefault(entry => entry.Step == step);
        if (item is null)
        {
            return;
        }

        item.Status = status;
        if (!string.IsNullOrEmpty(message))
        {
            item.Message = message;
        }
    }

    /// <summary>
    /// Recebe o progresso global das operacoes. As atualizacoes sao
    /// limitadas para a interface nao ficar saltando.
    ///
    /// A porcentagem vem do rastreador, que so conta trabalho feito de
    /// verdade: bytes extraidos, bytes copiados, operacoes de registro e
    /// verificacoes finais. A velocidade e o tempo restante vem da
    /// medicao de bytes da operacao em andamento.
    /// </summary>
    public void ReportProgress(InstallerProgress progress)
    {
        var stageChanged = !string.Equals(progress.CurrentStage, _lastProgressStage, StringComparison.Ordinal);
        var now = DateTimeOffset.UtcNow;

        if (!stageChanged && (now - _lastProgressAt) < ProgressInterval)
        {
            return;
        }

        _lastProgressAt = now;
        _lastProgressStage = progress.CurrentStage;

        ProgressVisible = true;
        ProgressMessage = progress.CurrentMessage;
        ProgressIndeterminate = progress.IsIndeterminate;

        ApplyStageToTracker(progress);

        ProgressPercent = _tracker.Percentage;

        if (progress.TotalBytes > 0)
        {
            ProgressDetail = Strings.Get(
                "Download_Bytes",
                FormatBytes(progress.BytesProcessed),
                FormatBytes(progress.TotalBytes));
        }
        else
        {
            ProgressDetail = progress.CurrentStage;
        }

        ProgressSpeed = progress.BytesPerSecond > 0
            ? FormatBytes((long)progress.BytesPerSecond) + "/s"
            : "--";

        ProgressRemaining = progress.EstimatedTimeRemaining is { } remaining
            ? FormatDuration(remaining)
            : "--";
    }

    /// <summary>
    /// Traduz a etapa real em parcelas de trabalho do rastreador.
    /// </summary>
    private void ApplyStageToTracker(InstallerProgress progress)
    {
        switch (progress.CurrentStage)
        {
            case "Baixando":
                _tracker.ObtainedBytes = progress.BytesProcessed;
                break;

            case "Instalando":
                if (progress.TotalBytes > 0)
                {
                    _tracker.CopiedBytes = progress.BytesProcessed;
                }

                break;

            case "Criando atalhos":
            case "Registrando o aplicativo":
            case "Escrevendo o desinstalador":
                _tracker.RegisteringStepDone();
                break;

            case "Finalizando":
                _tracker.FinalCheckDone();
                break;
        }
    }

    /// <summary>
    /// A velocidade medida da operacao em andamento.
    /// </summary>
    public string ProgressSpeed
    {
        get => _progressSpeed;
        private set => SetProperty(ref _progressSpeed, value);
    }

    /// <summary>
    /// O tempo restante estimado, quando ha velocidade confiavel.
    /// </summary>
    public string ProgressRemaining
    {
        get => _progressRemaining;
        private set => SetProperty(ref _progressRemaining, value);
    }

    /// <summary>
    /// Atualiza a mensagem de progresso de fora da tela.
    /// </summary>
    public void ReportStatus(string message)
    {
        ProgressVisible = true;
        ProgressMessage = message;
    }

    /// <summary>
    /// Espera um instante antes de mostrar o resultado de uma verificacao,
    /// para o usuario enxergar o que aconteceu.
    /// </summary>
    public static Task BriefPauseAsync() => Task.Delay(450);

    /// <summary>
    /// A acao do botao principal.
    /// </summary>
    public async Task ContinueAsync()
    {
        Error = null;

        if (_retryAction is not null)
        {
            var action = _retryAction;
            _retryAction = null;
            await action().ConfigureAwait(true);
            return;
        }

        await _current.ContinueAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// A acao do botao voltar.
    /// </summary>
    public async Task BackAsync()
    {
        Error = null;
        await _current.BackAsync().ConfigureAwait(true);
    }

    /// <summary>
    /// Define a acao do botao principal para uma nova tentativa.
    /// </summary>
    public void SetRetryAction(Func<Task> action) => _retryAction = action;

    /// <summary>
    /// Mostra a tela de erro: o que aconteceu, o codigo, o que a pessoa
    /// pode fazer e onde esta o log. A repeticao so fica disponivel
    /// quando a operacao realmente pode ser repetida.
    /// </summary>
    public void ShowError(InstallerError error, bool canRetry, Func<Task>? retry = null)
    {
        Error = error;
        ErrorScreen.Prepare(error, canRetry);

        _retryAction = canRetry ? retry : null;

        ProgressVisible = false;
        IsBusy = false;
        ShowBack = false;
        ShowNext = canRetry;
        ShowCancel = true;
        NextButtonText = Strings.Get("Btn_Retry");

        Show(ErrorScreen, Strings.Get("Btn_Retry"), showNext: canRetry, showCancel: true);
        RaiseCommandStates();
    }

    /// <summary>
    /// O cancelamento. Durante uma operacao, interrompe e desfaz; fora
    /// dela, fecha o instalador.
    /// </summary>
    public void Cancel()
    {
        if (IsBusy)
        {
            _cancellation.Cancel();
            ProgressMessage = Strings.Get("Cancel_Confirm_Title");
            return;
        }

        if (State is InstallerState.Completed)
        {
            Close();
            return;
        }

        if (_dialogs.Confirm(
                Strings.Get("Cancel_Confirm_Title"),
                Strings.Get("Cancel_Confirm_Body"),
                Strings.Get("Cancel_Confirm_Yes"),
                Strings.Get("Cancel_Confirm_No")))
        {
            Close();
        }
    }

    /// <summary>
    /// O pedido de fechar o instalador.
    /// </summary>
    public event EventHandler? RequestClose;

    /// <summary>
    /// Pede o fechamento do instalador.
    /// </summary>
    public void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// O token da operacao em andamento.
    /// </summary>
    public CancellationToken Token => _cancellation.Token;

    /// <summary>
    /// Zera o token de cancelamento para uma nova tentativa.
    /// </summary>
    public void ResetCancellation()
    {
        if (!_cancellation.IsCancellationRequested)
        {
            return;
        }

        _cancellation.Dispose();
        _cancellation = new CancellationTokenSource();
    }

    /// <summary>
    /// Formata um tamanho de arquivo no padrao brasileiro.
    /// </summary>
    public static string FormatBytes(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} B"
            : string.Format(System.Globalization.CultureInfo.CurrentCulture, "{0:0.#} {1}", value, units[unit]);
    }

    /// <summary>
    /// Formata um intervalo de tempo de um jeito curto.
    /// </summary>
    public static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalHours >= 1)
        {
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                "{0:0}h {1:00}min",
                (int)duration.TotalHours,
                duration.Minutes);
        }

        if (duration.TotalMinutes >= 1)
        {
            return string.Format(
                System.Globalization.CultureInfo.CurrentCulture,
                "{0:0}min {1:00}s",
                (int)duration.TotalMinutes,
                duration.Seconds);
        }

        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            "{0:0}s",
            Math.Max(1, (int)duration.TotalSeconds));
    }
}
