# FHZIPA INJECT

Assistente de sideloading para iPhone/iPad no Windows, com tema vermelho e preto.

O FHZIPA INJECT é um **fork modificado de um projeto open-source distribuído sob a licença MIT**. Esta versão não é oficial, não é afiliada a nenhum dos autores originais e não possui endosso deles. Consulte [NOTICE.md](NOTICE.md) para os créditos completos e [LICENSE](LICENSE) para os termos.

## Recursos reais

- Login real com Apple ID, incluindo verificação em dois fatores (2FA).
- Detecção de iPhone/iPad por USB via usbmuxd/Apple Mobile Device Support.
- Pareamento e gerenciamento de arquivos de pareamento.
- Instalação de qualquer arquivo `.ipa` assinado com o seu próprio certificado.
- Loja de IPAs integrada, com busca, categorias e download direto.
- Gerenciamento de certificados de desenvolvimento e App IDs.
- Armazenamento seguro de credenciais no Windows Credential Manager (keyring).
- Servidor Anisette configurável.
- Logs de diagnóstico e sugestões para erros comuns.
- Interface em português do Brasil, com detecção automática do idioma do sistema.

## A loja de apps

A aba **Baixar apps** lê o catálogo direto das fontes, sem nada embutido no
aplicativo — por isso a listagem nunca fica desatualizada. As fontes atuais são:

| Fonte | Formato | Apps | `.ipa` direto |
|---|---|---|---|
| IPAOMTK | API REST do WordPress | 11.633 | sim |
| iAppStores | API JSON própria | 5.669 | sim |
| GitHub | releases de repositórios | 1.306 | sim |
| iPA Library | API REST do WordPress | 834 | não |
| IPAJB | API REST do WordPress | 44 | não |

As três primeiras entregam o arquivo pronto. Os dois catálogos do WordPress
listam o app com todos os metadados (ícone, versão, tamanho, descrição) e
abrem a página do app, porque o link do arquivo é montado por script no site.

O botão **Baixar** traz o `.ipa` para a pasta temporária do sistema, mostra o
progresso e libera a instalação usando o mesmo fluxo de assinatura do restante
do aplicativo. Dá para instalar direto do catálogo, ou pelo painel
**Importar IPA** da tela inicial.

### Velocidade do download

O mesmo app costuma estar em mais de um lugar, e cada fornecedor tem um limite
de velocidade diferente. Antes de começar, o aplicativo mede a velocidade de
cada host e usa o mais rápido:

- o mesmo app é procurado nas outras fontes, e o espelho só é aceito quando o
  **bundle identifier** é o mesmo — o arquivo é do app certo;
- quando o espelho serve outro build, a versão vai junto na tela, para não
  haver surpresa;
- o arquivo é baixado em **6 conexões paralelas** com HTTP Range, cada uma
  escrevendo a sua parte, e as partes são juntadas no fim;
- o resultado é medido e o tamanho real do arquivo é mostrado, que costuma
  diferir do informado pela fonte;
- quando a fonte publica o **digest sha256** do arquivo, o download é
  conferido contra ele — um espelho que sirva outro arquivo é descartado.

A fonte do GitHub é lida pelo feed e pelas páginas de cada release, e não pela
API pública: aquela aceita poucas requisições por hora e responde com um
erro assim que a cota acaba. As respostas são guardadas por 10 minutos.

> O conteúdo dos catálogos é definido por cada fonte. Os apps modificados
> quebram os Termos de Serviço da App Store e a instalação pode ser revogada
> pela Apple a qualquer momento.

## Requisitos

