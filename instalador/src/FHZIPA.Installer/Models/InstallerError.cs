namespace FHZIPA.Installer.Models;

/// <summary>
/// O catalogo de erros do instalador. Cada codigo tem uma descricao e uma
/// acao recomendada, e o texto vai para o usuario e para o log.
/// </summary>
public enum InstallerErrorCode
{
    None = 0,

    // Configuracao
    InvalidConfiguration = 1001,
    AnotherInstanceRunning = 1002,
    MissingPayload = 1003,

    // Sistema
    UnsupportedOperatingSystem = 2001,
    InsufficientDiskSpace = 2002,
    InsufficientPermissions = 2003,
    InvalidInstallDirectory = 2004,
    WebView2Missing = 2005,
    NetworkUnavailable = 2006,

    // Rede e download
    ServerUnavailable = 3001,
    DownloadInterrupted = 3002,
    IntegrityCheckFailed = 3003,
    DownloadTimedOut = 3004,
    TlsFailure = 3005,

    // Instalacao
    ExtractionFailed = 4001,
    FileLocked = 4002,
    CopyFailed = 4003,
    ShortcutCreationFailed = 4004,

    // Dependencias
    DependencyMissing = 5001,
    DependencyFailed = 5002,

    // Fechamento
    FinalizationFailed = 6001,
    ApplicationLaunchFailed = 6002,

    // Recuperacao
    RollbackFailed = 7001,
    RecoveryRequired = 7002,

    // Cancelamento
    CancelledByUser = 8001,
}

