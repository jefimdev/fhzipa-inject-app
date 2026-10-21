using System.Net.Http;
using System.Runtime.InteropServices;
using FHZIPA.Installer.Models;
using Microsoft.Win32;

namespace FHZIPA.Installer.Services;

/// <summary>
/// As verificacoes que o instalador faz antes de comecar.
/// </summary>
public interface IRequirementChecker
{
    /// <summary>
    /// Roda as verificacoes e devolve o resultado de cada uma.
    /// </summary>
    Task<IReadOnlyList<RequirementResult>> CheckAsync(
        string installDirectory,
        IReadOnlyList<string> endpoints,
        CancellationToken cancellationToken);
}

/// <summary>
/// As verificacoes de verdade: sistema, espaco, permissoes, WebView2 e rede.
/// </summary>
public sealed class RequirementChecker : IRequirementChecker
{
    private const string WebView2ClientId = "{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}";

    private readonly ILogService _log;
    private readonly IElevationService _elevation;

    public RequirementChecker(ILogService log, IElevationService elevation)
    {
        _log = log;
        _elevation = elevation;
    }

    public async Task<IReadOnlyList<RequirementResult>> CheckAsync(
        string installDirectory,
        IReadOnlyList<string> endpoints,
        CancellationToken cancellationToken)
    {
        var results = new List<RequirementResult>
        {
            CheckOperatingSystem(),
            CheckDiskSpace(installDirectory),
            CheckPermissions(installDirectory),
            CheckWebView2(),
        };

        results.Add(await CheckNetworkAsync(endpoints, cancellationToken).ConfigureAwait(false));
        return results;
    }

    /// <summary>
    /// A versao real do Windows, lida do sistema.
    /// </summary>
    private static RequirementResult CheckOperatingSystem()
    {
        var version = Environment.OSVersion.Version;
        var isX64 = RuntimeInformation.OSArchitecture is Architecture.X64 or Architecture.Arm64;
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        if (!isWindows)
        {
            return RequirementResult.Fail(
                "os",
                "Sistema operacional",
                "O instalador so funciona no Windows.",
                RequirementSeverity.Critical,
                "Use o instalador em um computador com Windows 10 ou 11.");
        }

        if (version.Major < 10)
        {
            return RequirementResult.Fail(
                "os",
                "Sistema operacional",
                $"Este computador usa o Windows {version.Major}.{version.Minor}.",
                RequirementSeverity.Critical,
                "O instalador precisa do Windows 10 ou 11, de 64 bits.");
        }

        var architecture = isX64 ? "64 bits" : RuntimeInformation.OSArchitecture.ToString();
        return RequirementResult.Pass(
            "os",
            "Sistema operacional",
            $"Windows {version.Major}.{version.Minor} ({architecture})");
    }

    /// <summary>
    /// O espaco livre na unidade do diretorio de instalacao.
    /// </summary>
    private static RequirementResult CheckDiskSpace(string installDirectory)
    {
        const long required = 250L * 1024 * 1024;
        const long margin = 64L * 1024 * 1024;

        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(installDirectory));
            if (string.IsNullOrEmpty(root))
            {
                return RequirementResult.Warn(
                    "espaco",
                    "Espaco em disco",
                    "Nao foi possivel ler a unidade do diretorio escolhido.");
            }

            var drive = new DriveInfo(root);
            var available = drive.AvailableFreeSpace;
            var needed = required + margin;

            if (available < needed)
            {
                return RequirementResult.Fail(
                    "espaco",
                    "Espaco em disco",
                    $"Espaco disponivel: {Format(available)}. Espaco necessario: {Format(needed)}.",
                    RequirementSeverity.Critical,
                    "Libere espaco na unidade e execute o instalador novamente.");
            }

