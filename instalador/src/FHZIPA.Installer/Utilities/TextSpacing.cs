namespace FHZIPA.Installer.Utilities;

/// <summary>
/// O WPF nao tem espacamento entre letras. Para o subtitulo da marca, o
/// espaco entra no proprio texto.
/// </summary>
public static class TextSpacing
{
    /// <summary>
    /// Devolve o texto com as letras espacadas e as palavras separadas por
    /// um espaco duplo.
    /// </summary>
    public static string Space(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("  ", words.Select(word => string.Join(" ", word)));
    }
}