/// <summary>
/// A descricao de um erro, com a acao que o usuario pode tomar.
/// </summary>
/// <param name="Code">O codigo do erro.</param>
/// <param name="Title">O titulo, curto.</param>
/// <param name="Description">O que aconteceu.</param>
/// <param name="Action">O que o usuario pode fazer.</param>
public sealed record InstallerError(
    InstallerErrorCode Code,
    string Title,
    string Description,
    string Action)
{
    /// <summary>
    /// O erro corrente, quando ha algum.
    /// </summary>
    public InstallerError? Error { get; init; }

    /// <summary>
    /// O codigo em texto, como aparece nos logs.
    /// </summary>
    public string CodeText => $"FHZ-{(int)Code}";

    public static InstallerError? None => null;

    /// <summary>
    /// Monta o erro a partir do catalogo, com os textos traduzidos.
    /// </summary>
    public static InstallerError Create(InstallerErrorCode code, string? detail = null)
    {
        var (title, description, action) = code switch
        {
            InstallerErrorCode.InvalidConfiguration =>
                ("Configuracao invalida", "O instalador nao conseguiu ler a sua configuracao.", "Reinstale o instalador a partir do pacote original."),
            InstallerErrorCode.AnotherInstanceRunning =>
                ("Ja existe uma instalacao em andamento", "Outro instalador do FHZIPA INJECT esta aberto neste momento.", "Feche a outra janela do instalador e tente de novo."),
            InstallerErrorCode.MissingPayload =>
                ("O aplicativo nao veio no instalador", "O arquivo do FHZIPA INJECT nao foi encontrado dentro do instalador.", "Baixe o instalador novamente e nao mova nenhum dos arquivos da pasta."),
            InstallerErrorCode.UnsupportedOperatingSystem =>
                ("Sistema operacional nao suportado", "Este instalador precisa do Windows 10 ou 11, de 64 bits.", "Atualize o Windows e rode o instalador de novo."),
            InstallerErrorCode.InsufficientDiskSpace =>
                ("Espaco em disco insuficiente", "Nao ha espaco suficiente para a instalacao.", "Libere espaco e execute o instalador novamente."),
            InstallerErrorCode.InsufficientPermissions =>
                ("Sem permissao para escrever", "O Windows nao autorizou a escrita no diretorio escolhido.", "Escolha um diretorio com permissao de escrita ou execute como administrador."),
            InstallerErrorCode.InvalidInstallDirectory =>
                ("Diretorio invalido", "O caminho escolhido nao pode ser usado para a instalacao.", "Escolha outro diretorio."),
            InstallerErrorCode.WebView2Missing =>
                ("WebView2 ausente", "O FHZIPA INJECT precisa do WebView2, que nao foi encontrado neste computador.", "Deixe a instalacao baixar o WebView2 ou instale-o pela pagina da Microsoft."),
            InstallerErrorCode.NetworkUnavailable =>
                ("Sem conexao com a internet", "O instalador nao conseguiu falar com a internet.", "Verifique a conexao e tente de novo."),
            InstallerErrorCode.ServerUnavailable =>
                ("Servidor indisponivel", "O servidor de downloads nao respondeu.", "Tente de novo em alguns minutos."),
            InstallerErrorCode.DownloadInterrupted =>
                ("Download interrompido", "A transferencia do arquivo foi interrompida.", "Tente de novo, a instalacao retoma de onde parou."),
            InstallerErrorCode.IntegrityCheckFailed =>
                ("Arquivo corrompido", "O arquivo baixado nao confere com o resumo esperado.", "O arquivo foi descartado, baixe de novo."),
            InstallerErrorCode.DownloadTimedOut =>
                ("Tempo esgotado", "O download demorou demais e foi interrompido.", "Tente de novo com uma conexao melhor."),
            InstallerErrorCode.TlsFailure =>
                ("Falha de seguranca na conexao", "A conexao segura com o servidor nao pode ser verificada.", "Verifique a data e a hora do computador e o certificado da rede."),
            InstallerErrorCode.ExtractionFailed =>
                ("Falha ao extrair os arquivos", "Os arquivos do pacote nao puderam ser extraidos.", "Verifique o espaco em disco e tente de novo."),
            InstallerErrorCode.FileLocked =>
                ("Arquivo em uso", "Um dos arquivos do aplicativo esta em uso.", "Feche o FHZIPA INJECT e qualquer gerenciador de arquivos e tente de novo."),
            InstallerErrorCode.CopyFailed =>
                ("Falha ao copiar os arquivos", "Nao foi possivel copiar o aplicativo para o destino.", "Verifique as permissoes do diretorio e tente de novo."),
            InstallerErrorCode.ShortcutCreationFailed =>
                ("Falha ao criar o atalho", "O atalho nao pode ser criado, mas a instalacao continua.", "Voce pode criar o atalho depois, manualmente."),
            InstallerErrorCode.DependencyMissing =>
                ("Dependencia ausente", "Um componente necessario nao foi encontrado.", "Rode o instalador com a internet ligada para baixar os componentes."),
            InstallerErrorCode.DependencyFailed =>
                ("Falha em um componente", "Um componente necessario nao pode ser instalado.", "Rode o instalador novamente ou instale o componente pela pagina do fabricante."),
            InstallerErrorCode.FinalizationFailed =>
                ("Falha ao finalizar", "A instalacao terminou sem a verificacao final.", "Rode o instalador novamente para reparar."),
            InstallerErrorCode.ApplicationLaunchFailed =>
                ("O aplicativo nao abriu", "O FHZIPA INJECT foi instalado, mas nao pode ser aberto agora.", "Tente abrir o aplicativo pelo menu Iniciar."),
            InstallerErrorCode.RollbackFailed =>
                ("Falha ao desfazer", "A instalacao nao pode ser desfeita por completo.", "Rode o instalador novamente para reparar a instalacao."),
            InstallerErrorCode.RecoveryRequired =>
                ("Instalacao precisa de reparo", "A instalacao anterior foi interrompida.", "Rode o instalador novamente para reparar."),
            InstallerErrorCode.CancelledByUser =>
                ("Instalacao cancelada", "Voce cancelou a instalacao.", "Nada foi alterado no seu computador."),
            _ => ("Erro desconhecido", "Ocorreu um erro inesperado.", "Tente de novo."),
        };

        return new InstallerError(
            code,
            title,
            detail is null ? description : $"{description} {detail}",
            action);
    }
}
