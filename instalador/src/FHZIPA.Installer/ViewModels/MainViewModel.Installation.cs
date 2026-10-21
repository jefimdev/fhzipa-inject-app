using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// O fluxo da instalacao: da verificacao do sistema ate a tela final.
///
/// Cada etapa avanca so quando a anterior termina bem, e a tela que
/// aparece acompanha a etapa real do trabalho.
///
/// O progresso mostrado vem do trabalho de verdade: bytes extraidos do
/// instalador, bytes gravados no destino, operacoes de registro e
/// verificacoes finais. Nada aqui avanca por temporizador.
/// </summary>
public sealed partial class MainViewModel
{
    /// <summary>
    /// A instalacao inteira. Cada etapa so avanca quando a anterior
    /// termina bem.
    /// </summary>
    public async Task RunPipelineAsync()
    {
        if (IsPreview)
        {
            await RunPreviewPipelineAsync().ConfigureAwait(true);
            return;
        }

        ResetCancellation();
        Error = null;
        IsBusy = true;
        ShowBack = false;
        ShowNext = false;
        ProgressVisible = true;
        ProgressIndeterminate = true;
        ProgressPercent = 0;
        ProgressMessage = string.Empty;
        ProgressDetail = string.Empty;
        ProgressSpeed = "--";
        ProgressRemaining = "--";
        _tracker.Reset();
        _tracker.PayloadBytes = _payload.Size;

        var token = Token;
        var workingDirectory = Path.Combine(
            Path.GetTempPath(),
            "FHZIPA INJECT",
            Guid.NewGuid().ToString("N"));

        var endpoints = _configuration.Downloads.ManifestUrl is { Length: > 0 } manifestUrl
            ? new[] { manifestUrl }
            : Array.Empty<string>();

        try
        {
            // ---- Preparando
            SetStep(InstallationStep.Preparing, StepStatus.Running, Strings.Get("Step_Preparing"));
            ProgressMessage = Strings.Get("Step_Preparing");
            await BriefPauseAsync().ConfigureAwait(true);

            // ---- Verificacao do sistema
            SetStep(InstallationStep.Preparing, StepStatus.Done);
            SetState(InstallerState.DirectorySelection);
            SetState(InstallerState.CheckingRequirements);
            Show(Requirements);
            SetStep(InstallationStep.Verifying, StepStatus.Running, Strings.Get("Step_Verifying"));
            ProgressMessage = Strings.Get("Step_Verifying");

            await Requirements.RunAsync(Directory.InstallDirectory, endpoints, token).ConfigureAwait(true);
            SetStep(InstallationStep.Verifying, StepStatus.Done);

            if (Requirements.HasCriticalFailure)
            {
                SetState(InstallerState.RequirementsFailed);
                IsBusy = false;
                ProgressVisible = false;
                ShowBack = true;
                ShowNext = false;
                RaiseCommandStates();
                return;
            }

            await BriefPauseAsync().ConfigureAwait(true);

            // ---- Obtencao do pacote
            SetState(InstallerState.ReadyToDownload);
            Show(Download);
            SetStep(InstallationStep.Downloading, StepStatus.Running, Strings.Get("Step_Downloading"));
            SetState(InstallerState.Downloading);
            ProgressMessage = Strings.Get("Step_Downloading");

            var payloadPath = await Download.RunAsync(workingDirectory, token).ConfigureAwait(true);
            SetStep(InstallationStep.Downloading, StepStatus.Done);

            // ---- Validacao do pacote
            SetState(InstallerState.ValidatingDownloads);
            SetState(InstallerState.ReadyToInstall);

            // ---- Instalacao
            Show(Install);
            SetStep(InstallationStep.Installing, StepStatus.Running, Strings.Get("Step_Installing"));
            SetState(InstallerState.Installing);

            var installProgress = new Progress<InstallerProgress>(report =>
            {
                ReportProgress(report);
                ApplyInstallStage(report.CurrentStage);
            });

            var outcome = await _installation
                .InstallAsync(
                    payloadPath,
                    Directory.InstallDirectory,
                    new InstallOptions(
                        _configuration.Application.ExecutableName,
                        Directory.CreateDesktopShortcut,
                        Directory.CreateStartMenuShortcut,
                        _payload.ExpectedSha256 ?? string.Empty,
                        InstallationService.UninstallRegistryKeyName,
                        $"\"{Path.Combine(Directory.InstallDirectory, "desinstalar.exe")}\" --uninstall")
                    {
                        ProductName = _configuration.Application.Name,
                        ProductVersion = _configuration.Application.Version,
                        Publisher = _configuration.Application.Publisher,
                    },
                    installProgress,
                    token)
                .ConfigureAwait(true);

            if (!outcome.Succeeded)
            {
                SetState(InstallerState.Failed);
                ShowError(
                    outcome.Error ?? InstallerError.Create(InstallerErrorCode.CopyFailed),
                    canRetry: true,
                    retry: RunPipelineAsync);
                return;
            }

            SetStep(InstallationStep.Installing, StepStatus.Done);
            SetStep(InstallationStep.Configuring, StepStatus.Done);

            // As verificacoes finais, com o resultado na tela antes de
            // passar para a conclusao. Uma verificacao que falha impede
            // a declaracao de sucesso.
            Show(Finalizing, showNext: false, showCancel: false);
            SetState(InstallerState.Finalizing);
            SetStep(InstallationStep.Finalizing, StepStatus.Running, Strings.Get("Step_Finalizing"));
            ProgressMessage = Strings.Get("Step_Finalizing");
            ProgressIndeterminate = true;

            var report = Finalizing.RunChecks(Directory.InstallDirectory);
            await BriefPauseAsync().ConfigureAwait(true);

            if (!report.Passed)
            {
                SetState(InstallerState.Failed);
                ShowError(
                    InstallerError.Create(
                        InstallerErrorCode.FinalizationFailed,
                        string.Join("; ", report.FailedChecks)),
                    canRetry: true,
                    retry: RunPipelineAsync);
                return;
            }

            SetStep(InstallationStep.Finalizing, StepStatus.Done);

            // ---- Conclusao
            SetState(InstallerState.Completed);
            Show(Success);
            Success.Prepare(
                _configuration.Application.Version,
                Directory.InstallDirectory,
                Path.Combine(Directory.InstallDirectory, "fhzipa-inject.exe"));

            ProgressVisible = false;
            ProgressPercent = 100;
            ShowNext = false;
            ShowBack = false;
            ShowCancel = false;
            IsBusy = false;
            RaiseCommandStates();
        }
        catch (OperationCanceledException)
        {
            SetState(InstallerState.Cancelled);
            SetStep(InstallationStep.Preparing, StepStatus.Pending);
            ShowError(
                InstallerError.Create(InstallerErrorCode.CancelledByUser),
                canRetry: true,
                retry: RunPipelineAsync);
        }
        catch (Exception exception)
        {
            _log.Error("Falha na instalacao: {0}", exception);
            SetState(InstallerState.Failed);
            ShowError(
                InstallerError.Create(InstallerErrorCode.FinalizationFailed, exception.Message),
                canRetry: true,
                retry: RunPipelineAsync);
        }
        finally
        {
            CleanUp(workingDirectory);
        }
    }

