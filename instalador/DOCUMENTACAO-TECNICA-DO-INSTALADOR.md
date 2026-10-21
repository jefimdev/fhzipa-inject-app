# Documentação técnica do instalador — FHZIPA INJECT

Instalador próprio do FHZIPA INJECT: aplicação WPF (.NET 10) com interface
própria sobre um motor de instalação real, empacotado em um único `.exe`
autocontido.

Este documento explica como as peças conversam. O manual de uso está em
`GUIA-DO-INSTALADOR.txt`; o resultado das verificações está em
`RELATORIO-DE-VALIDACAO-DO-INSTALADOR.txt`.

---

## 1. Decisão de arquitetura

**Alternativa escolhida: interface WPF própria + motor de instalação
próprio, dentro do mesmo executável.**

### O que foi avaliado

| Alternativa | Por que foi descartada |
|---|---|
| Personalizar o NSIS do Tauri (`installerHooks`) | O NSIS/MUI não consegue reproduzir o desenho de referência (painel lateral com etapas, cartões com borda e gradiente, barra de progresso no tom da marca). Dá para mudar cores e textos, não a composição. |
| Bootstrapper que abre a interface e roda o instalador NSIS em silêncio | O instalador NSIS em modo silencioso não reporta progresso. A interface ficaria sem medida real — exatamente o que a tarefa proíbe. |
| WiX Burn / Inno Setup | Traz um framework de scripting e um motor de canvas próprios; a identidade visual exigiria skins de terceiros, e não há ganho sobre escrever a interface em WPF, que já está no mesmo ecossistema do projeto. |
| **WPF próprio com motor de instalação próprio** | **Escolhida.** A interface é livre (HTML não entra na equação), o motor faz o trabalho de verdade e o resultado é um `.exe` autocontido, sem dependência do .NET na máquina do usuário final. |

### Por que WPF e não uma interface web

A interface web exigiria WebView2 na máquina do usuário. O WebView2 é
dependência do **aplicativo** (ele precisa para rodar), mas fazer do
instalador também depender disso cria dois problemas: a máquina sem WebView2
não consegue nem *instalar* o que precisa do WebView2, e o instalador
passaria a depender de um download da Microsoft durante a instalação. Com
WPF, o instalador usa a .NET Desktop Runtime presente no Windows 10/11.

### Por que o motor de instalação é próprio

A tarefa pedia "instalação verdadeira" com operações e progresso reais.
O motor (`Services/InstallationService.cs`) faz, nesta ordem:

1. grava um *journal* do que será feito (`InstallationJournal`);
2. confere o **SHA-256 do payload** antes de qualquer escrita;
3. cria a pasta de destino;
4. guarda o executável anterior como `.fhzipa-antigo`;
5. copia o executável, reporting bytes reais;
6. cria os atalhos (COM `WScript.Shell`);
7. escreve a entrada de desinstalação no registro do usuário (HKCU);
8. escreve o desinstalador companion;
9. apaga o journal.

Se qualquer passo falhar, o *rollback* desfaz exatamente o que o journal
registrou. O que **não** é reaproveitado de outro mecanismo: o empacotador
NSIS do Tauri e o registro que ele escrevia. O formato da entrada de
registro foi mantido o mesmo (`HKCU\...\Uninstall\FHZIPA INJECT`), de modo
que "Aplicativos instalados" continua funcionando e a detecção de
instalação anterior também.

---

## 2. Mapa do código

