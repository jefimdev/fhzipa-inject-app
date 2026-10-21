namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// O que toda tela da instalacao precisa oferecer: poder continuar, poder
/// voltar e dizer se as acoes fazem sentido no momento.
/// </summary>
public interface IStepViewModel
{
    /// <summary>
    /// Se o botao principal pode ser usado.
    /// </summary>
    bool CanContinue { get; }

    /// <summary>
    /// Se o botao voltar pode ser usado.
    /// </summary>
    bool CanGoBack { get; }

    /// <summary>
    /// Executa a acao principal da tela.
    /// </summary>
    Task ContinueAsync();

    /// <summary>
    /// Volta para a tela anterior.
    /// </summary>
    Task BackAsync();
}