    /// <summary>
    /// O modo de previa: percorre as mesmas telas, com eventos simulados,
    /// sem tocar em nenhum arquivo, atalho ou chave de registro. A
    /// interface avisa o tempo todo que e uma previa.
    /// </summary>
    private async Task RunPreviewPipelineAsync()
    {
        ResetCancellation();
        Error = null;
        IsBusy = true;
        ShowBack = false;
        ShowNext = false;
        ProgressVisible = true;
        ProgressIndeterminate = true;
        ProgressPercent = 0;
        _tracker.Reset();
        _tracker.PayloadBytes = _payload.Size;

        var token = Token;

        try
        {
            SetStep(InstallationStep.Preparing, StepStatus.Running, Strings.Get("Step_Preparing"));
            ProgressMessage = Strings.Get("Preview_Simulated");
            await BriefPauseAsync().ConfigureAwait(true);

            SetStep(InstallationStep.Preparing, StepStatus.Done);
            SetState(InstallerState.DirectorySelection);
            SetState(InstallerState.CheckingRequirements);
            Show(Requirements);
            SetStep(InstallationStep.Verifying, StepStatus.Running, Strings.Get("Step_Verifying"));
            await BriefPauseAsync().ConfigureAwait(true);
            SetStep(InstallationStep.Verifying, StepStatus.Done);

            SetState(InstallerState.ReadyToDownload);
            Show(Download);
            SetState(InstallerState.Downloading);
            SetStep(InstallationStep.Downloading, StepStatus.Running, Strings.Get("Step_Downloading"));
            await SimulateTransferAsync(token).ConfigureAwait(true);
            SetStep(InstallationStep.Downloading, StepStatus.Done);

            SetState(InstallerState.ValidatingDownloads);
            SetState(InstallerState.ReadyToInstall);

            Show(Install);
            SetState(InstallerState.Installing);
            SetStep(InstallationStep.Installing, StepStatus.Running, Strings.Get("Step_Installing"));
            await SimulateTransferAsync(token).ConfigureAwait(true);
            SetStep(InstallationStep.Installing, StepStatus.Done);

            Show(Components, showNext: false, showCancel: false);
            SetState(InstallerState.InstallingDependencies);
            SetState(InstallerState.Configuring);
            SetStep(InstallationStep.Configuring, StepStatus.Running, Strings.Get("Step_Configuring"));
            Components.SetStatus("app", ComponentStatus.Done);
            Components.SetStatus("shortcuts", ComponentStatus.Installing);
            await BriefPauseAsync().ConfigureAwait(true);
            Components.SetStatus("shortcuts", ComponentStatus.Done);
            Components.SetStatus("registry", ComponentStatus.Done);
            Components.SetStatus("uninstaller", ComponentStatus.Installing);
            await BriefPauseAsync().ConfigureAwait(true);
            Components.SetStatus("uninstaller", ComponentStatus.Done);
            SetStep(InstallationStep.Configuring, StepStatus.Done);

            Show(Finalizing, showNext: false, showCancel: false);
            SetState(InstallerState.Finalizing);
            SetStep(InstallationStep.Finalizing, StepStatus.Running, Strings.Get("Step_Finalizing"));
            await BriefPauseAsync().ConfigureAwait(true);
            SetStep(InstallationStep.Finalizing, StepStatus.Done);

            SetState(InstallerState.Completed);
            Show(Success);
            Success.Prepare(
                _configuration.Application.Version,
                Directory.InstallDirectory,
                Path.Combine(Directory.InstallDirectory, "fhzipa-inject.exe"));

            ProgressVisible = false;
            ProgressPercent = 100;
            ShowNext = false;
            ShowBack = false;
            ShowCancel = false;
            IsBusy = false;
            RaiseCommandStates();
        }
        catch (OperationCanceledException)
        {
            SetState(InstallerState.Cancelled);
            ShowError(
                InstallerError.Create(InstallerErrorCode.CancelledByUser),
                canRetry: true,
                retry: RunPipelineAsync);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Simula a transferencia de um arquivo, marking cada avanco como
    /// simulado no texto. O numero de bytes e o mesmo do payload, para
    /// que a previa mostre a mesma escala, mas nada e lido nem gravado.
    /// </summary>
    private async Task SimulateTransferAsync(CancellationToken token)
    {
        var total = Math.Max(_payload.Size, 1);
        var steps = 24;

        for (var step = 1; step <= steps; step++)
        {
            token.ThrowIfCancellationRequested();

            var processed = (long)(total * step / steps);
            var report = new InstallerProgress
            {
                OperationName = Strings.Get("Preview_Operation"),
                CurrentStage = step <= steps / 2 ? "Baixando" : "Instalando",
                CurrentMessage = Strings.Get("Preview_Simulated"),
                BytesProcessed = processed,
                TotalBytes = total,
                BytesPerSecond = total / 12d,
                EstimatedTimeRemaining = TimeSpan.FromSeconds(Math.Max(1, 12 - step)),
            };

            ReportProgress(report);
            Download.Report(report);

            await Task.Delay(60, token).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Traduz a etapa informada pelo motor de instalacao em etapas da
    /// maquina de estados, do painel e das telas.
    /// </summary>
    private void ApplyInstallStage(string stage)
    {
        switch (stage)
        {
            case "Preparando":
                break;

            case "Validando":
                Install.CompleteOperation("prepare");
                break;

            case "Instalando":
                Install.CompleteOperation("extract");
                SetStep(InstallationStep.Installing, StepStatus.Running, stage);
                break;

            case "Criando atalhos":
                Install.CompleteOperation("copy");
                SetStep(InstallationStep.Installing, StepStatus.Done, stage);
                SetState(InstallerState.InstallingDependencies);
                SetState(InstallerState.Configuring);
                SetStep(InstallationStep.Configuring, StepStatus.Running, stage);
                Components.SetStatus("app", ComponentStatus.Done);
                Components.SetStatus("shortcuts", ComponentStatus.Installing);
                Show(Components, showNext: false, showCancel: false);
                break;

            case "Registrando o aplicativo":
                Components.SetStatus("shortcuts", ComponentStatus.Done);
                Components.SetStatus("registry", ComponentStatus.Installing);
                break;

            case "Escrevendo o desinstalador":
                Components.SetStatus("registry", ComponentStatus.Done);
                Components.SetStatus("uninstaller", ComponentStatus.Installing);
                break;

            case "Finalizando":
                Components.SetStatus("uninstaller", ComponentStatus.Done);
                SetStep(InstallationStep.Configuring, StepStatus.Done, stage);
                Show(Finalizing, showNext: false, showCancel: false);
                SetState(InstallerState.Finalizing);
                SetStep(InstallationStep.Finalizing, StepStatus.Running, stage);
                break;
        }
    }

    /// <summary>
    /// Apaga os temporarios que o instalador criou.
    /// </summary>
    private void CleanUp(string workingDirectory)
    {
        try
        {
            if (System.IO.Directory.Exists(workingDirectory))
            {
                System.IO.Directory.Delete(workingDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _log.Warn("Nao foi possivel apagar {0}: {1}", workingDirectory, exception.Message);
        }
    }
}
