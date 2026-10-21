using FHZIPA.Installer.Infrastructure;
using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// O manifesto de distribuicao e a configuracao do instalador.
/// </summary>
public class ManifestTests
{
    [Fact]
    public void ManifestoValidoNaoTemProblemas()
    {
        var manifest = new DistributionManifest
        {
            Version = "1.0.0",
            Files =
            [
                new ManifestFile
                {
                    Name = "aplicativo.exe",
                    Url = "https://exemplo.test/aplicativo.exe",
                    Size = 1024,
                    Sha256 = new string('a', 64),
                },
            ],
        };

        Assert.Empty(manifest.Validate());
    }

    [Fact]
    public void ManifestoSemVersaoEhRecusado()
    {
        var manifest = new DistributionManifest();

        Assert.Contains(manifest.Validate(), problem => problem.Contains("versao"));
    }

    [Fact]
    public void ManifestoDeOutraPlataformaEhRecusado()
    {
        var manifest = new DistributionManifest { Version = "1.0.0", Platform = "android" };

        Assert.Contains(manifest.Validate(), problem => problem.Contains("plataforma"));
    }

    [Fact]
    public void ArquivoSemResumoEhRecusado()
    {
        var manifest = new DistributionManifest
        {
            Version = "1.0.0",
            Files =
            [
                new ManifestFile
                {
                    Name = "aplicativo.exe",
                    Url = "https://exemplo.test/aplicativo.exe",
                    Size = 1024,
                },
            ],
        };

        Assert.Contains(manifest.Validate(), problem => problem.Contains("SHA-256"));
    }

    [Fact]
    public void ResumoDeTamanhoErradoNaoValeComoResumo()
    {
        var file = new ManifestFile { Sha256 = "abc" };

        Assert.False(file.HasSha256);
    }

    [Fact]
    public void TextoJsonInvalidoDevolveNulo()
    {
        Assert.Null(DistributionManifest.Parse("{ isto nao e json"));
    }

    [Fact]
    public void ConfiguracaoCompletaNaoTemProblemas()
    {
        var configuration = new AppConfiguration();

        Assert.Empty(configuration.Validate());
    }

    [Fact]
    public void ConfiguracaoSemNomeDeAplicativoEhRecusada()
    {
        var configuration = new AppConfiguration
        {
            Application = new ApplicationSettings { Name = string.Empty },
        };

        Assert.Contains(configuration.Validate(), problem => problem.Contains("Application.Name"));
    }

    [Fact]
    public void ConfiguracaoComTempoInvalidoEhRecusada()
    {
        var configuration = new AppConfiguration
        {
            Downloads = new DownloadSettings { TimeoutSeconds = 0 },
        };

        Assert.Contains(configuration.Validate(), problem => problem.Contains("TimeoutSeconds"));
    }
}

/// <summary>
/// O catalogo de erros.
/// </summary>
public class ErrorCatalogTests
{
    [Fact]
    public void TodoErroTemDescricaoEacao()
    {
        var codes = Enum.GetValues<InstallerErrorCode>()
            .Where(code => code != InstallerErrorCode.None)
            .ToList();

        Assert.NotEmpty(codes);

        foreach (var code in codes)
        {
            var error = InstallerError.Create(code);

            Assert.False(string.IsNullOrWhiteSpace(error.Title));
            Assert.False(string.IsNullOrWhiteSpace(error.Description));
            Assert.False(string.IsNullOrWhiteSpace(error.Action));
        }
    }

    [Fact]
    public void OCodigoTemOFormatoDaCasa()
    {
        var error = InstallerError.Create(InstallerErrorCode.IntegrityCheckFailed);

        Assert.Equal("FHZ-3003", error.CodeText);
    }

    [Fact]
    public void ODetalheEntraNaDescricao()
    {
        var error = InstallerError.Create(InstallerErrorCode.CopyFailed, "disco cheio");

        Assert.Contains("disco cheio", error.Description);
    }
}
