using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FHZIPA.Installer.Models;

/// <summary>
/// As etapas mostradas no painel da esquerda.
/// </summary>
public enum InstallationStep
{
    Preparing,
    Verifying,
    Downloading,
    Installing,
    Configuring,
    Finalizing,
}

/// <summary>
/// O estado de uma etapa do painel.
/// </summary>
public enum StepStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>
/// Uma etapa do painel esquerdo, com o estado e a mensagem.
/// </summary>
public sealed class StepItem : INotifyPropertyChanged
{
    private StepStatus _status;
    private string _message = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public InstallationStep Step { get; init; }

    public string Title { get; init; } = string.Empty;

    public StepStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            Raise(nameof(Status));
            Raise(nameof(StatusText));
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            if (_message == value)
            {
                return;
            }

            _message = value;
            Raise(nameof(Message));
        }
    }

    /// <summary>
    /// O estado em texto, como na referencia: "Concluido", "Em andamento".
    /// </summary>
    public string StatusText => Status switch
    {
        StepStatus.Running => Localization.Strings.Get("StepState_Running"),
        StepStatus.Done => Localization.Strings.Get("StepState_Done"),
        StepStatus.Failed => Localization.Strings.Get("StepState_Failed"),
        _ => string.Empty,
    };

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// O estado de uma operacao dentro de uma tela.
/// </summary>
public enum OperationStatus
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>
/// Uma operacao real da instalacao, mostrada na tela de instalacao.
/// </summary>
public sealed class OperationItem : INotifyPropertyChanged
{
    private OperationStatus _status;
    private string _detail = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public OperationStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            Raise(nameof(Status));
            Raise(nameof(StatusText));
        }
    }

    public string Detail
    {
        get => _detail;
        set
        {
            if (_detail == value)
            {
                return;
            }

            _detail = value;
            Raise(nameof(Detail));
        }
    }

    public string StatusText => Status switch
    {
        OperationStatus.Running => Localization.Strings.Get("OperationState_Running"),
        OperationStatus.Done => Localization.Strings.Get("OperationState_Done"),
        OperationStatus.Failed => Localization.Strings.Get("OperationState_Failed"),
        _ => Localization.Strings.Get("OperationState_Pending"),
    };

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// O estado de um componente que a instalacao trata.
/// </summary>
public enum ComponentStatus
{
    Pending,
    Verifying,
    Downloading,
    Validating,
    Installing,
    Done,
    Skipped,
    Failed,
}

/// <summary>
/// Um componente da instalacao, mostrado na tela de componentes.
/// </summary>
public sealed class ComponentItem : INotifyPropertyChanged
{
    private ComponentStatus _status;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;

    public bool IsOptional { get; init; }

    public ComponentStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            Raise(nameof(Status));
            Raise(nameof(StatusText));
        }
    }

    public double Progress { get; set; }

    /// <summary>
    /// O estado em texto, para a interface.
    /// </summary>
    public string StatusText => Status switch
    {
        ComponentStatus.Pending => Localization.Strings.Get("Status_Pending"),
        ComponentStatus.Verifying => Localization.Strings.Get("Status_Verifying"),
        ComponentStatus.Downloading => Localization.Strings.Get("Status_Downloading"),
        ComponentStatus.Validating => Localization.Strings.Get("Status_Validating"),
        ComponentStatus.Installing => Localization.Strings.Get("Status_Installing"),
        ComponentStatus.Done => Localization.Strings.Get("Status_Done"),
        ComponentStatus.Skipped => Localization.Strings.Get("Status_Skipped"),
        ComponentStatus.Failed => Localization.Strings.Get("Status_Failed"),
        _ => Localization.Strings.Get("Status_Pending"),
    };

    private void Raise([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// A lista de etapas do instalador, na ordem em que aparecem.
/// </summary>
public static class InstallationSteps
{
    public static IReadOnlyList<(InstallationStep Step, string Title)> All { get; } =
    [
        (InstallationStep.Preparing, "Preparando"),
        (InstallationStep.Verifying, "Verificando o sistema"),
        (InstallationStep.Downloading, "Baixando arquivos"),
        (InstallationStep.Installing, "Instalando"),
        (InstallationStep.Configuring, "Configurando"),
        (InstallationStep.Finalizing, "Finalizando"),
    ];
}
