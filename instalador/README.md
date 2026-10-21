# FHZIPA INJECT — Instalador

Instalador oficial do **FHZIPA INJECT** para Windows 10 e 11 (64 bits).

O instalador é um aplicativo WPF (.NET 10) que faz a instalação de verdade:
copia o aplicativo, cria os atalhos, registra o desinstalador em
*Aplicativos instalados* e, se qualquer etapa falhar, desfaz o que já fez.

A interface reproduz a identidade da marca: preto profundo, vermelho
`#FF1744`, o quadrado da logo desenhado em vetor, curvas vermelhas no
fundo e uma janela compacta — **760 × 520** por padrão, um terço da área
útil do monitor.

---

## As nove telas

| # | Tela | O que mostra |
|---|---|---|
| 1 | Boas-vindas | A identidade, o aviso sobre a assinatura e os três destaques do instalador. |
| 2 | Diretório | A pasta, o espaço em disco, os atalhos e a instalação anterior. |
| 3 | Requisitos | As cinco verificações reais, uma por linha, com o resultado à direita. |
| 4 | Obtenção | A barra de progresso, o arquivo, o tamanho, a velocidade e o tempo restante. |
| 5 | Instalação | As operações que estão acontecendo, com o estado de cada uma. |
| 6 | Componentes | O aplicativo, os atalhos, o registro e o desinstalador. |
| 7 | Finalização | As últimas verificações antes de dar a instalação por concluída. |
| 8 | Conclusão | O selo verde, o título centralizado e os botões. |
| 9 | Abertura | A captura real do aplicativo e a decisão de abrir agora. |

## Requisitos

- Windows 10 1607 ou superior, 64 bits.
- 250 MB livres na unidade de instalação.
- WebView2 (o Windows 10/11 já traz; a instalação avisa quando falta).
- Permissão de administrador **apenas** quando a pasta escolhida fica dentro
  de `Program Files` — nesse caso o Windows pede a autorização na hora.

## Compilação

```powershell
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

O script restaura, compila, roda os testes, publica o instalador autocontido
de arquivo único, gera os checksums e publica na pasta de distribuição.

Detalhes: [BUILD.md](docs/BUILD.md).

## Estrutura

```
instalador fhzipa/
├── src/
│   ├── FHZIPA.Installer/        o instalador (WPF, .NET 10)
│   │   ├── Themes/               cores, tipografia, controles e fundos
│   │   ├── Controls/             a logo vetórica
│   │   ├── Models/               a máquina de estados, o progresso, os erros
│   │   ├── ViewModels/           o estado de cada tela
│   │   ├── Views/                as nove telas
│   │   ├── Services/             instalação, verificação, download, log
│   │   └── Utilities/            conversores e o espaçamento de letras
│   └── FHZIPA.Uninstaller/      o desinstalador (.NET Framework 4.8, 14 KB)
├── tests/
│   └── FHZIPA.Installer.Tests/  59 testes automatizados
├── scripts/
│   └── build.ps1                o build reproduzível
├── docs/                        a documentação
├── legacy-nsis/                 o instalador antigo, arquivado
└── FHZIPA-INJECT.slnx           a solução
```

## Documentação

| Documento | Assunto |
|---|---|
| [ARCHITECTURE.md](docs/ARCHITECTURE.md) | Como o instalador é organizado e por quê. |
| [BUILD.md](docs/BUILD.md) | Como compilar, publicar e assinar. |
| [INSTALLATION.md](docs/INSTALLATION.md) | O que acontece em cada tela. |
| [CONFIGURATION.md](docs/CONFIGURATION.md) | O arquivo de configuração. |
| [SECURITY.md](docs/SECURITY.md) | As garantias e os limites de segurança. |
| [TESTING.md](docs/TESTING.md) | Os testes e a matriz de cenários. |
| [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md) | Problemas conhecidos e o que fazer. |
| [RELEASE.md](docs/RELEASE.md) | Como gerar uma versão. |

## Estado atual

- 59 testes automatizados passando.
- Instalação, desinstalação, atalhos e registro verificados de ponta a ponta.
- As nove telas foram capturadas e conferidas contra a referência.
- O instalador **não** tem assinatura Authenticode: o SmartScreen vai
  avisar até que exista um certificado. Ver [SECURITY.md](docs/SECURITY.md).
- `win-arm64` está previsto no projeto, mas **não** foi testado neste
  computador (amd64).
