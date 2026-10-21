namespace FHZIPA.Installer.Models;

/// <summary>
/// Converte o trabalho real da instalacao em uma porcentagem global.
///
/// A barra nao anda por relogio: ela anda porque alguma coisa foi
/// efetivamente feita. O trabalho e dividido em partes com peso conhecido:
///
///   obtencao do pacote  30 %   bytes realmente extraidos do instalador
///   copia dos arquivos  45 %   bytes realmente gravados no destino
///   registro e atalhos  15 %   tres operacoes discretas, 5 % cada
///   verificacoes finais 10 %   tres verificacoes, um terco cada
///
/// A soma e 100 % e a barra so chega no fim quando as verificacoes
/// finais passam. As operacoes sem medida de bytes (validar o resumo,
/// criar atalho, escrever o registro) entram como fracao do bloco a que
/// pertencem, e nao como tempo.
/// </summary>
public sealed class InstallationProgressTracker
{
    /// <summary>Peso da obtencao do pacote dentro do trabalho total.</summary>
    public const double ObtainingWeight = 0.30;

    /// <summary>Peso da copia dos arquivos dentro do trabalho total.</summary>
    public const double CopyingWeight = 0.45;

    /// <summary>Peso do registro, dos atalhos e do desinstalador.</summary>
    public const double RegisteringWeight = 0.15;

    /// <summary>Peso das verificacoes finais.</summary>
    public const double FinalChecksWeight = 0.10;

    private const int RegisteringSteps = 3;
    private const int FinalCheckSteps = 3;

    private long _payloadBytes;
    private long _obtainedBytes;
    private long _copiedBytes;
    private int _registeringStepsDone;
    private int _finalChecksDone;

    /// <summary>
    /// O tamanho do payload, em bytes. Sem ele nao ha como medir o
    /// progresso de obtencao nem de copia.
    /// </summary>
    public long PayloadBytes
    {
        get => _payloadBytes;
        set => _payloadBytes = Math.Max(0, value);
    }

    /// <summary>Bytes realmente extraidos do instalador.</summary>
    public long ObtainedBytes
    {
        get => _obtainedBytes;
        set => _obtainedBytes = Math.Max(0, Math.Min(value, _payloadBytes));
    }

    /// <summary>Bytes realmente gravados no diretorio de destino.</summary>
    public long CopiedBytes
    {
        get => _copiedBytes;
        set => _copiedBytes = Math.Max(0, Math.Min(value, _payloadBytes));
    }

    /// <summary>
    /// Marca uma operacao discreta de registro, atalho ou desinstalador.
    /// </summary>
    public void RegisteringStepDone() =>
        _registeringStepsDone = Math.Min(RegisteringSteps, _registeringStepsDone + 1);

    /// <summary>
    /// Marca uma verificacao final que passou.
    /// </summary>
    public void FinalCheckDone() =>
        _finalChecksDone = Math.Min(FinalCheckSteps, _finalChecksDone + 1);

    /// <summary>
    /// Zera o rastreamento para uma nova tentativa.
    /// </summary>
    public void Reset()
    {
        _obtainedBytes = 0;
        _copiedBytes = 0;
        _registeringStepsDone = 0;
        _finalChecksDone = 0;
    }

    /// <summary>
    /// A fracao do trabalho de obtencao que ja foi feita, de 0 a 1.
    /// </summary>
    public double ObtainingRatio => _payloadBytes > 0
        ? Math.Clamp((double)_obtainedBytes / _payloadBytes, 0d, 1d)
        : 0d;

    /// <summary>
    /// A fracao do trabalho de copia que ja foi feita, de 0 a 1.
    /// </summary>
    public double CopyingRatio => _payloadBytes > 0
        ? Math.Clamp((double)_copiedBytes / _payloadBytes, 0d, 1d)
        : 0d;

    /// <summary>
    /// A porcentagem global, de 0 a 100.
    /// </summary>
    public double Percentage
    {
        get
        {
            var obtained = ObtainingRatio * ObtainingWeight;
            var copied = CopyingRatio * CopyingWeight;
            var registering = (double)_registeringStepsDone / RegisteringSteps * RegisteringWeight;
            var checks = (double)_finalChecksDone / FinalCheckSteps * FinalChecksWeight;

            return Math.Clamp((obtained + copied + registering + checks) * 100d, 0d, 100d);
        }
    }

    /// <summary>
    /// O progresso real, em bytes, da operacao que esta em andamento.
    /// Serve para a tela mostrar "12 MB de 24 MB" sem inventar numero.
    /// </summary>
    public (long Processed, long Total) Bytes =>
        _copiedBytes > 0 ? (_copiedBytes, _payloadBytes) : (_obtainedBytes, _payloadBytes);
}
