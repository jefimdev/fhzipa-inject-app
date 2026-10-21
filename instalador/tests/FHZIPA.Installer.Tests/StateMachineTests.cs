using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.Tests;

/// <summary>
/// A maquina de estados nao aceita transicoes invalidas.
/// </summary>
public class StateMachineTests
{
    [Fact]
    public void TransicaoInvalidaDeWelcomeParaDownloadingEhRecusada()
    {
        Assert.False(InstallerStateMachine.CanTransition(
            InstallerState.Welcome,
            InstallerState.Downloading));
    }

    [Fact]
    public void TransicaoInvalidaDeWelcomeParaCompletedEhRecusada()
    {
        Assert.False(InstallerStateMachine.CanTransition(
            InstallerState.Welcome,
            InstallerState.Completed));
    }

    [Fact]
    public void NaoSeInstalaAntesDeValidarOsArquivos()
    {
        // Do estado de pronto para instalar nao se pula direto para
        // concluido.
        Assert.False(InstallerStateMachine.CanTransition(
            InstallerState.ReadyToInstall,
            InstallerState.Completed));
    }

    [Fact]
    public void NaoSeConcluiAntesDaVerificacaoFinal()
    {
        Assert.False(InstallerStateMachine.CanTransition(
            InstallerState.Installing,
            InstallerState.Completed));
    }

    [Fact]
    public void FalhaNaoViraSucesso()
    {
        Assert.False(InstallerStateMachine.CanTransition(
            InstallerState.Failed,
            InstallerState.Completed));
    }

    [Fact]
    public void AEtapaFinalNaoSaiDeLugar()
    {
        Assert.Empty(InstallerStateMachine.TransitionsFrom(InstallerState.Completed));
    }

    [Fact]
    public void CancelamentoEstaDisponivelNasEtapasQuePodemSerInterrompidas()
    {
        var states = new[]
        {
            InstallerState.Welcome,
            InstallerState.DirectorySelection,
            InstallerState.CheckingRequirements,
            InstallerState.ReadyToDownload,
            InstallerState.Downloading,
            InstallerState.ValidatingDownloads,
            InstallerState.ReadyToInstall,
            InstallerState.Installing,
            InstallerState.InstallingDependencies,
            InstallerState.Configuring,
            InstallerState.Finalizing,
        };

        foreach (var state in states)
        {
            Assert.True(
                InstallerStateMachine.CanTransition(state, InstallerState.Cancelled),
                $"O estado {state} nao pode ser cancelado.");
        }
    }

    [Fact]
    public void OFluxoCompletoEhValido()
    {
        var flow = new[]
        {
            InstallerState.Welcome,
            InstallerState.DirectorySelection,
            InstallerState.CheckingRequirements,
            InstallerState.ReadyToDownload,
            InstallerState.Downloading,
            InstallerState.ValidatingDownloads,
            InstallerState.ReadyToInstall,
            InstallerState.Installing,
            InstallerState.InstallingDependencies,
            InstallerState.Configuring,
            InstallerState.Finalizing,
            InstallerState.Completed,
        };

        for (var index = 0; index < flow.Length - 1; index++)
        {
            Assert.True(
                InstallerStateMachine.CanTransition(flow[index], flow[index + 1]),
                $"A transicao de {flow[index]} para {flow[index + 1]} deveria valer.");
        }
    }

    [Fact]
    public void AVerificacaoComFalhaVoltaParaOMesmoCaminho()
    {
        Assert.True(InstallerStateMachine.CanTransition(
            InstallerState.RequirementsFailed,
            InstallerState.CheckingRequirements));
    }

    [Fact]
    public void AsEtapasQueEstaoRodandoSaoConhecidas()
    {
        Assert.True(InstallerStateMachine.IsBusy(InstallerState.Installing));
        Assert.True(InstallerStateMachine.IsBusy(InstallerState.Downloading));
        Assert.False(InstallerStateMachine.IsBusy(InstallerState.Welcome));
        Assert.False(InstallerStateMachine.IsBusy(InstallerState.Completed));
    }
}
