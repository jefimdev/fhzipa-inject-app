<#
.SYNOPSIS
    Compila o instalador do FHZIPA INJECT, roda os testes e publica os
    artefatos na pasta de distribuicao.

.DESCRIPTION
    O script faz, nesta ordem:
      1. confere as ferramentas necessarias;
      2. sincroniza o aplicativo e o resumo de integridade;
      3. restaura e compila;
      4. roda os testes;
      5. publica o instalador autocontido de arquivo unico;
      6. confere os artefatos;
      7. gera os checksums e o relatorio;
      8. publica na pasta de distribuicao.

    Nenhum passo obrigatorio pode falhar em silencio: o script para no
    primeiro erro.

.PARAMETER Configuration
    Debug ou Release. O padrao e Release.

.PARAMETER SkipTests
    Nao roda os testes. So para verificacao rapida de compilacao.

.PARAMETER SkipPublish
    Nao publica na pasta de distribuicao.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File scripts\build.ps1
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$SkipTests,

    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path -Parent $PSScriptRoot
$project = Join-Path $root 'src\FHZIPA.Installer\FHZIPA.Installer.csproj'
$solution = Join-Path $root 'FHZIPA-INJECT.slnx'
$payloadTarget = Join-Path $root 'src\FHZIPA.Installer\Resources\fhzipa-inject.exe'
$distribution = Join-Path (Split-Path -Parent $root) 'FHZIPA INJECT'
$publishFolder = Join-Path $root 'out\publish'

# O aplicativo pode estar ao lado do instalador (projeto separado) ou na
# raiz do mesmo repositorio. Os dois caminhos sao tentados.
$payloadCandidates = @(
    (Join-Path (Split-Path -Parent $root) 'fhzipa-inject-app\src-tauri\target\release\fhzipa-inject.exe'),
    (Join-Path (Split-Path -Parent $root) 'src-tauri\target\release\fhzipa-inject.exe')
)
$payloadSource = $payloadCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $payloadSource) {
    $payloadSource = $payloadCandidates[0]
}

function Write-Step([string]$message) {
    Write-Host ''
    Write-Host "==> $message" -ForegroundColor Cyan
}

function Write-Ok([string]$message) {
    Write-Host "    $message" -ForegroundColor Green
}

function Write-Info([string]$message) {
    Write-Host "    $message" -ForegroundColor DarkGray
}

$startedAt = Get-Date

# ---------------------------------------------------------------------
# 1. Ferramentas
# ---------------------------------------------------------------------
Write-Step 'Conferindo as ferramentas'

$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
if (-not $dotnet) {
    throw 'O SDK do .NET nao foi encontrado no PATH. Instale o .NET 10 SDK.'
}

$sdks = & dotnet --list-sdks
if (-not ($sdks | Where-Object { $_ -match '^10\.' })) {
    throw 'Este projeto precisa do .NET 10 SDK (a versao instalada nao serve).'
}

Write-Ok "SDK: $(($sdks | Select-Object -Last 1).Split(' ')[0])"

# ---------------------------------------------------------------------
# 2. Payload e resumo de integridade
# ---------------------------------------------------------------------
Write-Step 'Sincronizando o aplicativo embutido'

if (-not (Test-Path $payloadSource)) {
    throw "O aplicativo nao foi encontrado em $payloadSource. Compile o app antes (npm run tauri build)."
}

Copy-Item $payloadSource $payloadTarget -Force
$hash = (Get-FileHash $payloadTarget -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText(
    (Join-Path $root 'src\FHZIPA.Installer\Resources\payload.sha256'),
    $hash,
    [System.Text.Encoding]::ASCII)

Write-Ok "Aplicativo: $([math]::Round((Get-Item $payloadTarget).Length / 1MB, 2)) MB"
Write-Ok "SHA-256: $hash"

# ---------------------------------------------------------------------
# 3. Restore e build
# ---------------------------------------------------------------------
Write-Step "Restaurando e compilando ($Configuration)"

& dotnet restore $solution | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'O restore falhou.' }

& dotnet build $solution -c $Configuration --no-restore -v minimal
if ($LASTEXITCODE -ne 0) { throw 'A compilacao falhou.' }

Write-Ok 'Compilacao concluida.'

# ---------------------------------------------------------------------
# 4. Testes
# ---------------------------------------------------------------------
if (-not $SkipTests) {
    Write-Step 'Rodando os testes'

    & dotnet test $solution -c $Configuration --no-build -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Os testes falharam. A distribuicao foi interrompida.' }

    Write-Ok 'Todos os testes passaram.'
}
else {
    Write-Info 'Testes pulados (--SkipTests).'
}

# ---------------------------------------------------------------------
# 5. Publicacao
# ---------------------------------------------------------------------
Write-Step 'Publicando o instalador'

if (Test-Path $publishFolder) {
    Remove-Item $publishFolder -Recurse -Force
}

& dotnet publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    --no-build `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none `
    -o $publishFolder

if ($LASTEXITCODE -ne 0) { throw 'A publicacao falhou.' }

