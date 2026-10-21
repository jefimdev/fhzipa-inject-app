# Build

## Requisitos

| Ferramenta | Versão | Para quê |
|---|---|---|
| .NET SDK | 10.0 (LTS) | compilar o instalador e rodar os testes |
| .NET Framework 4.8 | presente no Windows | compilar o desinstalador |
| Node.js | 20+ | só para o instalador antigo em `legacy-nsis` |

O SDK precisa estar no `PATH`:

```powershell
dotnet --list-sdks
```

## Build completo

```powershell
cd "C:\Users\fheiz\Downloads\projetos codes\instalador fhzipa"
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

O script faz, nesta ordem, e **para no primeiro erro**:

1. confere o SDK;
2. copia o aplicativo do projeto do app para `src/FHZIPA.Installer/Resources/`
   e regera o `payload.sha256`;
3. restaura e compila em `Release`;
4. roda os 53 testes;
5. publica o instalador autocontido de arquivo único;
6. compila o desinstalador em .NET Framework;
7. confere os artefatos e gera o `SHA256SUMS.txt`;
8. publica na pasta de distribuição `FHZIPA INJECT`.

### Opções

| Opção | Efeito |
|---|---|
| `-Configuration Debug` | compila sem otimização, para depurar. |
| `-SkipTests` | pula os testes (verificação rápida de compilação). |
| `-SkipPublish` | não mexe na pasta de distribuição. |

## Build de desenvolvimento

```powershell
dotnet build FHZIPA-INJECT.slnx -c Debug
dotnet run --project src\FHZIPA.Installer\FHZIPA.Installer.csproj
```

Para depurar as associations de dados, a compilação `Debug` liga o
`PresentationTraceSources` e grava os problemas em
`%LOCALAPPDATA%\FHZIPA INJECT\Logs\bindings.log`.

## Publicação

O comando de publicação, se você quiser rodar na mão:

```powershell
dotnet publish src\FHZIPA.Installer\FHZIPA.Installer.csproj `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o out\publish
```

### Por que estes parâmetros

| Parâmetro | Motivo |
|---|---|
| `--self-contained true` | quem instala não precisa ter o runtime. |
| `PublishSingleFile` | um arquivo só para distribuir. |
| `IncludeNativeLibrariesForSelfExtract` | as bibliotecas nativas do WPF vão junto. |
| `EnableCompressionInSingleFile` | derruba o tamanho de 93 MB para 67 MB. |
| **sem** `PublishTrimmed` | *trimming* quebra WPF: reflexão e *bindings* quebram em tempo de execução. |

O instalador fica com cerca de **67 MB** (runtime do .NET embutido) e
instala um aplicativo de **24 MB**.

### ARM64

`win-arm64` está declarado no projeto, mas **não foi testado** nesta
máquina (amd64). Para gerar:

```powershell
dotnet publish src\FHZIPA.Installer\FHZIPA.Installer.csproj -c Release -r win-arm64 ...
```

Só declare suporte a ARM64 depois de rodar o instalador em um Windows ARM.

## O desinstalador

Ele é compilado com o compilador do .NET Framework que já vem no Windows:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe" `
    /nologo /target:winexe /platform:anycpu /optimize+ `
    /out:out\publish\desinstalar.exe `
    /reference:System.dll /reference:System.Core.dll /reference:System.Windows.Forms.dll `
    src\FHZIPA.Uninstaller\Uninstaller.cs
```

O instalador procura `desinstalar.exe` ao lado de si e o copia para a pasta
de instalação. Se ele não estiver lá, o próprio instalador assume o papel de
desinstalador (funciona, mas deixa 67 MB na pasta).

## Checksums

`SHA256SUMS.txt` sai junto com os artefatos, no formato do `sha256sum`:

```
0cbb2cae...  fhzipa-inject.exe
```

Para conferir:

```powershell
Get-FileHash out\publish\FHZIPA_INJECT_Setup.exe -Algorithm SHA256
```

## Assinatura digital

O instalador **não** é assinado. Para assinar:

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f "<certificado.pfx>" /p "<senha>" out\publish\FHZIPA_INJECT_Setup.exe
```

Requer um certificado Authenticode emitido por uma autoridade confiável.
Um certificado autoassinado não faz o SmartScreen parar de avisar e não
deve ser usado para distribuição. Ver [SECURITY.md](SECURITY.md).