            return RequirementResult.Pass(
                "espaco",
                "Espaco em disco",
                $"{Format(available)} disponiveis");
        }
        catch (IOException)
        {
            return RequirementResult.Warn("espaco", "Espaco em disco", "Nao foi possivel ler a unidade.");
        }
        catch (UnauthorizedAccessException)
        {
            return RequirementResult.Warn("espaco", "Espaco em disco", "A unidade nao pode ser lida.");
        }
    }

    /// <summary>
    /// Se o diretorio aceita escrita.
    ///
    /// Uma pasta que so o administrador pode escrever nao e uma falha
    /// critica: o instalador pede a autorizacao do Windows no momento de
    /// avancar. E a pasta nao e criada aqui: o teste e feito na pasta de
    /// origem, que ja existe, e nada sobra no disco.
    /// </summary>
    private RequirementResult CheckPermissions(string installDirectory)
    {
        try
        {
            if (Security.PathValidator.IsWritable(installDirectory))
            {
                return RequirementResult.Pass("permissoes", "Permissoes de administrador", "Escrita permitida");
            }

            if (_elevation.RequiresElevation(installDirectory) && !_elevation.IsElevated)
            {
                return RequirementResult.Warn(
                    "permissoes",
                    "Permissoes de administrador",
                    "O Windows vai pedir a autorizacao de administrador para escrever nesta pasta.");
            }

            return RequirementResult.Fail(
                "permissoes",
                "Permissoes de administrador",
                "O Windows nao autorizou a escrita no diretorio escolhido.",
                RequirementSeverity.Critical,
                "Escolha outro diretorio ou execute o instalador como administrador.");
        }
        catch (UnauthorizedAccessException)
        {
            return RequirementResult.Fail(
                "permissoes",
                "Permissoes de administrador",
                "O Windows nao autorizou a escrita no diretorio escolhido.",
                RequirementSeverity.Critical,
                "Escolha outro diretorio ou execute o instalador como administrador.");
        }
        catch (IOException exception)
        {
            return RequirementResult.Warn(
                "permissoes",
                "Permissoes de administrador",
                $"O Windows recusou o acesso: {exception.Message}");
        }
    }

    /// <summary>
    /// Se o WebView2, o motor da interface do aplicativo, esta instalado.
    /// </summary>
    private static RequirementResult CheckWebView2()
    {
        var version = ReadWebView2Version();
        if (version is null)
        {
            return RequirementResult.Warn(
                "webview2",
                "Dependencias",
                "O WebView2 nao foi encontrado neste computador.",
                "A instalacao pode baixar o WebView2 ou voce pode instala-lo pela pagina da Microsoft.");
        }

        return RequirementResult.Pass(
            "webview2",
            "Dependencias",
            $"WebView2 {version} instalado");
    }

    private static string? ReadWebView2Version()
    {
        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry32);
            using var edge = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients");
            using var client = edge?.OpenSubKey(WebView2ClientId);
            if (client?.GetValue("pv") is string value && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    /// <summary>
    /// A conexao com a internet, medida por uma requisicao de verdade.
    /// </summary>
    private async Task<RequirementResult> CheckNetworkAsync(
        IReadOnlyList<string> endpoints,
        CancellationToken cancellationToken)
    {
        if (endpoints.Count == 0)
        {
            return RequirementResult.Warn(
                "rede",
                "Conexao com a internet",
                "Nenhuma origem remota configurada, a instalacao usa o pacote local.");
        }

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        foreach (var endpoint in endpoints)
        {
            try
            {
                using var response = await client
                    .GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                return response.IsSuccessStatusCode
                    ? RequirementResult.Pass("rede", "Conexao com a internet", "Disponivel")
                    : RequirementResult.Warn(
                        "rede",
                        "Conexao com a internet",
                        $"O servidor respondeu {response.StatusCode}.");
            }
            catch (HttpRequestException exception)
            {
                _log.Warn("Falha na conexao: {0}", exception.Message);
                return RequirementResult.Warn(
                    "rede",
                    "Conexao com a internet",
                    "Nao foi possivel falar com o servidor.",
                    "Verifique a conexao ou use o pacote local.");
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return RequirementResult.Warn(
                    "rede",
                    "Conexao com a internet",
                    "O servidor demorou demais para responder.",
                    "Verifique a conexao ou use o pacote local.");
            }
        }

        return RequirementResult.Pass("rede", "Conexao com a internet", "Disponivel");
    }

    private static string Format(long bytes)
    {
        var units = new[] { "B", "KB", "MB", "GB", "TB" };
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 102;
            unit++;
        }

        return $"{value:0.#} {units[unit]}";
    }
}
