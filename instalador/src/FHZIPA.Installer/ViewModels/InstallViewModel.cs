using System.Collections.ObjectModel;
using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de instalacao: as operacoes que estao acontecendo de verdade.
/// </summary>
public sealed class InstallViewModel : StepViewModelBase
{
    public InstallViewModel(MainViewModel main) : base(main)
    {
        Operations =
        [
            new OperationItem { Id = "prepare", Title = Strings.Get("Operation_Prepare") },
            new OperationItem { Id = "extract", Title = Strings.Get("Operation_Extract") },
            new OperationItem { Id = "copy", Title = Strings.Get("Operation_Copy") },
            new OperationItem { Id = "shortcuts", Title = Strings.Get("Operation_Shortcuts") },
            new OperationItem { Id = "register", Title = Strings.Get("Operation_Register") },
        ];
    }

    public ObservableCollection<OperationItem> Operations { get; }

    public string Title => Strings.Get("Install_Title");

    public string Description => Strings.Get("Install_Description");

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;

    /// <summary>
    /// Marca uma operacao como concluida e a seguinte como em andamento.
    /// </summary>
    public void CompleteOperation(string id)
    {
        var index = Operations.ToList().FindIndex(operation => operation.Id == id);
        if (index < 0)
        {
            return;
        }

        Operations[index].Status = OperationStatus.Done;

        for (var next = index + 1; next < Operations.Count; next++)
        {
            if (Operations[next].Status == OperationStatus.Pending)
            {
                Operations[next].Status = OperationStatus.Running;
                break;
            }
        }
    }

    /// <summary>
    /// Marca todas as operacoes como concluidas.
    /// </summary>
    public void CompleteAll()
    {
        foreach (var operation in Operations)
        {
            operation.Status = OperationStatus.Done;
        }
    }
}
