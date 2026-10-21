using System.Text.RegularExpressions;

namespace FHZIPA.Installer.Security;

/// <summary>
/// A validação dos caminhos que o instalador aceita.
/// </summary>
public static partial class PathValidator
{
    /// <summary>
    /// Os caracteres que o Windows nao aceita em um nome de arquivo.
    /// </summary>
    [GeneratedRegex("[<>:\"|?*\x00-\x1F]")]
    private static partial Regex InvalidCharacters();

    /// <summary>
    /// Os nomes reservados pelo Windows, que nao podem ser usados.
    /// </summary>
    private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>
    /// O caminho completo que o Windows comeca a recusar, a partir do
    /// caminho absoluto.
    /// </summary>
    public const int MaxPathLength = 260;

    /// <summary>
    /// Verifica se o caminho serve para uma instalacao.
    /// </summary>
    /// <param name="path">O caminho escolhido.</param>
    /// <param name="problem">O que impede o uso, quando impede.</param>
    public static bool IsValidInstallPath(string? path, out string? problem)
    {
        problem = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            problem = "O caminho esta vazio.";
            return false;
        }

        if (!Path.IsPathFullyQualified(path))
        {
            problem = "O caminho precisa ser absoluto.";
            return false;
        }

        if (path.Length >= MaxPathLength)
        {
            problem = "O caminho e longo demais para o Windows.";
            return false;
        }

        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            problem = "A unidade do caminho nao existe.";
            return false;
        }

        // Os caracteres invalidos sao conferidos fora da unidade, porque
        // os dois-pontos da letra de unidade sao normais.
        var relative = path[root.Length..];
        if (InvalidCharacters().IsMatch(relative))
        {
            problem = "O caminho tem caracteres que o Windows nao aceita.";
            return false;
        }

        foreach (var segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (ReservedNames.Contains(segment.TrimEnd('.')))
            {
                problem = $"O Windows reserva o nome '{segment}'.";
                return false;
            }
        }

        // Um link simbolico ou uma juncao pode apontar para outro lugar e
        // escaping da area gerenciada.
        var info = new DirectoryInfo(path);
        if (info.Exists && info.Attributes.HasFlag(FileAttributes.ReparsePoint))
        {
            problem = "O caminho aponta para uma juncao ou um link.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Verifica se o caminho pode receber a escrita. Quando a pasta ainda
    /// nao existe, o teste e feito na pasta de origem que existe.
    /// </summary>
    public static bool IsWritable(string path)
    {
        var probeDirectory = path;
        var created = false;

        try
        {
            while (!string.IsNullOrEmpty(probeDirectory) && !Directory.Exists(probeDirectory))
            {
                var parent = Path.GetDirectoryName(probeDirectory);
                if (string.IsNullOrEmpty(parent) || parent == probeDirectory)
                {
                    return false;
                }

                probeDirectory = parent;
            }

            if (string.IsNullOrEmpty(probeDirectory) || !Directory.Exists(probeDirectory))
            {
                return false;
            }

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                created = true;
            }

            var probe = Path.Combine(path, $".fhzipa-probe-{Guid.NewGuid():N}.tmp");
            using (var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                return stream.CanWrite;
            }
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            if (created)
            {
                // A pasta foi criada só para o teste e continua vazia.
                try
                {
                    if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
                    {
                        Directory.Delete(path);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                }
            }
        }
    }

    /// <summary>
    /// Garante que o caminho filho fique dentro do diretorio gerenciado.
    /// E o que impede um pacote de escrever fora da area de instalacao.
    /// </summary>
    public static bool IsInside(string root, string candidate)
    {
        var normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var normalizedCandidate = Path.GetFullPath(candidate);

        return normalizedCandidate.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase);
    }
}