```
instalador fhzipa/
  FHZIPA-INJECT.slnx
  scripts/build.ps1                 geração da entrega
  src/FHZIPA.Installer/
    App.xaml(.cs)                  DI, argumentos da linha de comando,
                                   modo de limpeza, desinstalação
    installer.config.json          configuração do instalador
    app.manifest                   asInvoker, DPI por monitor
    Views/                         uma tela por arquivo + ViewLocator
    ViewModels/                    um ViewModel por tela, MVVM à mão
    Models/                        InstallerState, InstallerProgress,
                                   InstallerError, InstallationProgressTracker
    Services/                      motor de instalação, requisitos,
                                   payload embutido, elevação, log
    Security/                      PathValidator, SingleInstanceGuard
    Installation/InstallationJournal.cs
    Localization/                  Strings.resx (pt-BR) e Strings.en.resx
    Themes/                        cores, tipografia, controles, fundos
    Resources/                     payload + resumo, embutidos
  src/FHZIPA.Uninstaller/           companion .NET Framework 4.8
  tests/FHZIPA.Installer.Tests/     72 testes
```

Não há biblioteca de interface de terceiros. As únicas dependências NuGet
são `Microsoft.Extensions.DependencyInjection` e
`Microsoft.Extensions.Logging` (usadas para o log em arquivo). O MVVM, a
navegação, a máquina de estados e os conversores são escritos à mão.

---

## 3. Interface com o mecanismo de instalação

A interface e o motor **não se conhecem diretamente**. O contrato é o
evento `InstallerProgress` (`Models/InstallerProgress.cs`):

| Campo | Significado |
|---|---|
| `OperationName` | qual operação está em curso |
| `CurrentStage` | a etapa real ("Baixando", "Instalando", "Criando atalhos", …) |
| `StageStatus` | o estado da etapa |
| `BytesProcessed` / `TotalBytes` | progresso medido; total desconhecido ⇒ indeterminado |
| `BytesPerSecond` | velocidade medida (`TransferSpeedEstimator`, janela de 4 s) |
| `EstimatedTimeRemaining` | tempo restante, só quando a velocidade é confiável |
| `IsIndeterminate` | a operação não tem medida de bytes |
| `CurrentMessage` | o texto que a pessoa lê |
| `ErrorCode` / `ErrorMessage` | quando a operação falha |

O motor emite; o `MainViewModel` traduz para a tela. Três regras:

1. **A interface nunca deduz sucesso.** Quem confirma é o código de saída do
   motor e as três verificações finais (`FinalizingViewModel.RunChecks`).
   Se uma verificação falha, a instalação **não** é declarada concluída: a
   tela de erro aparece com o que ficou pendente.
2. **Eventos atrasados não contaminam uma nova tentativa.** O
   `InstallationProgressTracker` é zerado (`Reset`) no começo de cada
   execução, e a máquina de estados recusa transições inválidas, então um
   evento antigo não muda a etapa corrente.
3. **O progresso global sai de trabalho real.** O
   `InstallationProgressTracker` converte bytes extraídos, bytes copiados,
   operações de registro e verificações finais em uma porcentagem, com
   pesos documentados (30/45/15/10). Não existe temporizador que empurre a
   barra.

### Modo de prévia

`--preview` troca o motor real por uma simulação com os mesmos eventos. A
interface recebe uma faixa amarela permanente e o título da janela muda. O
modo não copia arquivo, não cria atalho, não escreve no registro e não
pede elevação.

---

## 4. Máquina de estados

`Models/InstallerState.cs`.

```
NotStarted -> Welcome -> DirectorySelection -> CheckingRequirements
  -> RequirementsFailed -> CheckingRequirements      (reverificar)
  -> ReadyToDownload -> Downloading -> DownloadPaused/DownloadFailed
  -> ValidatingDownloads -> ReadyToInstall
  -> Installing -> InstallingDependencies -> Configuring -> Finalizing
  -> Completed                       (estado final, nao sai de lugar)
```

Regras:

- Uma transição fora da tabela é **recusada** e registrada no log.
- `Completed` não tem saída: a instalação concluída não volta a Working.
- `Failed` e `Cancelled` podem voltar para `DirectorySelection` ou
  `NotStarted` — é o que permite **Tentar de novo** funcionar de verdade.
- `Installing` e `Configuring` não pulam para `Completed`: a verificação
  final é obrigatória.
