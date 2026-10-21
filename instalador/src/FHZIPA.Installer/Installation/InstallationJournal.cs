using System.Text.Json;

namespace FHZIPA.Installer.Installation;

/// <summary>
/// O registro do que a instalacao fez. E o que permite desfazer.
/// </summary>
public sealed class InstallationJournal
{
    public string InstallDirectory { get; set; } = string.Empty;

    public List<string> CreatedDirectories { get; set; } = [];

    public List<string> InstalledFiles { get; set; } = [];

    public List<string> BackedUpFiles { get; set; } = [];

    public List<string> CreatedShortcuts { get; set; } = [];

    public List<string> CreatedRegistryKeys { get; set; } = [];

    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;

    public bool Completed { get; set; }

    /// <summary>
    /// Grava o registro, para que uma interrupcao ainda possa ser
    /// desfeita no proximo arranque.
    /// </summary>
    public void Save(string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "journal.json");
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Le o registro de uma instalacao interrompida.
    /// </summary>
    public static InstallationJournal? Load(string directory)
    {
        var path = Path.Combine(directory, "journal.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<InstallationJournal>(File.ReadAllText(path));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Apaga o registro, quando a instalacao terminou bem.
    /// </summary>
    public static void Clear(string directory)
    {
        var path = Path.Combine(directory, "journal.json");
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
