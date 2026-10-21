namespace FHZIPA.Installer.Models;

/// <summary>
/// O estado de uma operacao em andamento. A interface observa esta
/// estrutura e nunca calcula porcentagem por conta propria.
/// </summary>
public sealed class InstallerProgress
{
    public Guid OperationId { get; init; } = Guid.NewGuid();

    public string OperationName { get; set; } = string.Empty;

    public string CurrentStage { get; set; } = string.Empty;

    public string StageStatus { get; set; } = string.Empty;

    public long BytesProcessed { get; set; }

    public long TotalBytes { get; set; }

    /// <summary>
    /// A porcentagem, de 0 a 100. Quando o total e desconhecido a
    /// operacao fica indeterminada e a porcentagem vale zero.
    /// </summary>
    public double ProgressPercentage => TotalBytes > 0
        ? Math.Clamp(BytesProcessed * 100d / TotalBytes, 0d, 100d)
        : 0d;

    /// <summary>
    /// A velocidade da transferencia, em bytes por segundo.
    /// </summary>
    public double BytesPerSecond { get; set; }

    public bool IsIndeterminate { get; set; }

    public string CurrentMessage { get; set; } = string.Empty;

    public TimeSpan? EstimatedTimeRemaining { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Calcula a velocidade de transferencia e o tempo restante.
///
/// A velocidade usa uma janela de tempo estavel: a media sai da janela e
/// nao do ultimo instante, senao a leitura oscila a cada quadro.
/// </summary>
public sealed class TransferSpeedEstimator
{
    private const int WindowSeconds = 4;

    private readonly Queue<(DateTimeOffset At, long Bytes)> _samples = new();
    private long _totalBytes;
    private DateTimeOffset _startedAt;

    public void Start(long totalBytes)
    {
        _samples.Clear();
        _totalBytes = totalBytes;
        _startedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Registra mais bytes recebidos.
    /// </summary>
    public void Add(long bytes)
    {
        _samples.Enqueue((DateTimeOffset.UtcNow, bytes));
        Trim();
    }

    /// <summary>
    /// A velocidade, em bytes por segundo. Zero enquanto nao ha dados
    /// suficientes para uma leitura confiavel.
    /// </summary>
    public double BytesPerSecond
    {
        get
        {
            Trim();
            if (_samples.Count < 2)
            {
                return 0;
            }

            var span = (_samples.Last().At - _samples.First().At).TotalSeconds;
            if (span <= 0.1)
            {
                return 0;
            }

            var total = _samples.Sum(sample => sample.Bytes);
            return total / span;
        }
    }

    /// <summary>
    /// O tempo que falta, quando a velocidade e confiavel. Sem estimativa
    /// nao se inventa um numero.
    /// </summary>
    public TimeSpan? EstimatedRemaining(long bytesProcessed)
    {
        var speed = BytesPerSecond;
        if (speed <= 0 || _totalBytes <= 0 || bytesProcessed >= _totalBytes)
        {
            return null;
        }

        var remaining = _totalBytes - bytesProcessed;
        return TimeSpan.FromSeconds(remaining / speed);
    }

    private void Trim()
    {
        var limit = DateTimeOffset.UtcNow.AddSeconds(-WindowSeconds);
        while (_samples.Count > 0 && _samples.First().At < limit)
        {
            _samples.Dequeue();
        }
    }
}
