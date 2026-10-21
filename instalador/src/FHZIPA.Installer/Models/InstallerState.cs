namespace FHZIPA.Installer.Models;

/// <summary>
/// As etapas do instalador. A máquina de estados é explicita: cada passo
/// tem uma origem e um destino validados, e qualquer transição fora da
/// tabela é recusada.
/// </summary>
public enum InstallerState
{
    NotStarted = 0,
    Welcome,
    DirectorySelection,
    CheckingRequirements,
    RequirementsFailed,
    ReadyToDownload,
    Downloading,
    DownloadPaused,
    DownloadFailed,
    ValidatingDownloads,
    ReadyToInstall,
    Installing,
    InstallingDependencies,
    Configuring,
    Finalizing,
    Completed,
    Cancelled,
    Failed,
    RollbackInProgress,
    RollbackCompleted,
    RecoveryRequired,
}

/// <summary>
/// As transições permitidas da máquina de estados.
/// </summary>
public static class InstallerStateMachine
{
    private static readonly Dictionary<InstallerState, InstallerState[]> Allowed = new()
    {
        [InstallerState.NotStarted] = [InstallerState.Welcome],
        [InstallerState.Welcome] = [InstallerState.DirectorySelection, InstallerState.Cancelled],
        [InstallerState.DirectorySelection] =
            [InstallerState.CheckingRequirements, InstallerState.Welcome, InstallerState.Cancelled],
        [InstallerState.CheckingRequirements] =
            [InstallerState.RequirementsFailed, InstallerState.ReadyToDownload, InstallerState.Cancelled],
        [InstallerState.RequirementsFailed] =
            [InstallerState.CheckingRequirements, InstallerState.Cancelled],
        [InstallerState.ReadyToDownload] =
            [InstallerState.Downloading, InstallerState.Cancelled],
        [InstallerState.Downloading] =
            [InstallerState.DownloadPaused, InstallerState.DownloadFailed, InstallerState.ValidatingDownloads, InstallerState.Cancelled],
        [InstallerState.DownloadPaused] =
            [InstallerState.Downloading, InstallerState.Cancelled],
        [InstallerState.DownloadFailed] =
            [InstallerState.ReadyToDownload, InstallerState.Cancelled],
        [InstallerState.ValidatingDownloads] =
            [InstallerState.ReadyToInstall, InstallerState.DownloadFailed, InstallerState.Cancelled],
        [InstallerState.ReadyToInstall] =
            [InstallerState.Installing, InstallerState.Cancelled],
        [InstallerState.Installing] =
            [InstallerState.InstallingDependencies, InstallerState.Failed, InstallerState.Cancelled],
        [InstallerState.InstallingDependencies] =
            [InstallerState.Configuring, InstallerState.Failed, InstallerState.Cancelled],
        [InstallerState.Configuring] =
            [InstallerState.Finalizing, InstallerState.Failed, InstallerState.Cancelled],
        [InstallerState.Finalizing] =
            [InstallerState.Completed, InstallerState.Failed, InstallerState.Cancelled],
        [InstallerState.Completed] = [],
        [InstallerState.Cancelled] =
            [InstallerState.NotStarted, InstallerState.Welcome, InstallerState.DirectorySelection],
        [InstallerState.Failed] =
        [
            InstallerState.RollbackInProgress,
            InstallerState.Cancelled,
            InstallerState.NotStarted,
            InstallerState.Welcome,
            InstallerState.DirectorySelection,
        ],
        [InstallerState.RollbackInProgress] =
            [InstallerState.RollbackCompleted, InstallerState.RecoveryRequired],
        [InstallerState.RollbackCompleted] = [InstallerState.NotStarted],
        [InstallerState.RecoveryRequired] = [InstallerState.NotStarted],
    };

    /// <summary>
    /// Diz se a transição é permitida. Uma etapa de trabalho não pode ser
    /// pulada, e uma falha não pode virar sucesso.
    /// </summary>
    public static bool CanTransition(InstallerState from, InstallerState to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    /// <summary>
    /// As transições possíveis a partir de uma etapa.
    /// </summary>
    public static IReadOnlyList<InstallerState> TransitionsFrom(InstallerState from) =>
        Allowed.TryGetValue(from, out var targets) ? targets : [];

    /// <summary>
    /// Uma etapa esta em andamento quando não há retorno possível por cancelamento.
    /// </summary>
    public static bool IsBusy(InstallerState state) => state is
        InstallerState.CheckingRequirements or
        InstallerState.Downloading or
        InstallerState.ValidatingDownloads or
        InstallerState.Installing or
        InstallerState.InstallingDependencies or
        InstallerState.Configuring or
        InstallerState.Finalizing or
        InstallerState.RollbackInProgress;

    /// <summary>
    /// Uma falha ou um cancelamento pode ser retomado. Sem esta lista a
    /// repeticao do processo seria recusada pela maquina de estados e a
    /// instalacao nunca voltaria ao caminho normal.
    /// </summary>
    public static bool CanRetry(InstallerState state) => state is
        InstallerState.Failed or
        InstallerState.Cancelled or
        InstallerState.RequirementsFailed or
        InstallerState.DownloadFailed;
}
