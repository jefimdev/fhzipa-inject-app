# Arquitetura

## Visão geral

O instalador é um aplicativo WPF em .NET 10 (`net10.0-windows`). Ele é
publicado como **arquivo único autocontido**: quem recebe o instalador não
precisa ter o runtime do .NET instalado.

```
FHZIPA_INJECT_Setup.exe
├── o próprio instalador (WPF, MVVM)
├── o runtime do .NET 10 (embutido)
└── o aplicativo FHZIPA INJECT (recurso embutido, 24 MB)
    └── com o resumo SHA-256 (recurso ao lado)
```

## Camadas

```
Views (XAML)          uma tela por arquivo, só apresentação
    │
ViewModels            estado, comandos, tradução das telas
    │
Models               a máquina de estados, o progresso, o catálogo de erros
    │
Services             instalação, verificação, download, log, elevação
    │
Infrastructure       configuração e caminhos
```

Nenhum ViewModel conhece o disco; nenhum serviço conhece a interface.

## A janela e o dimensionamento

A janela é compacta. No primeiro arranque, o instalador calcula um terço da
área útil do monitor e aplica o menor tamanho que ainda deixa o conteúdo
legível:

| Item | Valor |
|---|---|
| Área útil do monitor | 1920 × 1045 (1920 × 1080, barra de tarefas) |
| Tamanho solicitado (1/3) | 640 × 348 |
| Tamanho aplicado | **760 × 520** (mínimo funcional) |
| DPI | 96 (100%) |

O motivo da diferença é registrado no log a cada arranque. 640 × 348 não
cabe: a tela de componentes, com quatro linhas e os cartões, ficaria
cortada.

### Layout compacto e expandido

A proporção do painel lateral muda com a largura da janela (abaixo de 820 px
o painel estreita e as margens decorativas diminuem), mas **os tamanhos de
fonte não mudam**. O conteúdo que não cabe rola na vertical; o rodapé de
ações continua sempre visível.

## A máquina de estados

`Models/InstallerState.cs` define as 21 etapas e a tabela de transições
permitidas. `InstallerStateMachine.CanTransition` é a única porta de
entrada: uma transição fora da tabela é recusada e registrada no log.

Isso garante as regras que o produto precisa:

- não se instala antes de validar os arquivos;
- não se conclui antes da verificação final;
- falha não vira sucesso;
- duas instalações não acontecem ao mesmo tempo.

```
NotStarted → Welcome → DirectorySelection → CheckingRequirements
    ├→ RequirementsFailed → CheckingRequirements (nova tentativa)
    └→ ReadyToDownload → Downloading → ValidatingDownloads
         → ReadyToInstall → Installing → InstallingDependencies
         → Configuring → Finalizing → Completed
```

`Failed` leva a `RollbackInProgress` e a `RecoveryRequired`.

## O motor de instalação

`Services/InstallationService.cs` faz as operações de verdade:

1. grava o registro (`InstallationJournal`) **antes** de mexer no disco;
2. confere o SHA-256 do pacote;
3. copia o arquivo, guardando o anterior como `.fhzipa-antigo`;
4. cria os atalhos;
5. registra o desinstalador;
6. apaga o registro quando tudo deu certo.

A cópia reporta os bytes reais, então a barra de progresso do instalador
mostra um percentual verdadeiro.

Qualquer falha chama o rollback, que devolve os arquivos, remove os atalhos
e apaga as pastas criadas. O rollback é idempotente: rodar duas vezes não
faz mal.

Se o instalador for interrompido (queda de energia, fechar o processo), o
registro fica em `%LOCALAPPDATA%\FHZIPA INJECT\journal.json` e a próxima
execução encontra a instalação pela metade.

## A verificação de requisitos

`Services/RequirementChecker.cs` roda cinco verificações reais, todas
assíncronas:

| Verificação | Como é feita |
|---|---|
| Sistema operacional | `Environment.OSVersion` e `RuntimeInformation` |
| Espaço em disco | `DriveInfo.AvailableFreeSpace` |
| Permissões | escrita de teste na pasta escolhida |
| WebView2 | registro `EdgeUpdate\Clients\{F3017226-…}` |
| Rede | requisição HTTPS de verdade, com tempo limite |

