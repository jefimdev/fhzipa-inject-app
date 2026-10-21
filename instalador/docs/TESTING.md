# Testes

## Como rodar

```powershell
dotnet test FHZIPA-INJECT.slnx -c Release
```

Resultado atual: **59 testes, todos passando**.

```powershell
dotnet test FHZIPA-INJECT.slnx -c Debug --logger "console;verbosity=detailed"
```

## O que é testado

| Arquivo | Cobre |
|---|---|
| `StateMachineTests.cs` | transições válidas e inválidas, o caminho completo, o cancelamento. |
| `PathValidatorTests.cs` | caminho vazio, relativo, unidade inexistente, caractere inválido, nome reservado, caminho longo, escrita, escape da área gerenciada. |
| `ProgressTests.cs` | porcentagem, limites, velocidade e tempo restante. |
| `ManifestTests.cs` | validação do manifesto e da configuração, catálogo de erros. |
| `InstallationTests.cs` | instalação, resumo errado, rollback, restauração do arquivo anterior, desinstalação, registro. |
| `LocalizationTests.cs` | toda chave usada no XAML existe no arquivo de recursos. |

## O que é testado de verdade (manual)

Estes foram executados nesta máquina, com o instalador compilado:

| Cenário | Resultado |
|---|---|
| Instalação limpa | o aplicativo e o desinstalador são copiados, os atalhos criados e o registro escrito. |
| Instalação em pasta inexistente | a pasta é criada e a instalação segue. |
| Resumo de integridade errado | a instalação é recusada com `FHZ-3003` e nada é gravado. |
| Cancelamento | a operação para e o registro é desfeito. |
| Desinstalação | a pasta, os atalhos e o registro saem; os dados ficam. |
| Instalação por cima | o arquivo anterior é guardado e restaurado em caso de falha. |
| Falta de permissão | o caminho é recusado com a explicação. |
| WebView2 ausente | aviso, não bloqueio. |
| Navegação por teclado | `Tab`, `Enter` e `Esc` funcionam. |
| Instância dupla | a segunda janela avisa e sai. |

## A matriz de cenários

A lista completa dos 40 cenários está em `docs/../docs/CENARIOS.md`? Não —
ela está aqui:

| # | Cenário | Coberto | Onde |
|---|---|---|---|
| 1 | Instalação limpa | sim | teste automatizado + manual |
| 2 | Internet lenta | sim | `DownloadService` com espera exponencial |
| 3 | Sem internet | parcial | aviso na verificação de rede; o pacote local cobre a instalação |
| 4 | Download interrompido | sim | retomada por `Range` |
| 5 | Download corrompido | sim | SHA-256 |
| 6 | Diretório sem permissão | sim | `PathValidator.IsWritable` |
| 7 | Disco sem espaço | sim | verificação de espaço |
| 8 | Instalação existente | sim | detecção na tela de diretório |
| 9 | Versão mais recente instalada | não | depende de manifesto remoto |
| 10 | Atualização | não | depende de manifesto remoto |
| 11 | Reparação | sim | instalar por cima |
| 12 | Cancelar no download | sim | `CancellationToken` |
| 13 | Cancelar na instalação | sim | rollback |
| 14 | Arquivo em uso | parcial | o erro é reportado, sem teste automatizado |
| 15 | Dependência ausente | sim | WebView2 |
| 16 | Dependência incompatível | não | — |
| 17 | Falha ao criar atalho | sim | o aviso é registrado, a instalação continua |
| 18 | Falha ao abrir o app | sim | `ProcessLauncher` |
| 19 | Desinstalação | sim | teste automatizado + manual |
| 20 | Reinstalação | sim | manual |
| 21 | Execução simultânea | sim | mutex + teste manual |
| 22 | Reinicialização inesperada | sim | registro sobrevivente |
| 23 | Recuperação após interrupção | parcial | o registro é lido; a tela de recuperação não existe |
| 24 | Caminho com Unicode | sim | validação |
| 25 | Caminho longo | sim | validação |
| 26 | DPI elevado | parcial | `PerMonitorV2`; falta medição em 200% |
| 27 | Monitor secundário | parcial | a janela se centraliza na tela principal |
| 28 | Usuário sem privilégio | sim | manual |
| 29 | Proxy corporativo | não | — |
| 30 | Antivírus bloqueando | parcial | o erro é reportado |
| 31 | HTTP 404 | sim | `EnsureSuccessStatusCode` |
| 32 | HTTP 500 | sim | idem |
| 33 | Timeout | sim | `TaskCanceledException` tratada como tempo esgotado |
| 34 | Falha de TLS | sim | o certificado nunca é ignorado |
| 35 | Temporário bloqueado | sim | `IOException` tratada |
| 36 | Espaço esgotado na extração | parcial | o erro é reportado |
| 37 | Falha no rollback | sim | `FHZ-7001` e `RecoveryRequired` |
| 38 | Configuração inválida | sim | `AppConfiguration.Validate` |
| 39 | Manifesto incompatível | sim | `DistributionManifest.Validate` |
| 40 | Pacote com assinatura inválida | não | a assinatura do app é verificada pelo Windows |

## O que falta automatizar

1. Testes de interface com WPF UI Automation (a navegação é testada de
   forma assistida, não automatizada).
2. Testes de carga e retomada com um servidor HTTP local controlado.
3. Teste de interrupção real (matar o processo no meio da cópia).
4. Medição de DPI em 150% e 200%.

## Regras dos testes

- Nenhum teste mexe na instalação real do usuário: tudo vai para `%TEMP%`.
- Nenhum teste depende da rede.
- Nenhum teste deixa lixo: as pastas de teste são apagadas no final.
