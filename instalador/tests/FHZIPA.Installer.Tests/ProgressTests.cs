using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// O calculo do progresso e a leitura de velocidade.
/// </summary>
public class ProgressTests
{
    [Fact]
    public void ProgressoDeZeroQuandoOTotalEDesconhecido()
    {
        var progress = new InstallerProgress { BytesProcessed = 500, TotalBytes = 0 };

        Assert.Equal(0, progress.ProgressPercentage);
        Assert.False(progress.IsIndeterminate);
    }

    [Fact]
    public void ProgressoCalculadoSobreOTotal()
    {
        var progress = new InstallerProgress { BytesProcessed = 250, TotalBytes = 1000 };

        Assert.Equal(25, progress.ProgressPercentage);
    }

    [Fact]
    public void ProgressoNuncaPassaDeCem()
    {
        var progress = new InstallerProgress { BytesProcessed = 5000, TotalBytes = 1000 };

        Assert.Equal(100, progress.ProgressPercentage);
    }

    [Fact]
    public void ProgressoNuncaFicaNegativo()
    {
        var progress = new InstallerProgress { BytesProcessed = -10, TotalBytes = 1000 };

        Assert.Equal(0, progress.ProgressPercentage);
    }

    [Fact]
    public void VelocidadeFicaZeroSemAmostrasSuficientes()
    {
        var estimator = new TransferSpeedEstimator();
        estimator.Start(1000);

        Assert.Equal(0, estimator.BytesPerSecond);
    }

    [Fact]
    public void SemEstimativaQuandoAVelocidadeNaoEhConfiavel()
    {
        var estimator = new TransferSpeedEstimator();
        estimator.Start(1000);
        estimator.Add(100);

        Assert.Null(estimator.EstimatedRemaining(100));
    }

    [Fact]
    public void TempoRestanteCalculadoComAVelocidade()
    {
        var estimator = new TransferSpeedEstimator();
        estimator.Start(10_000);

        // Duas amostras com um intervalo de tempo real entre elas.
        estimator.Add(1000);
        Thread.Sleep(700);
        estimator.Add(1000);

        var remaining = estimator.EstimatedRemaining(2000);

        Assert.NotNull(remaining);
        Assert.True(remaining!.Value.TotalSeconds > 0);
    }

    [Fact]
    public void NaoHaTempoRestanteQuandoTudoJaBaixou()
    {
        var estimator = new TransferSpeedEstimator();
        estimator.Start(1000);
        estimator.Add(500);
        Thread.Sleep(700);
        estimator.Add(500);

        Assert.Null(estimator.EstimatedRemaining(1000));
    }
}