- Windows 10 ou 11, 64 bits.
- [iTunes](https://apple.co/ms) ou Apple Devices instalado, para fornecer o serviço **Apple Mobile Device Support**.
- Cabo USB e um iPhone/iPad desbloqueado e confiável neste computador.
- Conta Apple ID válida. O código de 2FA será solicitado pela Apple.
- Conexão com a internet para autenticação, Anisette e comunicação com os serviços da Apple.
- Em iOS 16 ou superior, pode ser necessário ativar **Ajustes > Privacidade e Segurança > Modo de Desenvolvedor** e reiniciar o aparelho.

### Limites de uma conta Apple ID gratuita

- Os aplicativos assinados expiram em **7 dias** e precisam ser reinstalados.
- Normalmente é possível manter até **3 aplicativos** assinados por dispositivo.
- A Apple limita a criação de App IDs (cerca de 10 por semana).
- Uma conta paga do Apple Developer Program remove o limite de 7 dias.

## Como usar

1. Instale o iTunes/Apple Devices e conecte o iPhone ao computador.
2. Desbloqueie o iPhone e toque em **Confiar**.
3. Abra o FHZIPA INJECT.
4. Clique em **Atualizar dispositivos** e selecione o aparelho.
5. Entre com seu Apple ID e conclua o 2FA.
6. Vá na aba **Baixar apps**, escolha a fonte e clique em **Baixar** no app
   desejado — ou use o painel **Importar IPA** da tela inicial para um
   arquivo que você já tem.
7. Acompanhe o progresso e os logs no aplicativo.

O pareamento fica salvo e pode ser reenviado para os aplicativos compatíveis instalados no aparelho pela tela **Gerenciar Arquivo de Pareamento**. Também é possível importar um arquivo de pareamento existente nessa mesma tela.

## Dados e logs

- Configurações e logs: `%APPDATA%\app.fhzipa.inject\logs`
- Credenciais: Windows Credential Manager.
- O estado do Anisette e os certificados podem ser armazenados no keyring do sistema. A opção de desativar o keyring existe apenas para diagnóstico e reduz a segurança.

## Privacidade e segurança

- As credenciais são enviadas diretamente aos serviços da Apple durante a autenticação. O aplicativo não mantém um servidor próprio de coleta de credenciais.
- O servidor Anisette padrão é `ani2.stikstore.app`. Você pode trocar o servidor nas configurações; use apenas servidores confiáveis.
- Execute o aplicativo somente no seu próprio computador e com a sua própria conta Apple.
- Não compartilhe seu Apple ID, senha, código de 2FA ou arquivos de pareamento.

## Aviso legal

Use o FHZIPA INJECT apenas com a sua própria conta Apple e com aplicativos que você tem o direito de instalar. O sideloading pode violar os Termos de Serviço da Apple e pode fazer com que sua conta seja limitada. Não há garantia de funcionamento, de disponibilidade dos serviços da Apple ou de preservação dos dados. Você é responsável pelo uso.

## Compilar do código-fonte

Pré-requisitos:

- Node.js 20 ou superior
- Rust estável (toolchain `x86_64-pc-windows-msvc`)
- Visual Studio Build Tools com a carga de trabalho **Desenvolvimento para Desktop com C++**
- WebView2 Runtime

```powershell
npm install
npm run build
npm run tauri build
```

O instalador NSIS será gerado em `src-tauri/target/release/bundle/nsis/`.

Para desenvolvimento com recarga automática:

```powershell
npm run tauri dev
```

A instalação reprodutível, que usa exatamente o `package-lock.json`, é
`npm ci`.

### O instalador com a identidade da marca

Além do instalador NSIS, existe um instalador próprio, escrito em WPF
(.NET 10), que tem a identidade visual da marca e faz a instalação de
verdade. O código dele está na pasta [`instalador/`](instalador/) deste
repositório:

```powershell
cd instalador
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

O script copia o executável gerado por `npm run tauri build` para dentro do
instalador, confere o resumo SHA-256 e gera
`instalador/out/publish/FHZIPA_INJECT_Setup.exe`. Para ver todas as telas
sem instalar nada:

```powershell
instalador\out\publish\FHZIPA_INJECT_Setup.exe --preview
```

- Manual do instalador: [`instalador/GUIA-DO-INSTALADOR.txt`](instalador/GUIA-DO-INSTALADOR.txt)
- Arquitetura e decisões técnicas: [`instalador/DOCUMENTACAO-TECNICA-DO-INSTALADOR.md`](instalador/DOCUMENTACAO-TECNICA-DO-INSTALADOR.md)
- O que foi verificado e o que não foi: [`instalador/RELATORIO-DE-VALIDACAO-DO-INSTALADOR.txt`](instalador/RELATORIO-DE-VALIDACAO-DO-INSTALADOR.txt)

### Onde está a documentação do projeto

- Manual de edição, com o mapa de cada tela e o que mexer em cada
  arquivo: [`GUIA-DE-EDICAO-DO-APP.txt`](GUIA-DE-EDICAO-DO-APP.txt)
- Relatório da organização interna dos arquivos e das verificações:
  [`RELATORIO-DE-ORGANIZACAO.txt`](RELATORIO-DE-ORGANIZACAO.txt)

### Tecnologias

| Parte | Tecnologia |
|---|---|
| Interface | React 19, TypeScript, Vite 8, i18next, react-icons, sonner |
| Aplicativo nativo | Tauri 2, Rust (`x86_64-pc-windows-msvc`) |
| Plugins Tauri | `dialog`, `opener`, `store` |
| Back end Apple | crates `idevice` e `isideload` |
| Instalador próprio | WPF, .NET 10, `Microsoft.Extensions.*` |

### Configurar os links do projeto

Todos os links externos (repositório, issues, doações, Discord, traduções) ficam centralizados em [`src/configuracoes/links.ts`](src/configuracoes/links.ts). Para apontá-los para outro repositório, crie um arquivo `.env` na raiz:

```dotenv
VITE_REPO_URL=https://github.com/<owner>/<repo>
VITE_ISSUES_URL=https://github.com/<owner>/<repo>/issues
VITE_SPONSOR_URL=https://github.com/sponsors/<owner>
VITE_DISCORD_URL=https://discord.gg/<invite>
VITE_TRANSLATE_URL=https://github.com/<owner>/<repo>#translations
```

### Servidores Anisette

Os servidores pré-configurados ficam em [`src/configuracoes/servidores-anisette.ts`](src/configuracoes/servidores-anisette.ts). O cliente e o servidor Anisette precisam pertencer à mesma família de plataforma, por isso o padrão desta build para Windows é um servidor Windows. Uma preferência salva que aponte para um servidor fora da lista é migrada para o padrão na inicialização.

## Limitações conhecidas

- O instalador com a identidade da marca é novo: a instalação de verdade
  foi validada em um diretório isolado, com o aplicativo abrindo e a
  desinstalação removendo arquivos, atalho e registro. A instalação
  elevada no caminho padrão (`Program Files`) **não** foi testada de ponta
  a ponta, e a desinstalação com dados do usuário presentes também não.
- O caminho de atualização automática do Tauri (`latest.json`) continua
  apontando para o instalador NSIS. Se a distribuição passar a usar só o
  instalador próprio, esse caminho precisa ser revisto.
- O instalador próprio não é assinado digitalmente. A assinatura continua
  sendo feita no GitHub Actions, com o secret `TAURI_SIGNING_PRIVATE_KEY`.
- O catálogo da loja depende de servidores de terceiros e do tempo de
  resposta deles; a listagem pode ficar vazia ou atrasada.
- O sideloading depende de conta Apple válida, do serviço Apple Mobile
  Device Support instalado e do iPhone desbloqueado.

## Monitor de downloads

O repositório inclui o workflow `.github/workflows/download-notifier.yml`, que
consulta os contadores de download das releases publicadas e avisa quando
alguém baixa o instalador. Ele roda de hora em hora e a cada release
publicada, e usa o arquivo `download-state.json` para guardar os contadores
anteriores.

Os avisos saem por Discord, Telegram ou ntfy, conforme os secrets
configurados no repositório. O script que faz a consulta está em
`.github/scripts/check-downloads.mjs`.

Um aplicativo separado, o **FHZIPA MONITOR**, acompanha os mesmos
números em tempo real e mostra o histórico, mas é pessoal e não faz parte
desta distribuição.

## Licença e atribuição

O código-fonte original é Copyright (C) 2026 nab138 e está licenciado sob a **MIT License**. Consulte [LICENSE](LICENSE) e [NOTICE.md](NOTICE.md).

O FHZIPA INJECT mantém o código sob a MIT License e substitui a identidade visual original.

## Componentes de terceiros

- [idevice](https://github.com/jkcoxson/idevice) — comunicação com dispositivos iOS.
- [isideload](https://github.com/nab138/isideload) — autenticação, assinatura e instalação.
- [Tauri](https://tauri.app) — shell desktop.
- [React](https://react.dev) e [Vite](https://vite.dev) — interface.
