using Microsoft.Win32;

namespace FHZIPA.Installer.Services;

/// <summary>
/// As janelas de dialogo do Windows. Ficam atras de uma interface para que
/// a camada de apresentacao possa ser testada sem abrir janelas.
/// </summary>
public interface IDialogService
{
    /// <summary>
    /// Abre o seletor de pasta do Windows.
    /// </summary>
    string? PickFolder(string initialPath);

    /// <summary>
    /// Mostra uma mensagem com um botao.
    /// </summary>
    void ShowInformation(string title, string message);

    /// <summary>
    /// Mostra uma mensagem de erro.
    /// </summary>
    void ShowError(string title, string message);

    /// <summary>
    /// Pergunta ao usuario e devolve a resposta.
    /// </summary>
    bool Confirm(string title, string message, string yesText, string noText);
}

/// <summary>
/// Os dialogos de verdade, usando as janelas do Windows.
/// </summary>
public sealed class DialogService : IDialogService
{
    public string? PickFolder(string initialPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Selecione a pasta de instalacao",
            Multiselect = false,
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : null,
        };

        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowInformation(string title, string message) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);

    public void ShowError(string title, string message) =>
        System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);

    public bool Confirm(string title, string message, string yesText, string noText)
    {
        var result = System.Windows.MessageBox.Show(
            message,
            title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No);

        return result == System.Windows.MessageBoxResult.Yes;
    }
}
