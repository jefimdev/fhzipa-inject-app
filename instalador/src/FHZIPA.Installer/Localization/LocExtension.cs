using System.Windows.Markup;

namespace FHZIPA.Installer.Localization;

/// <summary>
/// A extensao de marcacao que busca um texto no arquivo de recursos.
/// Uso: Text="{loc:Loc Key=Welcome_Title}"
/// </summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    /// <summary>
    /// A chave do texto no arquivo de recursos.
    /// </summary>
    public string? Key { get; set; }

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        string.IsNullOrEmpty(Key) ? string.Empty : Strings.Get(Key);
}
