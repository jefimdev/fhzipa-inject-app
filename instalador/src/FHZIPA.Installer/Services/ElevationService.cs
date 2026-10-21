using System.Diagnostics;
using System.Security.Principal;

namespace FHZIPA.Installer.Services;

/// <summary>
/// A elevacao do instalador. O processo so e reiniciado como administrador
/// quando o diretorio escolhido exige isso.
/// </summary>
public interface IElevationService
{
    /// <summary>
    /// Se o processo atual roda como administrador.
    /// </summary>
    bool IsElevated { get; }

    /// <summary>
    /// Se o diretorio exige permissao de administrador.
    /// </summary>
    bool RequiresElevation(string directory);

    /// <summary>
    /// Reinicia o instalador com elevacao, passando os argumentos.
    /// </summary>
    bool RestartElevated(string arguments);
}

/// <summary>
/// A elevacao de verdade, usando o proprio Windows.
/// </summary>
public sealed class ElevationService : IElevationService
{
    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool RequiresElevation(string directory)
    {
        if (IsElevated)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            return false;
        }

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            if (string.IsNullOrEmpty(root))
            {
                return false;
            }

            var protectedRoots = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            }
            .Where(path => !string.IsNullOrEmpty(path))
            .Select(path => path.TrimEnd(Path.DirectorySeparatorChar))
            .ToArray();

            var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);
            return protectedRoots.Any(protectedRoot =>
                full.Equals(protectedRoot, StringComparison.OrdinalIgnoreCase) ||
                full.StartsWith(protectedRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (PathTooLongException)
        {
            return false;
        }
    }

    public bool RestartElevated(string arguments)
    {
        var source = Environment.ProcessPath;
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        try
        {
            var info = new ProcessStartInfo
            {
                FileName = source,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
            };

            Process.Start(info);
            return true;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
