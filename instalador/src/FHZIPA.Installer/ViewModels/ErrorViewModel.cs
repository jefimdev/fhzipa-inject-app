using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de erro.
///
/// Ela explica o que aconteceu, mostra o codigo do erro, diz o que a
/// pessoa pode fazer e aponta onde esta o log com o detalhe tecnico.
/// Nao e um cartao por cima da tela anterior: e uma tela da propria
/// instalacao, com o botao de tentar de novo e o de fechar.
/// </summary>
public sealed class ErrorViewModel : StepViewModelBase
{
    private readonly ILogService _log;

    public ErrorViewModel(MainViewModel main, ILogService log)
        : base(main)
    {
        _log = log;
    }

    public string Title => Strings.Get("Error_Title");

    public string Description => Strings.Get("Error_Description");

    public string CodeLabel => Strings.Get("Error_CodeLabel");

    public string CodeText { get; private set; } = string.Empty;

    public string ActionLabel => Strings.Get("Error_ActionLabel");

    public string ActionText { get; private set; } = string.Empty;

    public string LogLabel => Strings.Get("Error_LogLabel");

    public string LogText => _log.LogFilePath;

    public string RetryText => Strings.Get("Btn_Retry");

    public string CloseText => Strings.Get("Btn_Close");

    /// <summary>
    /// Prepara a tela com o erro que aconteceu.
    /// </summary>
    public void Prepare(InstallerError error, bool canRetry)
    {
        CodeText = error.CodeText;
        ActionText = error.Action;
        CanRetry = canRetry;
        OnPropertyChanged(nameof(CodeText));
        OnPropertyChanged(nameof(ActionText));
        OnPropertyChanged(nameof(CanRetry));
    }

    /// <summary>
    /// Verdadeiro quando a operacao pode ser repetida.
    /// </summary>
    public bool CanRetry { get; private set; }

    public override bool CanContinue => CanRetry;

    public override bool CanGoBack => false;

    public override Task ContinueAsync() => Main.ContinueAsync();

    public override Task BackAsync() => Task.CompletedTask;
}
