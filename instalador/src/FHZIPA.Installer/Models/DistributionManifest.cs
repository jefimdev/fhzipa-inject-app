using System.Text.Json;
using System.Text.Json.Serialization;

namespace FHZIPA.Installer.Models;

/// <summary>
/// O manifesto de distribuicao. Ele descreve o que sera instalado e de onde
/// vem, com o resumo do conteudo para a verificacao de integridade.
/// </summary>
public sealed class DistributionManifest
{
    /// <summary>A versao do manifesto, para evolucoes do formato.</summary>
    [JsonPropertyName("schema")]
    public int Schema { get; set; } = 1;

    [JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("platform")]
    public string Platform { get; set; } = "windows";

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "x64";

    [JsonPropertyName("files")]
    public List<ManifestFile> Files { get; set; } = [];

    [JsonPropertyName("dependencies")]
    public List<ManifestDependency> Dependencies { get; set; } = [];

    [JsonPropertyName("releaseNotes")]
    public string? ReleaseNotes { get; set; }

    /// <summary>
    /// Valida a forma do manifesto. Um manifesto invalido interrompe a
    /// operacao, ele nunca e usado pela metade.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (Schema != 1)
        {
            problems.Add($"O formato do manifesto e {Schema} e o instalador entende o formato 1.");
        }

        if (string.IsNullOrWhiteSpace(Version))
        {
            problems.Add("O manifesto nao informa a versao.");
        }

        if (!string.Equals(Platform, "windows", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"O manifesto e para a plataforma '{Platform}' e nao para o Windows.");
        }

        if (!string.Equals(Architecture, "x64", StringComparison.OrdinalIgnoreCase))
        {
            problems.Add($"O manifesto e para a arquitetura '{Architecture}' e nao para x64.");
        }

        foreach (var file in Files)
        {
            if (string.IsNullOrWhiteSpace(file.Url))
            {
                problems.Add($"O arquivo '{file.Name}' nao tem origem.");
            }

            if (file.Size <= 0)
            {
                problems.Add($"O arquivo '{file.Name}' nao informa o tamanho.");
            }

            if (!file.HasSha256)
            {
                problems.Add($"O arquivo '{file.Name}' nao informa o resumo SHA-256.");
            }
        }

        return problems;
    }

    /// <summary>
    /// Le o manifesto de um texto json.
    /// </summary>
    public static DistributionManifest? Parse(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
            };

            return JsonSerializer.Deserialize<DistributionManifest>(json, options);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// Um arquivo do manifesto.
/// </summary>
public sealed class ManifestFile
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    /// <summary>
    /// Se o manifesto traz o resumo do conteudo.
    /// </summary>
    public bool HasSha256 =>
        !string.IsNullOrWhiteSpace(Sha256) && Sha256.Length == 64;
}

/// <summary>
/// Uma dependencia do manifesto.
/// </summary>
public sealed class ManifestDependency
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("minimumVersion")]
    public string? MinimumVersion { get; set; }

    [JsonPropertyName("architecture")]
    public string Architecture { get; set; } = "x64";

    [JsonPropertyName("officialUrl")]
    public string? OfficialUrl { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("installCommand")]
    public string? InstallCommand { get; set; }

    [JsonPropertyName("requiresRestart")]
    public bool RequiresRestart { get; set; }
}
