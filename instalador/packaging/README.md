# Empacotamento

## A arquitetura escolhida

**A — Bootstrapper WPF com motor de instalação próprio.**

O `FHZIPA_INJECT_Setup.exe` é o instalador. Ele:

- valida o sistema e o diretório;
- confere a integridade do aplicativo;
- copia os arquivos, cria os atalhos e registra o desinstalador;
- desfaz tudo se algo falhar;
- é o desinstalador de referência e monta o desinstalador de 14 KB que
  fica na pasta de instalação.

Ele é publicado como **arquivo único autocontido**, o que dispensa WiX,
Inno Setup ou qualquer outro empacotador externo.

## Por que não WiX Burn nem Inno Setup

| Alternativa | Motivo da não escolha |
|---|---|
| **WiX Burn** | exigiria a cadeia de ferramentas do WiX, um projeto de bootstrapper em C++/C#, e a identidade visual da tela ficaria limitada ao que o Burn permite. A interface pedida (painel de etapas, cartões, indicador por etapa) não cabe no bootstrapper padrão. |
| **Inno Setup** | continua sendo a melhor escolha para quem quer um instalador de arquivo único com script. Mas a interface seria limitada ao que o Inno desenha, e a verificação de integridade, o rollback e a máquina de estados ficariam no script, sem testes automatizados. |
| **NSIS (o projeto antigo)** | já usado antes neste projeto. A interface era gerada em bitmap, sem acessibilidade, e a verificação de integridade não existia. Está arquivado em `legacy-nsis/`. |

A regra do projeto é: **um mecanismo só**. O instalador em WPF é o
oficial; o NSIS está arquivado e não é mais mantido.

## O que o empacotador teria que fazer, e quem faz

| Operação | Responsável |
|---|---|
| Copiar os arquivos | `InstallationService` |
| Criar atalhos | `InstallationService` |
| Registrar o desinstalador | `InstallationService` (em `HKCU`) |
| Elevação | `ElevationService` (UAC só quando necessário) |
| Rollback | `InstallationService` + `InstallationJournal` |
| Desinstalação | `desinstalar.exe` (.NET Framework 4.8) |

## Onde ficam as coisas

| Pasta | Conteúdo |
|---|---|
| `out\publish\` | o instalador e o desinstalador, prontos para publicar. |
| `..\FHZIPA INJECT\` | a pasta de distribuição, com o instalador, o portátil e o LEIA-ME. |
| `%LOCALAPPDATA%\FHZIPA INJECT\` | logs e o registro da instalação, no computador do usuário. |

## Se um dia for preciso um empacotador externo

O ponto de entrada é o instalador em si: ele aceita argumentos e faz a
instalação de verdade. Um Inno Setup ou um WiX Burn poderia apenas chamá-lo:

```
FHZIPA_INJECT_Setup.exe --install-dir="C:\Apps\FHZIPA" --desktop-shortcut=1 --start-menu-shortcut=1
```

Nesse caso, o empacotador externo cuida só do pré-requisito (por exemplo,
instalar o WebView2), e a instalação continua sendo feita pelo instalador
oficial, com o rollback dele.
