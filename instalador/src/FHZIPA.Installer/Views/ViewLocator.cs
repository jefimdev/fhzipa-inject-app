using System.Windows.Controls;
using FHZIPA.Installer.ViewModels;

namespace FHZIPA.Installer.Views;

/// <summary>
/// Localiza as telas do instalador.
///
/// Cada tela e criada uma unica vez e guardada. Reutilizar a mesma
/// instancia evita que o WPF recrie a arvore visual a cada mudanca de
/// etapa, que era a origem das piscadas.
/// </summary>
public sealed class ViewLocator : IViewLocator
{
    private readonly Dictionary<Type, Func<UserControl>> _factories = new()
    {
        [typeof(WelcomeViewModel)] = () => new WelcomeView(),
        [typeof(DirectoryViewModel)] = () => new DirectoryView(),
        [typeof(RequirementsViewModel)] = () => new RequirementsView(),
        [typeof(DownloadViewModel)] = () => new DownloadView(),
        [typeof(InstallViewModel)] = () => new InstallView(),
        [typeof(ComponentsViewModel)] = () => new ComponentsView(),
        [typeof(FinalizingViewModel)] = () => new FinalizingView(),
        [typeof(SuccessViewModel)] = () => new SuccessView(),
        [typeof(OpenNowViewModel)] = () => new OpenNowView(),
        [typeof(ErrorViewModel)] = () => new ErrorView(),
    };

    private readonly Dictionary<Type, UserControl> _views = [];

    public object Resolve(object viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        var type = viewModel.GetType();

        if (!_factories.TryGetValue(type, out var factory))
        {
            throw new InvalidOperationException($"Nao existe uma tela para {type.Name}.");
        }

        if (!_views.TryGetValue(type, out var view))
        {
            view = factory();
            _views[type] = view;
        }

        view.DataContext = viewModel;
        return view;
    }
}