$installer = Join-Path $publishFolder 'FHZIPA_INJECT_Setup.exe'
if (-not (Test-Path $installer)) {
    throw "O instalador nao foi gerado em $installer."
}

$installerSize = (Get-Item $installer).Length
Write-Ok "Instalador: $([math]::Round($installerSize / 1MB, 2)) MB"

# ---------------------------------------------------------------------
# 5b. O desinstalador companion
# ---------------------------------------------------------------------
Write-Step 'Compilando o desinstalador'

# O desinstalador usa .NET Framework 4.8, que ja vem em todo Windows 10 e
# 11. Assim ele nao depende de nenhum tempo de execucao extra.
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    throw "O compilador do .NET Framework nao foi encontrado em $csc."
}

$uninstallerSource = Join-Path $root 'src\FHZIPA.Uninstaller\Uninstaller.cs'
$uninstallerOutput = Join-Path $publishFolder 'desinstalar.exe'
$uninstallerIcon = Join-Path $root 'src\FHZIPA.Installer\Assets\app.ico'

& $csc `
    /nologo `
    /target:winexe `
    /platform:anycpu `
    /optimize+ `
    "/out:$uninstallerOutput" `
    '/reference:System.dll' `
    '/reference:System.Core.dll' `
    '/reference:System.Windows.Forms.dll' `
    "/win32icon:$uninstallerIcon" `
    $uninstallerSource

if ($LASTEXITCODE -ne 0) { throw 'A compilacao do desinstalador falhou.' }
if (-not (Test-Path $uninstallerOutput)) { throw 'O desinstalador nao foi gerado.' }

Write-Ok "Desinstalador: $([math]::Round((Get-Item $uninstallerOutput).Length / 1KB, 1)) KB"

# ---------------------------------------------------------------------
# 6. Validacao dos artefatos
# ---------------------------------------------------------------------
Write-Step 'Validando os artefatos'

$files = @(Get-ChildItem $publishFolder -File)
if ($files.Count -lt 1) {
    throw 'A publicacao nao produziu nenhum arquivo.'
}

foreach ($file in $files) {
    Write-Info ("{0}  ({1} KB)" -f $file.Name, [math]::Round($file.Length / 1KB, 1))
}

$version = (Get-Item $installer).VersionInfo.FileVersion
Write-Ok "Versao do arquivo: $version"

# ---------------------------------------------------------------------
# 7. Checksums e relatorio
# ---------------------------------------------------------------------
Write-Step 'Gerando os checksums'

$checksums = foreach ($file in $files) {
    $fileHash = (Get-FileHash $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "$fileHash  $($file.Name)"
}

$checksumFile = Join-Path $publishFolder 'SHA256SUMS.txt'
[System.IO.File]::WriteAllLines($checksumFile, $checksums, [System.Text.Encoding]::ASCII)
Write-Ok "SHA256SUMS.txt gerado."

# ---------------------------------------------------------------------
# 8. Publicacao na pasta de distribuicao
# ---------------------------------------------------------------------
if (-not $SkipPublish) {
    Write-Step 'Publicando na pasta de distribuicao'

    # A pasta de distribuicao e a que fica ao lado do projeto do
    # instalador. Quando o instalador esta dentro do repositorio do
    # aplicativo, esse caminho seria a raiz do repositorio: nesse caso a
    # publicacao e pulada, para nao sujar o codigo do aplicativo com o
    # instalador gerado. Os artefatos continuam em out\publish.
    $distributionParent = Split-Path -Parent $distribution
    $distributionInsideApp = Test-Path (Join-Path $distributionParent 'src-tauri')

    if ($distributionInsideApp) {
        Write-Ok "Pasta de distribuicao pulada (o instalador esta dentro do repositorio do aplicativo). Artefatos em out\publish."
    }
    else {
        if (-not (Test-Path $distribution)) {
            New-Item -ItemType Directory -Force -Path $distribution | Out-Null
        }

        Copy-Item $installer (Join-Path $distribution 'FHZIPA_INJECT_Setup.exe') -Force
        Copy-Item $checksumFile (Join-Path $distribution 'SHA256SUMS.txt') -Force

        Write-Ok "Instalador publicado em $distribution"
    }
}

# ---------------------------------------------------------------------
# Relatorio
# ---------------------------------------------------------------------
$elapsed = (Get-Date) - $startedAt

Write-Host ''
Write-Host '=====================================================' -ForegroundColor Cyan
Write-Host ' RELATORIO DA COMPILACAO' -ForegroundColor Cyan
Write-Host '=====================================================' -ForegroundColor Cyan
Write-Host " Configuracao      : $Configuration"
Write-Host " Aplicativo        : $([math]::Round((Get-Item $payloadTarget).Length / 1MB, 2)) MB"
Write-Host " SHA-256 do app    : $hash"
Write-Host " Instalador        : $([math]::Round($installerSize / 1MB, 2)) MB"
Write-Host " Pasta de artefatos: $publishFolder"
if (-not $SkipPublish) {
    Write-Host " Distribuicao      : $distribution"
}
Write-Host " Tempo total       : $([math]::Round($elapsed.TotalSeconds, 1)) s"
Write-Host '=====================================================' -ForegroundColor Cyan
