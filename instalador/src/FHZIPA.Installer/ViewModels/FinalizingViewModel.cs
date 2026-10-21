using System.Collections.ObjectModel;
using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de finalizacao: as ultimas verificacoes antes de declarar a
/// instalacao concluida.
/// </summary>
public sealed class FinalizingViewModel : StepViewModelBase
{
    private readonly IInstallationService _installation;

    public FinalizingViewModel(MainViewModel main, IInstallationService installation)
        : base(main)
    {
        _installation = installation;

        Checks =
        [
            new FinalizationCheck { Title = Strings.Get("Finalizing_Check1") },
            new FinalizationCheck { Title = Strings.Get("Finalizing_Check2") },
            new FinalizationCheck { Title = Strings.Get("Finalizing_Check3") },
        ];
    }

    public ObservableCollection<FinalizationCheck> Checks { get; }

    public string Title => Strings.Get("Finalizing_Title");

    public string Description => Strings.Get("Finalizing_Description");

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;

    /// <summary>
    /// Atualiza o estado de uma verificacao final.
    /// </summary>
    public void SetStatus(int index, RequirementStatus status, string detail)
    {
        if (index < 0 || index >= Checks.Count)
        {
            return;
        }

        Checks[index].Status = status;
        Checks[index].Detail = detail;
    }

    /// <summary>
    /// Roda as verificacoes finais de verdade: o executavel esta la, os
    /// atalhos foram criados e o desinstalador foi registrado.
    ///
    /// O resultado traz tambem quais verificacoes falharam, para que a tela
    /// de erro possa dizer o que ficou pendente em vez de so avisar que
    /// algo deu errado.
    /// </summary>
    public FinalizationReport RunChecks(string installDirectory)
    {
        var executable = Path.Combine(installDirectory, "fhzipa-inject.exe");
        var executableOk = File.Exists(executable);
        SetStatus(0, executableOk ? RequirementStatus.Passed : RequirementStatus.Failed,
            executableOk ? Path.GetFileName(executable) : "O executavel principal nao foi encontrado.");

        var shortcuts = new List<string>();
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "FHZIPA INJECT");

        if (File.Exists(Path.Combine(desktop, "FHZIPA INJECT.lnk")))
        {
            shortcuts.Add("area de trabalho");
        }

        if (File.Exists(Path.Combine(startMenu, "FHZIPA INJECT.lnk")))
        {
            shortcuts.Add("menu Iniciar");
        }

        SetStatus(1, shortcuts.Count > 0 ? RequirementStatus.Passed : RequirementStatus.Failed,
            shortcuts.Count > 0 ? string.Join(", ", shortcuts) : "Nenhum atalho foi criado.");

        var registered = _installation.IsRegistered();
        SetStatus(2, registered ? RequirementStatus.Passed : RequirementStatus.Failed,
            registered ? "Registrado em Aplicativos instalados" : "O desinstalador nao foi registrado.");

        var failed = Checks
            .Where(check => check.Status != RequirementStatus.Passed)
            .Select(check => check.Title)
            .ToArray();

        return new FinalizationReport(failed.Length == 0, failed);
    }
}

/// <summary>
/// O resultado das verificacoes finais.
/// </summary>
/// <param name="Passed">Verdadeiro quando todas as verificacoes passaram.</param>
/// <param name="FailedChecks">Os nomes das verificacoes que falharam.</param>
public sealed record FinalizationReport(bool Passed, IReadOnlyList<string> FailedChecks);

/// <summary>
/// Uma verificacao da finalizacao.
/// </summary>
public sealed class FinalizationCheck
{
    public string Title { get; init; } = string.Empty;

    public RequirementStatus Status { get; set; } = RequirementStatus.Pending;

    public string Detail { get; set; } = string.Empty;
}
