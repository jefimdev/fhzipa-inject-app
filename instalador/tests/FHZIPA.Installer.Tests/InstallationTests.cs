using System.Security.Cryptography;
using FHZIPA.Installer.Installation;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;
using Microsoft.Win32;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// A instalacao, o rollback e a desinstalacao, em uma pasta de teste.
/// </summary>
public class InstallationTests : IDisposable
{
    private readonly string _root;
    private readonly string _installDirectory;
    private readonly string _payload;
    private readonly string _registryKey =
        @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FHZIPA INJECT TESTE";

    public InstallationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"fhzipa-teste-{Guid.NewGuid():N}");
        _installDirectory = Path.Combine(_root, "instalado");
        _payload = Path.Combine(_root, "aplicativo.exe");
        Directory.CreateDirectory(_root);
        File.WriteAllText(_payload, "conteudo de teste do instalador");
    }

    public void Dispose()
    {
        using (var key = Registry.CurrentUser.OpenSubKey(
                   @"Software\Microsoft\Windows\CurrentVersion\Uninstall", writable: true))
        {
            key?.DeleteSubKeyTree("FHZIPA INJECT TESTE", throwOnMissingSubKey: false);
        }

        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private InstallOptions Options(string? expectedSha256 = null) => new(
        "aplicativo.exe",
        CreateDesktopShortcut: false,
        CreateStartMenuShortcut: false,
        expectedSha256,
        _registryKey,
        $"\"{Path.Combine(_installDirectory, "desinstalar.exe")}\" --uninstall");

    [Fact]
    public async Task InstalaoCopiaOArquivoERegistraODesinstalador()
    {
        var service = new InstallationService(new ConsoleLogService());

        var outcome = await service.InstallAsync(
            _payload,
            _installDirectory,
            Options(),
            progress: null,
            CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Error?.Description);
        Assert.True(File.Exists(Path.Combine(_installDirectory, "aplicativo.exe")));

        using var key = Registry.CurrentUser.OpenSubKey(_registryKey);
        Assert.NotNull(key);
        Assert.Equal("FHZIPA INJECT", key!.GetValue("DisplayName"));
        Assert.False(string.IsNullOrEmpty(key.GetValue("UninstallString") as string));
    }

    [Fact]
    public async Task InstalaoComResumoErradoEhRecusada()
    {
        var service = new InstallationService(new ConsoleLogService());
        var wrongHash = new string('0', 64);

        var outcome = await service.InstallAsync(
            _payload,
            _installDirectory,
            Options(wrongHash),
            progress: null,
            CancellationToken.None);

        Assert.False(outcome.Succeeded);
        Assert.Equal(InstallerErrorCode.IntegrityCheckFailed, outcome.Error!.Code);
        Assert.False(File.Exists(Path.Combine(_installDirectory, "aplicativo.exe")));
    }

    [Fact]
    public async Task InstalaoComResumoCertoEhAceita()
    {
        var service = new InstallationService(new ConsoleLogService());

        var outcome = await service.InstallAsync(
            _payload,
            _installDirectory,
            Options(Sha256Of(_payload)),
            progress: null,
            CancellationToken.None);

        Assert.True(outcome.Succeeded, outcome.Error?.Description);
    }

    [Fact]
    public async Task RollbackDesfazOQueFoiInstalado()
    {
        var service = new InstallationService(new ConsoleLogService());

        var journal = new InstallationJournal { InstallDirectory = _installDirectory };
        Directory.CreateDirectory(_installDirectory);
        var installed = Path.Combine(_installDirectory, "aplicativo.exe");
        File.Copy(_payload, installed);
        journal.InstalledFiles.Add(installed);
        journal.CreatedDirectories.Add(_installDirectory);

        var outcome = await service.RollbackAsync(journal, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.False(outcome.RecoveryRequired);
        Assert.False(File.Exists(installed));
    }

    [Fact]
    public async Task RollbackRestauraOArquivoAnterior()
    {
        var service = new InstallationService(new ConsoleLogService());
        Directory.CreateDirectory(_installDirectory);

        var target = Path.Combine(_installDirectory, "aplicativo.exe");
        File.WriteAllText(target, "versao antiga");
        var backup = target + ".fhzipa-antigo";
        File.WriteAllText(backup, "versao antiga");

        var journal = new InstallationJournal
        {
            InstallDirectory = _installDirectory,
            InstalledFiles = [target],
            BackedUpFiles = [backup],
        };

        var outcome = await service.RollbackAsync(journal, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.True(File.Exists(target));
        Assert.Equal("versao antiga", File.ReadAllText(target));
        Assert.False(File.Exists(backup));
    }

    [Fact]
    public async Task DesinstalacaoRemoveOsArquivos()
    {
        var service = new InstallationService(new ConsoleLogService());
        Directory.CreateDirectory(_installDirectory);
        File.WriteAllText(Path.Combine(_installDirectory, "aplicativo.exe"), "x");

        var outcome = await service.UninstallAsync(_installDirectory, CancellationToken.None);

        Assert.True(outcome.Succeeded);
        Assert.False(Directory.Exists(_installDirectory));
    }

    [Fact]
    public void ORegistroDaInstalacaoEhLidoEApagado()
    {
        var stateDirectory = Path.Combine(_root, "estado");
        var journal = new InstallationJournal
        {
            InstallDirectory = _installDirectory,
            InstalledFiles = ["a.exe"],
            CreatedShortcuts = ["b.lnk"],
        };

        journal.Save(stateDirectory);
        var loaded = InstallationJournal.Load(stateDirectory);

        Assert.NotNull(loaded);
        Assert.Equal(_installDirectory, loaded!.InstallDirectory);
        Assert.Single(loaded.InstalledFiles);

        InstallationJournal.Clear(stateDirectory);
        Assert.Null(InstallationJournal.Load(stateDirectory));
    }
}
