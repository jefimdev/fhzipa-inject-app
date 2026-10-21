using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// A elevacao: o UAC so e pedido quando a pasta exige administrador.
/// </summary>
public class ElevationServiceTests
{
    private readonly ElevationService _service = new();

    [Fact]
    public void PastaDoUsuarioNaoExigeElevacao()
    {
        var path = Path.Combine(Path.GetTempPath(), "fhzipa-usuario");

        Assert.False(_service.RequiresElevation(path));
    }

    [Fact]
    public void PastaDeProgramasExigeElevacao()
    {
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "FHZIPA INJECT");

        // Quando o teste ja roda como administrador, a exigencia some.
        var expected = !_service.IsElevated;

        Assert.Equal(expected, _service.RequiresElevation(path));
    }

    [Fact]
    public void PastaVaziaNaoExigeElevacao()
    {
        Assert.False(_service.RequiresElevation(string.Empty));
    }

    [Fact]
    public void CaminhoRelativoNaoQuebra()
    {
        Assert.False(_service.RequiresElevation("FHZIPA INJECT"));
    }
}
