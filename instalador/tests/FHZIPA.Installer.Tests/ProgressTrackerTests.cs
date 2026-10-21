using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// O progresso global so anda com trabalho feito de verdade.
/// </summary>
public class ProgressTrackerTests
{
    [Fact]
    public void SemTrabalhoNaoHaProgresso()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };

        Assert.Equal(0d, tracker.Percentage, 3);
    }

    [Fact]
    public void AObtencaoPesadaNaPrimeiraParte()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };
        tracker.ObtainedBytes = 1000;

        var expected = InstallationProgressTracker.ObtainingWeight * 100d;
        Assert.Equal(expected, tracker.Percentage, 3);
    }

    [Fact]
    public void ACopiaPesadaNaSegundaParte()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };
        tracker.ObtainedBytes = 1000;
        tracker.CopiedBytes = 1000;

        var expected = (InstallationProgressTracker.ObtainingWeight + InstallationProgressTracker.CopyingWeight) * 100d;
        Assert.Equal(expected, tracker.Percentage, 3);
    }

    [Fact]
    public void ORegistroERegistradoEmTresPartes()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };

        tracker.RegisteringStepDone();
        tracker.RegisteringStepDone();
        tracker.RegisteringStepDone();

        var expected = InstallationProgressTracker.RegisteringWeight * 100d;
        Assert.Equal(expected, tracker.Percentage, 3);
    }

    [Fact]
    public void AsVerificacoesFinaisCompletamOProgresso()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };
        tracker.ObtainedBytes = 1000;
        tracker.CopiedBytes = 1000;
        tracker.RegisteringStepDone();
        tracker.RegisteringStepDone();
        tracker.RegisteringStepDone();
        tracker.FinalCheckDone();
        tracker.FinalCheckDone();
        tracker.FinalCheckDone();

        Assert.Equal(100d, tracker.Percentage, 3);
    }

    [Fact]
    public void ABarraNuncaPassaDeCem()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };
        tracker.ObtainedBytes = 5000;
        tracker.CopiedBytes = 5000;
        tracker.RegisteringStepDone();
        tracker.RegisteringStepDone();
        tracker.RegisteringStepDone();
        tracker.FinalCheckDone();
        tracker.FinalCheckDone();
        tracker.FinalCheckDone();

        Assert.Equal(100d, tracker.Percentage, 3);
    }

    [Fact]
    public void OsBytesNaoPassamDoTotalConhecido()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };
        tracker.ObtainedBytes = 9999;
        tracker.CopiedBytes = 9999;

        Assert.Equal(1000, tracker.ObtainedBytes);
        Assert.Equal(1000, tracker.CopiedBytes);
    }

    [Fact]
    public void UmaNovaTentativaComecaDoZero()
    {
        var tracker = new InstallationProgressTracker { PayloadBytes = 1000 };
        tracker.ObtainedBytes = 1000;
        tracker.CopiedBytes = 1000;
        tracker.RegisteringStepDone();

        tracker.Reset();

        Assert.Equal(0d, tracker.Percentage, 3);
        Assert.Equal(0, tracker.ObtainedBytes);
        Assert.Equal(0, tracker.CopiedBytes);
    }

    [Fact]
    public void OsPesosSomamCemPorCento()
    {
        var total = InstallationProgressTracker.ObtainingWeight +
                    InstallationProgressTracker.CopyingWeight +
                    InstallationProgressTracker.RegisteringWeight +
                    InstallationProgressTracker.FinalChecksWeight;

        Assert.Equal(1d, total, 6);
    }
}

/// <summary>
/// A maquina de estados tem que permitir repetir uma instalacao que falhou.
/// </summary>
public class RetryStateTests
{
    [Fact]
    public void UmaFalhaPodeVoltarParaADiretorio()
    {
        Assert.True(InstallerStateMachine.CanTransition(
            InstallerState.Failed,
            InstallerState.DirectorySelection));
    }

    [Fact]
    public void UmCancelamentoPodeVoltarParaADiretorio()
    {
        Assert.True(InstallerStateMachine.CanTransition(
            InstallerState.Cancelled,
            InstallerState.DirectorySelection));
    }

    [Fact]
    public void UmaFalhaAindaNaoViraSucessoDireto()
    {
        Assert.False(InstallerStateMachine.CanTransition(
            InstallerState.Failed,
            InstallerState.Completed));
    }

    [Fact]
    public void UmaFalhaPodeSerReavaliada()
    {
        Assert.True(InstallerStateMachine.CanRetry(InstallerState.Failed));
        Assert.True(InstallerStateMachine.CanRetry(InstallerState.Cancelled));
        Assert.True(InstallerStateMachine.CanRetry(InstallerState.RequirementsFailed));
        Assert.False(InstallerStateMachine.CanRetry(InstallerState.Completed));
        Assert.False(InstallerStateMachine.CanRetry(InstallerState.Welcome));
    }
}