- `IsBusy` marca as etapas em que não há cancelamento seguro.
- `CanRetry` diz se a etapa corrente admite repetição.

O teste `StateMachineTests` cobre as transições recusadas, o caminho
completo e a cancelabilidade; `RetryStateTests` cobre a repetição.

---

## 5. Privilégios

- O instalador é `asInvoker` (`app.manifest`). Ele **não** pede
  administrador na abertura.
- A elevação é pedida no momento exato em que é necessária: ao avançar
  com um destino dentro de `Program Files` (ou `Windows`). Aí o instalador
  mostra um diálogo de confirmação e, se aceito, reabre a si mesmo com
  `Verb = "runas"` e os mesmos argumentos (`--install-dir`,
  `--desktop-shortcut`, `--start-menu-shortcut`).
- Se o Windows **recusar** a elevação, o instalador mostra a tela de erro
  com o motivo e **não** continua a instalação sem permissão.
- A tela de requisitos trata uma pasta protegida como **aviso**, não como
  falha: o Windows vai pedir a autorização.
- O modo de prévia **nunca** pede elevação.
- A instalação é **por usuário** (HKCU, atalhos em `%APPDATA%` e na área de
  trabalho). O caminho padrão fica em `Program Files`, o que é uma escolha
  do produto: para ficar por usuário sem privilégio, o padrão teria de ser
  `%LOCALAPPDATA%`. A opção de mudar o caminho está na própria tela.

---

## 6. Segurança

- **Caminhos** (`Security/PathValidator.cs`): recusa caminho vazio,
  relativo, unidade inexistente, caractere inválido, nome reservado e
  caminho com mais de 260 caracteres; recusa junção e link simbólico; e
  garante que o caminho filho fique dentro do diretório gerenciado.
