using FHZIPA.Installer.Localization;
using FHZIPA.Installer.Models;
using FHZIPA.Installer.Services;

namespace FHZIPA.Installer.ViewModels;

/// <summary>
/// A tela de obtencao dos arquivos. O pacote vem dentro do instalador e e
/// conferido pelo resumo antes de qualquer escrita.
/// </summary>
public sealed class DownloadViewModel : StepViewModelBase
{
    private readonly IPayloadService _payload;

    private string? _extractedPath;
    private string _statusMessage = string.Empty;

    public DownloadViewModel(MainViewModel main, IPayloadService payload)
        : base(main)
    {
        _payload = payload;
    }

    public string Title => Strings.Get("Download_Title");

    public string Description => Strings.Get("Download_Description");

    public string LocalPackageText => Strings.Get("Download_Local", _payload.FileName);

    public string VerifyingText => Strings.Get("Download_Verifying");

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>
    /// O caminho do pacote ja extraido.
    /// </summary>
    public string? ExtractedPath => _extractedPath;

    /// <summary>
    /// O texto do arquivo, como na referencia.
    /// </summary>
    public string FileLabel => Strings.Get("Download_FileLabel");

    public string FileName { get; private set; } = string.Empty;

    public string SizeText { get; private set; } = string.Empty;

    public string SpeedText { get; private set; } = string.Empty;

    public string RemainingText { get; private set; } = string.Empty;


    /// <summary>
    /// Preenche os campos da tela com um relatorio de progresso. E o que
    /// o modo de previa usa para mostrar os mesmos numeros que a
    /// instalacao de verdade mostraria.
    /// </summary>
    public void Report(InstallerProgress report)
    {
        FileName = report.CurrentMessage;
        SizeText = report.TotalBytes > 0
            ? MainViewModel.FormatBytes(report.BytesProcessed) + " / " + MainViewModel.FormatBytes(report.TotalBytes)
            : string.Empty;
        SpeedText = report.BytesPerSecond > 0
            ? MainViewModel.FormatBytes((long)report.BytesPerSecond) + "/s"
            : "--";
        RemainingText = report.EstimatedTimeRemaining is { } remaining
            ? MainViewModel.FormatDuration(remaining)
            : "--";
    }

    public override bool CanContinue => false;

    public override bool CanGoBack => false;

    /// <summary>
    /// Extrai o pacote embutido para uma pasta temporaria e confere o
    /// resumo do arquivo.
    /// </summary>
    public async Task<string> RunAsync(string workingDirectory, CancellationToken cancellationToken)
    {
        var progress = new Progress<InstallerProgress>(report =>
        {
            Main.ReportProgress(report);
            Report(report);
        });

        var target = Path.Combine(workingDirectory, "payload");
        var path = await _payload.ExtractAsync(target, progress, cancellationToken).ConfigureAwait(true);

        var expected = _payload.ExpectedSha256;
        if (!string.IsNullOrEmpty(expected))
        {
            StatusMessage = VerifyingText;
            Main.ReportStatus(VerifyingText);

            var actual = await Services.InstallationService
                .ComputeSha256Async(path, cancellationToken)
                .ConfigureAwait(true);

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"O resumo do aplicativo nao confere. Esperado {expected}, encontrado {actual}.");
            }
        }

        _extractedPath = path;
        StatusMessage = Strings.Get("Install_Done");
        return path;
    }

    public override Task ContinueAsync() => Task.CompletedTask;

    public override Task BackAsync() => Task.CompletedTask;
}
