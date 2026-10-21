namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// Localiza a tela de um ViewModel.
///
/// A tela e criada uma vez e reaproveitada. Recriar a tela a cada
/// mudanca de etapa provoca piscadas e salto de layout.
/// </summary>
public interface IViewLocator
{
    /// <summary>
    /// Devolve a tela do ViewModel, pronta para ser exibida.
    /// </summary>
    object Resolve(object viewModel);
}
