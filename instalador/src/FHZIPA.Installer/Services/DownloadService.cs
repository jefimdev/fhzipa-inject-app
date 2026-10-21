using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using FHZIPA.Installer.Models;

namespace FHZIPA.Installer.Services;

/// <summary>
/// O gerenciador de downloads. E um servico separado da interface, e
/// trabalha em HTTPS, com retomada, tentativas limitadas e verificacao de
/// integridade.
/// </summary>
public interface IDownloadService
{
    /// <summary>
    /// Baixa um arquivo, com progresso real.
    /// </summary>
    Task<DownloadResult> DownloadAsync(
        ManifestFile file,
        string destinationDirectory,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken);
}

/// <summary>
/// O resultado de um download.
/// </summary>
public sealed record DownloadResult(
    bool Succeeded,
    string? Path,
    InstallerError? Error,
    bool Resumed);

/// <summary>
/// O gerenciador de downloads.
/// </summary>
public sealed class DownloadService : IDownloadService
{
    private readonly ILogService _log;
    private readonly HttpClient _client;

    public DownloadService(ILogService log, TimeSpan timeout)
    {
        _log = log;
        _client = new HttpClient
        {
            Timeout = timeout,
            // A validacao do certificado do servidor fica ligada, ela nunca
            // e desligada.
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("FHZIPA-INJECT-Installer/1.0");
    }

    public async Task<DownloadResult> DownloadAsync(
        ManifestFile file,
        string destinationDirectory,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!file.Url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return new DownloadResult(
                false,
                null,
                InstallerError.Create(
                    InstallerErrorCode.TlsFailure,
                    "A origem do arquivo nao e segura."),
                false);
        }

        Directory.CreateDirectory(destinationDirectory);
        var target = Path.Combine(destinationDirectory, file.Name);
        var partial = target + ".parcial";

        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var resumed = await DownloadOnceAsync(file, partial, progress, cancellationToken)
                    .ConfigureAwait(false);

                var digest = await ComputeSha256Async(partial, cancellationToken).ConfigureAwait(false);
                if (file.HasSha256 &&
                    !string.Equals(digest, file.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(partial);
                    return new DownloadResult(
                        false,
                        null,
                        InstallerError.Create(
                            InstallerErrorCode.IntegrityCheckFailed,
                            $"Esperado {file.Sha256}, encontrado {digest}."),
                        false);
                }

                File.Move(partial, target, overwrite: true);
                return new DownloadResult(true, target, null, resumed);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // O tempo da requisicao acabou. Isso e diferente de uma
                // cancelamento pedido pelo usuario.
                SafeDelete(partial);
                if (attempt == 3)
                {
                    return new DownloadResult(
                        false,
                        null,
                        InstallerError.Create(InstallerErrorCode.DownloadTimedOut),
                        false);
                }

                await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                SafeDelete(partial);
                throw;
            }
            catch (HttpRequestException exception)
            {
                _log.Warn("Download falhou na tentativa {0}: {1}", attempt, exception.Message);
                SafeDelete(partial);

                if (attempt == 3)
                {
                    return new DownloadResult(
                        false,
                        null,
                        InstallerError.Create(InstallerErrorCode.ServerUnavailable, exception.Message),
                        false);
                }

                await Task.Delay(Backoff(attempt), cancellationToken).ConfigureAwait(false);
            }
        }

        return new DownloadResult(
            false,
            null,
            InstallerError.Create(InstallerErrorCode.DownloadInterrupted),
            false);
    }

    private async Task<bool> DownloadOnceAsync(
        ManifestFile file,
        string partialPath,
        IProgress<InstallerProgress>? progress,
        CancellationToken cancellationToken)
    {
        var resumed = false;
        long already = 0;

        if (File.Exists(partialPath))
        {
            already = new FileInfo(partialPath).Length;
            resumed = true;
        }

        var request = new HttpRequestMessage(HttpMethod.Get, file.Url);
        if (resumed && already < file.Size)
        {
            // A retomada so acontece quando o arquivo bate com o tamanho
            // esperado, o que evita juntar pedacos de versoes diferentes.
            request.Headers.Range = new RangeHeaderValue(already, file.Size - 1);
        }
        else
        {
            resumed = false;
            already = 0;
        }

        using var response = await _client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (resumed && response.StatusCode != HttpStatusCode.PartialContent)
        {
            // O servidor nao aceitou o intervalo, o download recomeca.
            resumed = false;
            already = 0;
        }

        var mode = resumed ? FileMode.Append : FileMode.Create;
        await using var output = new FileStream(
            partialPath, mode, FileAccess.Write, FileShare.None, 1024 * 1024, useAsync: true);
        await using var input = await response.Content
            .ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);

        var estimator = new TransferSpeedEstimator();
        estimator.Start(file.Size);

        var buffer = new byte[1024 * 1024];
        var processed = already;
        var lastReport = DateTimeOffset.MinValue;

        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            processed += read;
            estimator.Add(read);

            var now = DateTimeOffset.UtcNow;
            if ((now - lastReport).TotalMilliseconds >= 100)
            {
                lastReport = now;
                progress?.Report(new InstallerProgress
                {
                    OperationName = "Baixando arquivos",
                    CurrentStage = "Baixando",
                    CurrentMessage = file.Name,
                    BytesProcessed = processed,
                    TotalBytes = file.Size,
                    EstimatedTimeRemaining = estimator.EstimatedRemaining(processed),
                });
            }
        }

        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        return resumed;
    }

    private static TimeSpan Backoff(int attempt)
    {
        // Espera exponencial com um desvio, para nao repetir tudo no mesmo
        // instante.
        var seconds = Math.Pow(2, attempt);
        var jitter = Random.Shared.NextDouble() * 0.5;
        return TimeSpan.FromSeconds(seconds + jitter);
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, useAsync: true);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
