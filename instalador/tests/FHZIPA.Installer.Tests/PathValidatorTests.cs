using FHZIPA.Installer.Security;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// A validacao dos caminhos de instalacao.
/// </summary>
public class PathValidatorTests
{
    [Fact]
    public void CaminhoVazioEhRecusado()
    {
        Assert.False(PathValidator.IsValidInstallPath(string.Empty, out var problem));
        Assert.False(string.IsNullOrWhiteSpace(problem));
    }

    [Fact]
    public void CaminhoRelativoEhRecusado()
    {
        Assert.False(PathValidator.IsValidInstallPath("Program Files\\FHZIPA", out _));
    }

    [Fact]
    public void CaminhoAbsolutoDeUnidadeExistenteEhAceito()
    {
        var path = Path.Combine(Path.GetTempPath(), "fhzipa-teste-caminho");
        Assert.True(PathValidator.IsValidInstallPath(path, out var problem), problem);
    }

    [Fact]
    public void UnidadeInexistenteEhRecusada()
    {
        Assert.False(PathValidator.IsValidInstallPath("Z:\\FHZIPA INJECT", out _));
    }

    [Fact]
    public void OsDoisPontosDaUnidadeNaoCountamComoCaractereInvalido()
    {
        var path = Path.Combine(Path.GetTempPath(), "FHZIPA INJECT");
        Assert.True(PathValidator.IsValidInstallPath(path, out var problem), problem);
    }

    [Fact]
    public void CaractereInvalidoEhRecusado()
    {
        var path = Path.Combine(Path.GetTempPath(), "FHZIPA*INJECT");
        Assert.False(PathValidator.IsValidInstallPath(path, out _));
    }

    [Fact]
    public void NomeReservadoEhRecusado()
    {
        var path = Path.Combine(Path.GetTempPath(), "CON");
        Assert.False(PathValidator.IsValidInstallPath(path, out _));
    }

    [Fact]
    public void CaminhoLongoEhRecusado()
    {
        var longPath = Path.Combine(Path.GetTempPath(), new string('a', 300));
        Assert.False(PathValidator.IsValidInstallPath(longPath, out _));
    }

    [Fact]
    public void PastaInexistenteAindaEhConsideradaGravavel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fhzipa-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(path));

        Assert.True(PathValidator.IsWritable(path));

        // A pasta criada para o teste e apagada em seguida.
        Assert.False(Directory.Exists(path));
    }

    [Fact]
    public void ArquivoDentroDaAreaGerenciadaEhAceito()
    {
        var root = Path.Combine(Path.GetTempPath(), "fhzipa-area");
        var inside = Path.Combine(root, "sub", "arquivo.exe");

        Assert.True(PathValidator.IsInside(root, inside));
    }

    [Fact]
    public void ArquivoForaDaAreaGerenciadaEhRecusado()
    {
        var root = Path.Combine(Path.GetTempPath(), "fhzipa-area");
        var outside = Path.Combine(Path.GetTempPath(), "outro", "arquivo.exe");

        Assert.False(PathValidator.IsInside(root, outside));
    }

    [Fact]
    public void CaminhoRelatorioComPontoEhRecusado()
    {
        var path = Path.Combine(Path.GetTempPath(), "..", "..", "etc");
        Assert.False(PathValidator.IsInside(Path.GetTempPath(), path));
    }
}
