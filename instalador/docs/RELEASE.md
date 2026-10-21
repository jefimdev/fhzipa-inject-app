# Publicação de uma versão

## Passo a passo

### 1. Compilar o aplicativo

O instalador carrega o executável do app. Ele precisa estar compilado:

```powershell
cd "..\fhzipa-inject-app"
npm run tauri build
```

O executável sai em
`src-tauri\target\release\fhzipa-inject.exe`.

### 2. Compilar o instalador

```powershell
cd "..\instalador fhzipa"
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

O script sincroniza o app, roda os testes, publica e distribui. Se algum
teste falhar, ele para — não publica.

### 3. Conferir o resultado

```powershell
$out = "C:\Users\fheiz\Downloads\projetos codes\FHZIPA INJECT"
Get-ChildItem $out
Get-Content "$out\SHA256SUMS.txt"
```

### 4. Assinar (opcional, mas recomendado)

```powershell
signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 `
    /f "<certificado.pfx>" /p "<senha>" `
    "C:\Users\fheiz\Downloads\projetos codes\FHZIPA INJECT\FHZIPA_INJECT_Setup.exe"
```

Sem um certificado de uma autoridade confiável, o SmartScreen vai avisar
todos que baixarem.

### 5. Testar a versão

Antes de publicar, rode o instalador em uma máquina limpa e confira:

- [ ] as 7 telas abrem sem erro;
- [ ] a instalação cria o aplicativo, os atalhos e o registro;
- [ ] a desinstalação remove tudo;
- [ ] o log não tem erros;
- [ ] o atalho abre o aplicativo.

## O que é publicado

Na pasta `FHZIPA INJECT`:

| Arquivo | O que é |
|---|---|
| `FHZIPA_INJECT_Setup.exe` | o instalador oficial (autocontido, arquivo único) |
| `SHA256SUMS.txt` | os checksums dos artefatos |
| `FHZIPA INJECT.exe` | o aplicativo portátil, para quem não quer instalar |
| `LEIA-ME.txt` | as instruções para o usuário |
| `preview.png` | a imagem de apresentação |

## Ajustar a versão

A versão aparece em três lugares, que precisam concordar:

| Onde | Arquivo |
|---|---|
| Metadados do instalador | `src/FHZIPA.Installer/FHZIPA.Installer.csproj` (`<Version>`) |
| Texto exibido | `src/FHZIPA.Installer/installer.config.json` (`Application.Version`) |
| Registro do Windows | `Services/InstallationService.cs` (`DisplayVersion`) |

Para automatizar, passe a versão por parâmetro no `build.ps1`:

```powershell
param([string]$Version = '1.0.0')
```

## Regras de versão

- Semântica: `MAIOR.MENOR.CORREÇÃO`.
- Só aumente a versão quando o conteúdo mudar.
- Um instalador já publicado não deve ser trocado no lugar: publique um
  arquivo novo, com nome novo, e guarde o antigo.

## Antes de publicar

- [ ] `dotnet test` passa.
- [ ] o log da instalação está limpo.
- [ ] a desinstalação foi testada.
- [ ] o `LEIA-ME.txt` foi atualizado.
- [ ] o checksum foi conferido.
- [ ] a assinatura foi aplicada (se houver certificado).
- [ ] a instalação foi testada em um Windows 10 e em um Windows 11.
