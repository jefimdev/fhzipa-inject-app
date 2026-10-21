namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A base das telas do instalador. Todas elas compartilham os comandos do
/// rodape e a regra das transicoes.
/// </summary>
public abstract class StepViewModelBase : ViewModelBase, IStepViewModel
{
    protected StepViewModelBase(MainViewModel main) => Main = main;

    protected MainViewModel Main { get; }

    /// <summary>
    /// O botao principal.
    /// </summary>
    public AsyncRelayCommand NextCommand => Main.NextCommand;

    /// <summary>
    /// O botao voltar.
    /// </summary>
    public AsyncRelayCommand BackCommand => Main.BackCommand;

    /// <summary>
    /// O botao cancelar.
    /// </summary>
    public RelayCommand CancelCommand => Main.CancelCommand;

    /// <summary>
    /// O texto do botao cancelar.
    /// </summary>
    public string CancelText => Localization.Strings.Get("Btn_Cancel");

    /// <summary>
    /// O texto do botao voltar.
    /// </summary>
    public string BackText => Localization.Strings.Get("Btn_Back");

    /// <summary>
    /// O progresso geral da instalacao, de 0 a 100.
    /// </summary>
    public double ProgressPercent => Main.ProgressPercent;

    /// <summary>
    /// O progresso em texto.
    /// </summary>
    public string ProgressPercentText => Main.ProgressPercentText;

    public abstract bool CanContinue { get; }

    public abstract bool CanGoBack { get; }

    public abstract Task ContinueAsync();

    public abstract Task BackAsync();
}
