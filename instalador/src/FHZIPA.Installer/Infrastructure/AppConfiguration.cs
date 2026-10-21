using System.Text.Json;

namespace FHZIPA.Installer.Infrastructure;

/// <summary>
/// A configuracao do instalador. Os campos vazios sao pendencias do
/// projeto real, e a validacao impede a instalacao de comecar com eles.
/// </summary>
public sealed class AppConfiguration
{
    public ApplicationSettings Application { get; set; } = new();

    public InstallerSettings Installer { get; set; } = new();

    public DownloadSettings Downloads { get; set; } = new();

    public SecuritySettings Security { get; set; } = new();

    /// <summary>
    /// Os problemas da configuracao. Uma lista vazia significa que a
    /// configuracao pode ser usada.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(Application.Name))
        {
            problems.Add("Application.Name nao foi informado.");
        }

        if (string.IsNullOrWhiteSpace(Application.ExecutableName))
        {
            problems.Add("Application.ExecutableName nao foi informado.");
        }

        if (string.IsNullOrWhiteSpace(Installer.DefaultDirectory))
        {
            problems.Add("Installer.DefaultDirectory nao foi informado.");
        }

        if (Downloads.TimeoutSeconds <= 0)
        {
            problems.Add("Downloads.TimeoutSeconds tem que ser maior que zero.");
        }

        if (Downloads.MaxRetries < 0)
        {
            problems.Add("Downloads.MaxRetries nao pode ser negativo.");
        }

        return problems;
    }

    /// <summary>
    /// Le a configuracao de um arquivo json.
    /// </summary>
    public static AppConfiguration Load(string path)
    {
        if (!File.Exists(path))
        {
            return new AppConfiguration();
        }

        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            var configuration = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(path), options)
                ?? new AppConfiguration();

            configuration.Installer.DefaultDirectory = Expand(configuration.Installer.DefaultDirectory);
            return configuration;
        }
        catch (JsonException)
        {
            return new AppConfiguration();
        }
    }

    /// <summary>
    /// Troca as variaveis de ambiente do caminho, para que a pasta de
    /// programas seja resolvida pelo proprio Windows e nao por um caminho
    /// fixo no arquivo.
    /// </summary>
    private static string Expand(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) is { Length: > 0 } programs
                ? Path.Combine(programs, "FHZIPA INJECT")
                : path;
        }

        var expanded = Environment.ExpandEnvironmentVariables(path);

        // O "%ProgramFiles%" continua literal quando a variavel nao existe.
        return expanded.Contains('%', StringComparison.Ordinal)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "FHZIPA INJECT")
            : expanded;
    }
}

public sealed class ApplicationSettings
{
    public string Name { get; set; } = "FHZIPA INJECT";

    public string ExecutableName { get; set; } = "fhzipa-inject.exe";

    public string Version { get; set; } = "1.0.0";

    public string Description { get; set; } = "Instalador de aplicativos para iPhone.";

    public string Publisher { get; set; } = "FHZIPA INJECT";
}

public sealed class InstallerSettings
{
    public string DefaultDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "FHZIPA INJECT");

    public bool CreateDesktopShortcut { get; set; } = true;

    public bool CreateStartMenuShortcut { get; set; } = true;

    public bool LaunchAfterInstall { get; set; } = true;
}

public sealed class DownloadSettings
{
    public string? ManifestUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 30;

    public int MaxRetries { get; set; } = 3;

    public bool UseEmbeddedPayload { get; set; } = true;
}

public sealed class SecuritySettings
{
    public bool RequireHashValidation { get; set; } = true;

    public bool VerifyDigitalSignatureWhenAvailable { get; set; } = true;
}
