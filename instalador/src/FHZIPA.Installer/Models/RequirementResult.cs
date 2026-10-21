namespace FHZIPA.Installer.Models;

/// <summary>
/// Como uma verificacao termina.
/// </summary>
public enum RequirementStatus
{
    Pending,
    Running,
    Passed,
    Failed,
    Skipped,
}

/// <summary>
/// A gravidade de uma verificacao. So a critica impede o avanco.
/// </summary>
public enum RequirementSeverity
{
    Information,
    Warning,
    Critical,
}

/// <summary>
/// O resultado de uma verificacao de requisito.
/// </summary>
/// <param name="Id">O identificador, estavel nos logs.</param>
/// <param name="Title">O titulo, curto.</param>
/// <param name="Status">Como a verificacao terminou.</param>
/// <param name="Detail">O detalhe, com o valor medido.</param>
/// <param name="Severity">A gravidade.</param>
/// <param name="Resolution">O que o usuario pode fazer, quando ha algo a fazer.</param>
public sealed record RequirementResult(
    string Id,
    string Title,
    RequirementStatus Status,
    string Detail,
    RequirementSeverity Severity = RequirementSeverity.Information,
    string? Resolution = null)
{
    /// <summary>
    /// A gravidade em texto, para a interface.
    /// </summary>
    public string SeverityText => Severity switch
    {
        RequirementSeverity.Critical => Localization.Strings.Get("Requirements_Critical"),
        RequirementSeverity.Warning => Localization.Strings.Get("Requirements_Warning"),
        _ => Localization.Strings.Get("Requirements_Info"),
    };

    /// <summary>
    /// Uma verificacao que passou.
    /// </summary>
    public static RequirementResult Pass(string id, string title, string detail) =>
        new(id, title, RequirementStatus.Passed, detail);

    /// <summary>
    /// Uma verificacao que falhou.
    /// </summary>
    public static RequirementResult Fail(
        string id,
        string title,
        string detail,
        RequirementSeverity severity = RequirementSeverity.Critical,
        string? resolution = null) =>
        new(id, title, RequirementStatus.Failed, detail, severity, resolution);

    /// <summary>
    /// Um aviso, que nao impede a instalacao.
    /// </summary>
    public static RequirementResult Warn(string id, string title, string detail, string? resolution = null) =>
        new(id, title, RequirementStatus.Failed, detail, RequirementSeverity.Warning, resolution);
}
