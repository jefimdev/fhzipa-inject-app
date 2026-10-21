using System.Collections.ObjectModel;
using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de componentes: cada item e uma parte real da instalacao.
/// </summary>
public sealed class ComponentsViewModel : StepViewModelBase
{
    public ComponentsViewModel(MainViewModel main) : base(main)
    {
        Components =
        [
            new ComponentItem
            {
                Id = "app",
                Name = Strings.Get("Component_App"),
                Detail = Strings.Get("Component_App_Detail"),
            },
            new ComponentItem
            {
                Id = "shortcuts",
                Name = Strings.Get("Component_Shortcuts"),
                Detail = Strings.Get("Component_Shortcuts_Detail"),
                IsOptional = true,
            },
            new ComponentItem
            {
                Id = "registry",
                Name = Strings.Get("Component_Registry"),
                Detail = Strings.Get("Component_Registry_Detail"),
            },
            new ComponentItem
            {
                Id = "uninstaller",
                Name = Strings.Get("Component_Uninstaller"),
                Detail = Strings.Get("Component_Uninstaller_Detail"),
            },
        ];
    }

    public ObservableCollection<ComponentItem> Components { get; }

    public string Title => Strings.Get("Components_Title");

    public string Description => Strings.Get("Components_Description");

    public string OptionalText => Strings.Get("Component_Optional");

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;

    /// <summary>
    /// Atualiza o estado de um componente.
    /// </summary>
    public void SetStatus(string id, ComponentStatus status)
    {
        var component = Components.FirstOrDefault(item => item.Id == id);
        if (component is not null)
        {
            component.Status = status;
        }
    }
}
