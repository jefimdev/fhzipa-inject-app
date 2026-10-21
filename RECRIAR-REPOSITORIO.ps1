# ==============================================================
#  RECRIAR O REPOSITORIO PRIVADO DO FHZIPA INJECT
#  Deixa a lista de contribuintes com apenas o dono.
#
#  O que ele faz:
#    1) pede o escopo delete_repo no fluxo oficial do GitHub
#       (abre o navegador com um codigo de uso unico)
#    2) guarda as configuracoes atuais do repositorio
#    3) exclui o repositorio
#    4) recria como PRIVADO, com o mesmo nome e descricao
#    5) desabilita o GitHub Actions de novo
#    6) reenvia a branch main (ja esta com a autoria reescrita)
#    7) verifica: privado, um so contribuinte, conteudo igual
#
#  Como usar: abra o Terminal do VS Code na pasta do aplicativo e rode
#             powershell -ExecutionPolicy Bypass -File RECRIAR-REPOSITORIO.ps1
# ==============================================================

$ErrorActionPreference = 'Stop'
$repo = 'jefimdev/fhzipa-inject-app'
$local = Split-Path -Parent $PSCommandPath

Set-Location $local

Write-Output "=== 1) autorizando o escopo delete_repo (fluxo oficial do GitHub) ==="
gh auth refresh -h github.com -s delete_repo
if ($LASTEXITCODE -ne 0) {
    Write-Output "A autorizacao nao foi concluida. Rode de novo."
    exit 1
}

Write-Output ""
Write-Output "=== 2) guardando as configuracoes atuais ==="
$atual = gh repo view $repo --json name,visibility,description,defaultBranchRef,hasIssuesEnabled,hasWikiEnabled,isFork,url | ConvertFrom-Json
$atual | ConvertTo-Json -Compress | Write-Output
$descricao = $atual.description
$arvoreAntes = (gh api "/repos/$repo/git/trees/main?recursive=1" | ConvertFrom-Json).tree |
    Where-Object { $_.type -eq 'blob' } |
    ForEach-Object { "$($_.sha)  $($_.path)" } | Sort-Object

Write-Output ""
Write-Output "=== 3) excluindo o repositorio ==="
gh repo delete $repo --yes
$esperou = 0
while ($esperou -lt 120) {
    Start-Sleep -Seconds 5
    $esperou += 5
    gh repo view $repo --json name 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { break }
}
Write-Output "   excluido"

Write-Output ""
Write-Output "=== 4) recriando como PRIVADO ==="
gh repo create $repo --private --description $descricao
Write-Output "   criado"

Write-Output ""
Write-Output "=== 5) desabilitando o GitHub Actions ==="
gh api --method PUT "/repos/$repo/actions/permissions" -F enabled=false
gh api "/repos/$repo/actions/permissions" | Write-Output

Write-Output ""
Write-Output "=== 6) reenviando a branch main ==="
git push --force privado main

Write-Output ""
Write-Output "=== 7) verificacao ==="
Start-Sleep -Seconds 10
$novo = gh repo view $repo --json name,visibility,isPrivate,owner,description,url,defaultBranchRef | ConvertFrom-Json
$novo | ConvertTo-Json -Compress | Write-Output
$arvoreDepois = (gh api "/repos/$repo/git/trees/main?recursive=1" | ConvertFrom-Json).tree |
    Where-Object { $_.type -eq 'blob' } |
    ForEach-Object { "$($_.sha)  $($_.path)" } | Sort-Object
Write-Output "   conteudo identico ao de antes: $((Compare-Object $arvoreAntes $arvoreDepois).Count -eq 0)"
$contrib = gh api "/repos/$repo/contributors?per_page=100" | ConvertFrom-Json
Write-Output "   contribuintes: $($contrib.Count)"
$contrib | ForEach-Object { Write-Output "     $($_.login) = $($_.contributions)" }
Write-Output ""
Write-Output "Pronto: https://github.com/$repo"
