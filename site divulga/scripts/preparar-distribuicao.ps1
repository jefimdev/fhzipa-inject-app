<#
  Prepara a pasta downloads/ do site com o instalador aprovado.

  Copia o artefato validado do projeto do instalador, confere o resumo
  SHA-256 contra o valor esperado e grava o arquivo SHA256SUMS.txt.

  Uso:
    powershell -ExecutionPolicy Bypass -File scripts\preparar-distribuicao.ps1
    powershell -ExecutionPolicy Bypass -File scripts\preparar-distribuicao.ps1 `
      -Origem "C:\caminho\FHZIPA_INJECT_Setup.exe"

  O script NAO publica nada. Ele so deixa a pasta downloads/ pronta para
  ser enviada ao servidor de hospedagem.
#>
[CmdletBinding()]
param(
  [string]$Origem,
  [string]$Nome = 'FHZIPA_INJECT_Setup.exe',
  [string]$EsperadoSha256 = '743e40a4a176962e62e50a564661e83d80af6406872dbfefe1bfb7bf6572aef5'
)

$ErrorActionPreference = 'Stop'

$raiz = Split-Path -Parent $PSScriptRoot
$destinoPasta = Join-Path $raiz 'downloads'

if (-not $Origem) {
  $candidatos = @(
    @(
      (Join-Path $raiz '..\..\instalador fhzipa\out\publish'),
      (Join-Path $raiz '..\instalador fhzipa\out\publish'),
      (Join-Path $raiz '..\..\..\instalador fhzipa\out\publish')
    ) | Where-Object { Test-Path (Join-Path $_ $Nome) }
  )
  $candidatos = @($candidatos | Where-Object { $_ })

  if ($candidatos.Count -eq 0) {
    Write-Host "Instalador nao encontrado. Use -Origem <caminho do .exe>." -ForegroundColor Red
    exit 1
  }
  $Origem = Join-Path $candidatos[0] $Nome
}

if (-not (Test-Path $Origem)) {
  Write-Host "Arquivo de origem inexistente: $Origem" -ForegroundColor Red
  exit 1
}

New-Item -ItemType Directory -Path $destinoPasta -Force | Out-Null
$destino = Join-Path $destinoPasta $Nome

Write-Host "Origem  : $Origem"
Copy-Item $Origem $destino -Force

$hash = (Get-FileHash $destino -Algorithm SHA256).Hash.ToLower()
$tamanho = (Get-Item $destino).Length

Write-Host "Destino : $destino"
Write-Host "Tamanho : $tamanho bytes"
Write-Host "SHA-256 : $hash"

if ($hash -ne $EsperadoSha256.ToLower()) {
  Write-Host ""
  Write-Host "ATENCAO: o resumo SHA-256 NAO confere com o valor aprovado." -ForegroundColor Red
  Write-Host "Esperado: $EsperadoSha256" -ForegroundColor Red
  Write-Host "Obtido  : $hash" -ForegroundColor Red
  Write-Host "Nao publique o download ate revisar o artefato." -ForegroundColor Red
  exit 2
}

$resumo = "$hash  $Nome"
Set-Content -Path (Join-Path $destinoPasta 'SHA256SUMS.txt') -Value $resumo -Encoding utf8

Write-Host ""
Write-Host "SHA-256 confere com o artefato aprovado. Pasta downloads/ pronta." -ForegroundColor Green