Só a falha crítica impede o avanço. Avisos aparecem explicados, com o
ícone âmbar.

## A obterção dos arquivos

Hoje o aplicativo viaja **embutido** no instalador, com o resumo SHA-256 ao
lado. O `DownloadService` existe para o caso de manifesto remoto:

- só HTTPS, com a validação do certificado ligada;
- retomada por `Range`, conferindo `Content-Range` e o tamanho esperado;
- tentativas limitadas com espera exponencial e desvio;
- SHA-256 conferido antes de qualquer uso;
- arquivo parcial apagado quando a transferência falha.

## A interface

### O sistema visual

| Arquivo | O que tem |
|---|---|
| `Themes/Colors.xaml` | a paleta, os pincéis e os gradientes |
| `Themes/Typography.xaml` | tipografia, espaçamento e arredondamento |
| `Themes/Controls.xaml` | botões, cartões, campos, barras de progresso |
| `Themes/Backgrounds.xaml` | as curvas do fundo e os brilhos |

A ordem dos dicionários importa: `Backgrounds.xaml` vem antes de
`Controls.xaml`, porque os botões usam os brilhos.

### As formas do fundo

O fundo tem seis camadas, todas vetoriais:

1. preto profundo (`#080B12`);
2. brilho radial escuro;
3. curva vermelha superior direita;
4. curva vermelha inferior esquerda, com o contorno luminoso;
5. iluminação suave no canto inferior esquerdo;
6. vinheta escura, para o texto continuar legível.

Nenhuma imagem esticada é usada como fundo.

### A navegação

`Views/ViewLocator.cs` resolve a tela de cada ViewModel e **guarda a
instância**. A tela é criada uma vez e reaproveitada: recriar a árvore
visual a cada mudança de etapa provoca piscadas e salto de layout.

## Estabilidade visual

| Sintoma | Causa | Correção |
|---|---|---|
| Piscar na troca de tela | a tela era recriada a cada etapa | o `ViewLocator` guarda a instância |
| Barra de progresso pulando | o progresso atualizava a cada bloco de bytes | o `ReportProgress` limita a 10 atualizações por segundo |
| Texto do campo vazio | o `ScrollViewer` interno media sem altura | o template segue a estrutura padrão do WPF |
| Botão sem estilo | os recursos de brilho eram resolvidos depois | a ordem dos dicionários foi corrigida |
| Layout saltando | a largura do painel era recalculada a cada quadro | a proporção é ajustada só na mudança de tamanho |

Os efeitos (`DropShadowEffect`) são discretos e não são animados
continuamente. Os recursos do `ResourceDictionary` são compartilhados e o
WPF os congela.

## Acessibilidade

- Ordem de tabulação natural, foco visível (anel vermelho no botão
  principal, borda vermelha no campo).
- `AutomationProperties.Name` em todos os botões e campos.
- `Enter` executa a ação principal, `Esc` cancela, `Alt+F4` fecha.
- Nenhum estado é comunicado só por cor: sempre há texto e um ícone.

## Log

`%LOCALAPPDATA%\FHZIPA INJECT\Logs\instalador.log`, com rotação em 2 MB e
até 5 arquivos. O formato é o do `Microsoft.Extensions.Logging`.

Nunca entram no log: senhas, tokens, cookies, credenciais da conta Apple ou
chaves privadas. O instalador não pede credencial nenhuma.

## Decisões que valem registro

| Decisão | Motivo |
|---|---|
| WPF em vez de NSIS | interface vetorial, DPI, acessibilidade e testabilidade. |
| .NET 10 e não .NET 8 | só o *targeting pack* 10.0 está instalado nesta máquina. |
| Payload embutido | o instalador funciona sem internet e sem servidor. |
| Desinstalador em .NET Framework 4.8 | 14 KB e presente em todo Windows 10/11. |
| Elevação só quando precisa | o UAC só aparece na pasta de programas. |
| Sem `trimming` | trimming quebra WPF (reflexão e *bindings*). |
| Sem `ScaleTransform` | a escala global deixa o texto borrado; o layout responde pela largura. |