- **Extração**: o payload vem de um recurso embutido; o destino é criado
  pelo próprio instalador, com um nome gerado por GUID, dentro de
  `%TEMP%\FHZIPA INJECT\`. Nada é extraído de um ZIP externo, então não
  há caminho de travessia.
- **Integridade**: o SHA-256 do payload vem de um recurso ao lado e é
  conferido **antes de qualquer escrita**. O resumo é regenerado no build
  pelo `scripts/build.ps1`, a partir do executável recém-compilado.
- **Sem shell**: não há comando montado com caminho escolhido pelo
  usuário. A desinstalação agenda a remoção com um executável próprio
  (copia do desinstalador na pasta temporária), que espera o processo
  terminar e apaga a pasta com a API do .NET.
- **Instância única** (`Security/SingleInstanceGuard.cs`): um mutex impede
  dois instaladores ao mesmo tempo; o segundo mostra um aviso e sai.
- **Registro**: só HKCU. Nada é escrito em HKLM.
- **Sem elevation silenciosa**: nenhuma política de UAC é alterada, o
  antivírus e o SmartScreen não são desativados, e nenhum certificado é
  criado para simular assinatura.

---

## 7. Cancelamento e recuperação

- Durante uma operação, `Cancel` cancela o token de cancelamento. A
  operação para no próximo bloco e a instalação vai para `Cancelled`.
- O que já foi feito é desfeito: o *journal* sabe o que criou.
- Se a operação não puder ser interrompida com segurança (a cópia do
  executável, por exemplo), a interface diz que a etapa está em andamento
  e o cancelamento espera o fim do bloco atual.
- Os temporários da execução atual são apagados ao final. Nada de outra
  execução é tocado.
- **Não há promessa de rollback completo**: se o rollback falhar, a
  situação é `RecoveryRequired` e a tela de erro explica, com o caminho
  do log.
- Uma instalação interrompida deixa um journal em
  `%LOCALAPPDATA%\FHZIPA INJECT\journal.json`.

---

## 8. Atualização

O instalador **não** é o caminho de atualização automática: ele instala a
versão que ele carrega, por cima da anterior, preservando os dados do
usuário (que ficam em `%APPDATA%\app.fhzipa.inject` e no chaveiro do
Windows, intocados pelo instalador).

- A detecção de instalação anterior é feita pelo registro
  (`InstallLocation`) e pela existencia de executavel na pasta.
- A instalacao por cima preserva o executavel anterior como
  `.fhzipa-antigo`, que volta ao lugar se algo der errado.
- Se a versao instalada for mais nova, o instalador nao faz
  rebaixamento automatico: ele substitui pelo executavel que ele carrega e
  isso fica registrado no log.
- **Ponto em aberto**: o atualizador automatico do Tauri (o `latest.json`
  publicado pelo GitHub Actions) aponta para o instalador NSIS. Se a
  distribuicao passar a usar somente o instalador novo, esse caminho tem de
  ser revisto. As chaves de assinatura e a identidade do atualizador
  (`app.fhzipa.inject`, `TAURI_SIGNING_PRIVATE_KEY`) nao foram tocadas.

---

## 9. Desinstalação

A entrada de desinstalação fica em
`HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\FHZIPA INJECT`:

| Valor | Conteúdo |
|---|---|
| DisplayName | FHZIPA INJECT |
| DisplayVersion | a versão da configuração |
| Publisher | FHZIPA INJECT |
| InstallLocation | a pasta de instalação |
| UninstallString / QuietUninstallString | `"<pasta>\desinstalar.exe" --uninstall` |
| DisplayIcon | o executável instalado |
| InstallDate | a data da instalação |
| NoModify / NoRepair | 1 |

O **desinstalador companion** (`src/FHZIPA.Uninstaller`, .NET Framework 4.8,
15,5 KB) pergunta antes, remove os dois atalhos, remove a entrada do
registro e agenda a remoção da pasta: ele copia a si mesmo para
`%TEMP%\FHZIPA INJECT\uninstall\`, e essa cópia espera o processo atual
terminar e apaga a pasta. Nenhum comando de shell é usado.

**Os dados do usuário são preservados**: `%APPDATA%\app.fhzipa.inject` (logs
e preferências) e as credenciais no chaveiro do Windows não são tocados.
Removê-los é uma decisão separada e não existe nesta versão.

O instalador também sabe fazer o papel de desinstalador
(`--uninstall`), usado como reserva se o companion não estiver ao lado.

---

## 10. Dependências e licenças

**Do projeto do instalador**

| Dependência | Licença | Para que serve |
|---|---|---|
| .NET 10 (WindowsDesktop) | MIT | compilação do instalador |
| .NET Framework 4.8 | — | compilação do desinstalador companion (já presente no Windows) |
| Microsoft.Extensions.DependencyInjection 10.0.0 | MIT | montagem dos serviços |
| Microsoft.Extensions.Logging 10.0.0 | MIT | log em arquivo com rotação |
| xunit 2.9.2, Microsoft.NET.Test.Sdk 17.12.0 | Apache-2.0 / MIT | testes |

**Do aplicativo** (o instalador só o transporta): Rust, Tauri 2, React 19,
Vite, i18next, react-icons, sonner, react-virtuoso, crates `idevice` e
`isideload`. As licenças de terceiros do aplicativo estão no `NOTICE.md`
do projeto `fhzipa-inject-app`.

**No computador do usuário final**: nada. O instalador é um `.exe`
autocontido (68 MB), sem .NET, sem Node, sem Rust, sem Visual Studio.

---

## 11. Métricas de qualidade

- 72 testes automatizados, todos passando.
- `TreatWarningsAsErrors` e `AnalysisLevel latest` ligados no `.csproj`.
- 0 avisos e 0 erros na compilação em Release.
- O `scripts/build.ps1` falha se o executável do aplicativo não existir.
- As duas telas de idioma têm o mesmo conjunto de chaves (verificado por
  teste).
