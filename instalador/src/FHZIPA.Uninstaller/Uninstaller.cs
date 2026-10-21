using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

/// <summary>
/// O desinstalador do FHZIPA INJECT.
///
/// Ele e compilado em .NET Framework 4.8, que ja vem em todo Windows 10 e
/// 11, para nao depender de nenhum tempo de execucao extra. O instalador
/// principal deixa este arquivo na pasta de instalacao e registra-o em
/// "Aplicativos instalados".
///
/// A propria pasta ainda esta em uso por este processo, entao a remocao
/// fica a cargo de uma segunda copia dele, guardada na pasta temporaria:
/// ela espera o processo atual terminar e apaga a pasta. Nada de shell,
/// nada de comando montado com o caminho escolhido pelo usuario.
/// </summary>
internal static class Uninstaller
{
    private const string RegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FHZIPA INJECT";
    private const string ProductName = "FHZIPA INJECT";

    [STAThread]
    private static int Main(string[] args)
    {
        // O modo de limpeza: espera o processo que me chamou terminar e
        // apaga a pasta de instalacao.
        if (args.Length > 0 && args[0] == "--remove-folder")
        {
            return RemoveFolderMode(args);
        }

        var directory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar);

        var answer = MessageBox.Show(
            "O " + ProductName + " e os atalhos serao removidos deste computador.\r\n\r\n" +
            "Os seus dados e configuracoes ficam preservados.\r\n\r\n" +
            "Deseja continuar?",
            "Desinstalar o " + ProductName,
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (answer != DialogResult.Yes)
        {
            return 0;
        }

        try
        {
            RemoveShortcuts();
            RemoveRegistryEntry();
            ScheduleFolderRemoval(directory);

            MessageBox.Show(
                "O " + ProductName + " foi removido deste computador.",
                ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Nao foi possivel remover tudo:\r\n\r\n" + exception.Message,
                ProductName,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return 1;
        }
    }

    /// <summary>
    /// O modo de limpeza: espera o processo indicado terminar e apaga a
    /// pasta. A propria copia fica na pasta temporaria, por isso ela
    /// consegue apagar o diretorio de instalacao.
    /// </summary>
    private static int RemoveFolderMode(string[] args)
    {
        if (args.Length < 3)
        {
            return 2;
        }

        var directory = args[1];
        int parentPid;

        if (!int.TryParse(args[2], out parentPid))
        {
            return 2;
        }

        try
        {
            using (var parent = Process.GetProcessById(parentPid))
            {
                parent.WaitForExit();
            }
        }
        catch (ArgumentException)
        {
            // O processo que me chamou ja encerrou.
        }

        DeleteDirectory(directory);

        // A copia temporaria se apaga sozinha.
        try
        {
            var self = Process.GetCurrentProcess().MainModule.FileName;
            File.Delete(self);
        }
        catch (Exception)
        {
            // A pasta temporaria e limpa pelo sistema.
        }

        return 0;
    }

    private static void RemoveShortcuts()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var startMenu = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Microsoft", "Windows", "Start Menu", "Programs", "FHZIPA INJECT");

        TryDelete(Path.Combine(desktop, "FHZIPA INJECT.lnk"));
        TryDelete(Path.Combine(startMenu, "FHZIPA INJECT.lnk"));

        try
        {
            if (Directory.Exists(startMenu) && !Directory.EnumerateFileSystemEntries(startMenu).Any())
            {
                Directory.Delete(startMenu);
            }
        }
        catch (Exception)
        {
            // A pasta vazia pode ficar para tras, nao e um problema.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
            // O atalho pode estar em uso; a instalacao continua.
        }
    }

    private static void RemoveRegistryEntry()
    {
        using (var uninstall = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
        {
            if (uninstall != null)
            {
                uninstall.DeleteSubKeyTree("FHZIPA INJECT", false);
            }
        }
    }

    /// <summary>
    /// Copia este executavel para a pasta temporaria e deixa a copia
    /// esperando o processo atual terminar para apagar a pasta de instalacao.
    /// </summary>
    private static void ScheduleFolderRemoval(string directory)
    {
        var source = Process.GetCurrentProcess().MainModule.FileName;
        var workDirectory = Path.Combine(Path.GetTempPath(), "FHZIPA INJECT", "uninstall");
        Directory.CreateDirectory(workDirectory);

        var copy = Path.Combine(
            workDirectory,
            "uninstall-" + Guid.NewGuid().ToString("N") + ".exe");
        File.Copy(source, copy, true);

        var info = new ProcessStartInfo
        {
            FileName = copy,
            Arguments = "--remove-folder \"" + directory + "\" " + Process.GetCurrentProcess().Id.ToString(),
            CreateNoWindow = true,
            UseShellExecute = false,
            WorkingDirectory = workDirectory,
        };

        Process.Start(info);
    }

    /// <summary>
    /// Apaga a pasta e o que estiver dentro dela, tolerando arquivos que
    /// ainda estejam abertos.
    /// </summary>
    private static void DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(directory, true);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(500);
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(500);
            }
        }

        // Ultimo recurso: apaga arquivo por arquivo, ignorando o que estiver
        // em uso.
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                TryDelete(file);
            }

            Directory.Delete(directory, true);
        }
        catch (Exception)
        {
            // A pasta pode ficar para tras, mas os arquivos importantes ja
            // foram removidos.
        }
    }
}
