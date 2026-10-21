using System.Reflection;
using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.Services;

/// <summary>
/// O payload que viaja dentro do instalador. O arquivo vem embutido como
/// recurso e o resumo vem de um recurso ao lado, o que permite conferir a
/// integridade antes de instalar qualquer coisa.
/// </summary>
public interface IPayloadService
{
    /// <summary>
    /// O nome do aplicativo que esta dentro do instalador.
    /// </summary>
    string FileName { get; }

    /// <summary>
    /// O resumo esperado do payload, quando ele esta disponivel.
    /// </summary>
    string? ExpectedSha256 { get; }

    /// <summary>
    /// O tamanho do payload, em bytes.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// Extrai o payload para um arquivo temporario e devolve o caminho.
    /// </summary>
    Task<string> ExtractAsync(
        string targetDirectory,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// O payload embutido no instalador.
/// </summary>
public sealed class EmbeddedPayloadService : IPayloadService
{
    private const string ResourceName = "FHZIPA.Installer.Resources.fhzipa-inject.exe";
    private const string DigestResourceName = "FHZIPA.Installer.Resources.payload.sha256";

    private readonly Assembly _assembly;

    public EmbeddedPayloadService() => _assembly = Assembly.GetExecutingAssembly();

    public string FileName => "fhzipa-inject.exe";

    public string? ExpectedSha256
    {
        get
        {
            using var stream = _assembly.GetManifestResourceStream(DigestResourceName);
            if (stream is null)
            {
                return null;
            }

            using var reader = new StreamReader(stream);
            var value = reader.ReadToEnd().Trim();
            return value.Length == 64 ? value.ToLowerInvariant() : null;
        }
    }

    public long Size
    {
        get
        {
            using var stream = _assembly.GetManifestResourceStream(ResourceName);
            return stream?.Length ?? 0;
        }
    }

    public async Task<string> ExtractAsync(
        string targetDirectory,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken)
    {
        await using var input = _assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("O instalador nao tem o aplicativo embutido.");

        Directory.CreateDirectory(targetDirectory);
        var target = Path.Combine(targetDirectory, FileName);

        await using var output = new FileStream(
            target, FileMode.Create, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);

        var total = input.Length;
        var processed = 0L;
        var buffer = new byte[1024 * 1024];
        var lastReport = DateTimeOffset.MinValue;
        var speed = new TransferSpeedEstimator();
        speed.Start(total);

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            processed += read;
            speed.Add(read);

            var now = DateTimeOffset.UtcNow;
            if ((now - lastReport).TotalMilliseconds >= 80)
            {
                lastReport = now;
                progress?.Report(new InstallerProgress
                {
                    OperationName = "Baixando arquivos",
                    CurrentStage = "Baixando",
                    CurrentMessage = FileName,
                    BytesProcessed = processed,
                    TotalBytes = total,
                    BytesPerSecond = speed.BytesPerSecond,
                    EstimatedTimeRemaining = speed.EstimatedRemaining(processed),
                });
            }
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        return target;
    }
}
