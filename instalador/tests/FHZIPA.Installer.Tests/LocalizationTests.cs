using System.Text.RegularExpressions;
using FHZIPA.Installer.Localization;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// Os textos da interface: toda chave usada no XAML existe no arquivo de
/// recursos, e o ingles tem as mesmas chaves do portugues.
/// </summary>
public class LocalizationTests
{
    private static string FindSourceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "FHZIPA.Installer");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("A pasta do codigo do instalador nao foi encontrada.");
    }

    private static IReadOnlyList<string> KeysUsedInXaml()
    {
        var pattern = new Regex(@"\{loc:Loc\s+Key\s*=\s*([A-Za-z0-9_]+)\}");
        var keys = new List<string>();

        foreach (var file in Directory.EnumerateFiles(FindSourceRoot(), "*.xaml", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            foreach (Match match in pattern.Matches(content))
            {
                keys.Add(match.Groups[1].Value);
            }
        }

        return keys;
    }

    [Fact]
    public void TodaChaveUsadaNoXamlExisteNoArquivoDeRecursos()
    {
        var existing = Strings.AllKeys().ToHashSet(StringComparer.Ordinal);

        foreach (var key in KeysUsedInXaml())
        {
            Assert.True(existing.Contains(key), $"A chave '{key}' nao existe no arquivo de recursos.");
        }
    }

    [Fact]
    public void OTextoDeUmaChaveAusenteMostraOProprioNome()
    {
        var text = Strings.Get("ChaveQueNaoExiste_1234");

        Assert.Contains("ChaveQueNaoExiste_1234", text);
    }

    [Fact]
    public void OTextoFormatadoRecebeOsArgumentos()
    {
        var text = Strings.Get("Footer_Version", "1.2.3");

        Assert.Contains("1.2.3", text);
    }

    [Fact]
    public void OArquivoDeRecursosNaoEstaVazio()
    {
        Assert.True(Strings.AllKeys().Count() > 50);
    }

    [Fact]
    public void TodaChaveUsadaNoCodigoExisteNoArquivoDeRecursos()
    {
        var existing = Strings.AllKeys().ToHashSet(StringComparer.Ordinal);
        var pattern = new Regex(@"Strings\.Get\(""([^""]+)""");

        foreach (var file in Directory.EnumerateFiles(FindSourceRoot(), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains("Localization", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            foreach (Match match in pattern.Matches(content))
            {
                var key = match.Groups[1].Value;
                Assert.True(existing.Contains(key), $"A chave '{key}' nao existe no arquivo de recursos.");
            }
        }
    }

    [Fact]
    public void OInglesTemAsMesmasChavesDoPortugues()
    {
        var neutral = Strings.AllKeys().ToHashSet(StringComparer.Ordinal);
        var english = new HashSet<string>(StringComparer.Ordinal);

        var manager = new System.Resources.ResourceManager(
            "FHZIPA.Installer.Localization.Strings",
            typeof(Strings).Assembly);

        var culture = new System.Globalization.CultureInfo("en");
        var set = manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);

        Assert.NotNull(set);
        foreach (System.Collections.DictionaryEntry entry in set!)
        {
            if (entry.Key is string key)
            {
                english.Add(key);
            }
        }

        foreach (var key in neutral)
        {
            Assert.True(english.Contains(key), $"A chave '{key}' nao existe no arquivo de recursos em ingles.");
        }
    }
}
