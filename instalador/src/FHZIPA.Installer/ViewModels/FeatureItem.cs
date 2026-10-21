namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// Os icones dos destaques do painel lateral.
/// </summary>
public enum FeatureIcon
{
    Shield,
    Bolt,
    Gear,
}

/// <summary>
/// Um destaque do instalador, mostrado no painel lateral.
/// </summary>
public sealed class FeatureItem
{
    public FeatureIcon Icon { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
