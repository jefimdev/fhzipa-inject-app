using System.Globalization;
using System.Resources;

namespace FHZIPA.Installer.Localization;

/// <summary>
/// O acesso aos textos da interface. Todos os textos vem do arquivo de
/// recursos, nenhum texto fica fixo no codigo.
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Manager = new(
        "FHZIPA.Installer.Localization.Strings",
        typeof(Strings).Assembly);

    /// <summary>
    /// A cultura usada nos textos. Vazio usa a cultura do sistema.
    /// </summary>
    public static CultureInfo? Culture { get; set; }

    /// <summary>
    /// Pega um texto pelo nome da chave. Quando a chave nao existe, o
    /// proprio nome aparece, o que denuncia o erro na hora.
    /// </summary>
    public static string Get(string key, params object?[] args)
    {
        var value = Manager.GetString(key, Culture);
        if (value is null)
        {
            return $"!{key}!";
        }

        if (args.Length == 0)
        {
            return value;
        }

        try
        {
            return string.Format(CultureInfo.CurrentCulture, value, args);
        }
        catch (FormatException)
        {
            return value;
        }
    }

    /// <summary>
    /// Todas as chaves existentes no arquivo de recursos.
    /// </summary>
    public static IEnumerable<string> AllKeys()
    {
        var set = Manager.GetResourceSet(
            CultureInfo.InvariantCulture,
            createIfNotExists: true,
            tryParents: false);

        if (set is null)
        {
            yield break;
        }

        foreach (System.Collections.DictionaryEntry entry in set)
        {
            if (entry.Key is string key)
            {
                yield return key;
            }
        }
    }
}
