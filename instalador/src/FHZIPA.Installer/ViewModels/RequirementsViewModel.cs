using System.Collections.ObjectModel;
using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de verificacao do sistema.
/// </summary>
public sealed class RequirementsViewModel : StepViewModelBase
{
    private static readonly (string Id, string Title)[] Checks =
    [
        ("os", "Sistema operacional"),
        ("espaco", "Espaco em disco"),
        ("permissoes", "Permissoes de administrador"),
        ("webview2", "Dependencias"),
        ("rede", "Conexao com a internet"),
    ];

    private readonly IRequirementChecker _checker;
    private readonly AsyncRelayCommand _retryCommand;

    private bool _hasCriticalFailure;
    private bool _hasFinished;
    private string _summary = string.Empty;

    public RequirementsViewModel(MainViewModel main, IRequirementChecker? checker = null)
        : base(main)
    {
        _checker = checker ?? throw new ArgumentNullException(nameof(checker));
        _retryCommand = new AsyncRelayCommand(() => Main.RunPipelineAsync());
        Results = [];

        foreach (var check in Checks)
        {
            Results.Add(new RequirementResult(check.Id, check.Title, RequirementStatus.Running, "Verificando…"));
        }
    }

    public ObservableCollection<RequirementResult> Results { get; }

    /// <summary>
    /// Roda as verificacoes de novo.
    /// </summary>
    public AsyncRelayCommand RetryCommand => _retryCommand;

    public string Title => Strings.Get("Requirements_Title");

    public string Description => Strings.Get("Requirements_Description");

    public string RetryText => Strings.Get("Requirements_Retry");

    public bool HasCriticalFailure
    {
        get => _hasCriticalFailure;
        private set => SetProperty(ref _hasCriticalFailure, value);
    }

    public bool HasFinished
    {
        get => _hasFinished;
        private set => SetProperty(ref _hasFinished, value);
    }

    public string Summary
    {
        get => _summary;
        private set => SetProperty(ref _summary, value);
    }

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    /// <summary>
    /// Roda as verificacoes e devolve os resultados.
    /// </summary>
    public async Task<IReadOnlyList<RequirementResult>> RunAsync(
        string installDirectory,
        IReadOnlyList<string> endpoints,
        CancellationToken cancellationToken)
    {
        HasFinished = false;
        HasCriticalFailure = false;
        Summary = string.Empty;

        foreach (var item in Results.ToList())
        {
            var updated = item with { Status = RequirementStatus.Running, Detail = "Verificando…" };
            Replace(item, updated);
        }

        var results = await _checker
            .CheckAsync(installDirectory, endpoints, cancellationToken)
            .ConfigureAwait(true);

        foreach (var result in results)
        {
            var existing = Results.FirstOrDefault(entry => entry.Id == result.Id);
            if (existing is not null)
            {
                Replace(existing, result);
            }
            else
            {
                Results.Add(result);
            }
        }

        HasCriticalFailure = results.Any(result =>
            result.Status == RequirementStatus.Failed && result.Severity == RequirementSeverity.Critical);

        var problems = results.Count(result => result.Status == RequirementStatus.Failed);
        Summary = problems == 0
            ? Strings.Get("Requirements_Passed")
            : problems == 1
                ? Strings.Get("Requirements_FailedOne")
                : Strings.Get("Requirements_Failed", problems);

        HasFinished = true;
        return results;
    }

    private void Replace(RequirementResult old, RequirementResult updated)
    {
        var index = Results.IndexOf(old);
        if (index >= 0)
        {
            Results[index] = updated;
        }
    }

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;
}
