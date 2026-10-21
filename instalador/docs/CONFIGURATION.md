# Configuração

O instalador lê `installer.config.json`, que fica ao lado do executável.
O arquivo é opcional: sem ele, o instalador usa os valores padrão.

## O arquivo

```json
{
  "Application": {
    "Name": "FHZIPA INJECT",
    "ExecutableName": "fhzipa-inject.exe",
    "Version": "1.0.0",
    "Description": "Instalador de aplicativos para iPhone.",
    "Publisher": "FHZIPA INJECT"
  },
  "Installer": {
    "DefaultDirectory": "%ProgramFiles%\\FHZIPA INJECT",
    "CreateDesktopShortcut": true,
    "CreateStartMenuShortcut": true,
    "LaunchAfterInstall": true
  },
  "Downloads": {
    "ManifestUrl": null,
    "TimeoutSeconds": 30,
    "MaxRetries": 3,
    "UseEmbeddedPayload": true
  },
  "Security": {
    "RequireHashValidation": true,
    "VerifyDigitalSignatureWhenAvailable": true
  }
}
```

## O que cada campo faz

### `Application`

| Campo | Uso |
|---|---|
| `Name` | o nome exibido no painel e no rodapé. |
| `ExecutableName` | o nome do arquivo do aplicativo instalado. |
| `Version` | a versão mostrada na tela de conclusão. |
| `Description` | a descrição do produto. |
| `Publisher` | o fabricante gravado no registro. |

### `Installer`

| Campo | Uso |
|---|---|
| `DefaultDirectory` | a pasta sugerida. `%ProgramFiles%` e outras variáveis do Windows são expandidas. |
| `CreateDesktopShortcut` | cria o atalho da área de trabalho. |
| `CreateStartMenuShortcut` | cria o atalho do menu Iniciar. |
| `LaunchAfterInstall` | abre o aplicativo ao terminar. |

### `Downloads`

| Campo | Uso |
|---|---|
| `ManifestUrl` | o manifesto remoto. `null` = usa o pacote embutido. |
| `TimeoutSeconds` | o tempo limite de cada requisição. |
| `MaxRetries` | quantas tentativas em caso de falha. |
| `UseEmbeddedPayload` | usa o aplicativo embutido no instalador. |

### `Security`

| Campo | Uso |
|---|---|
| `RequireHashValidation` | exige o SHA-256 do pacote. |
| `VerifyDigitalSignatureWhenAvailable` | confere a assinatura digital quando houver. |

## Validação

`AppConfiguration.Validate()` devolve a lista de problemas. O instalador
recusa uma configuração inválida em vez de trabalhar com ela pela metade:

- `Application.Name` e `Application.ExecutableName` são obrigatórios;
- `Installer.DefaultDirectory` é obrigatório;
- `Downloads.TimeoutSeconds` tem que ser maior que zero;
- `Downloads.MaxRetries` não pode ser negativo.

## Manifesto remoto

Para usar um manifesto remoto, o formato é:

```json
{
  "schema": 1,
  "version": "1.0.0",
  "platform": "windows",
  "architecture": "x64",
  "files": [
    {
      "name": "fhzipa-inject.exe",
      "url": "https://exemplo.test/fhzipa-inject.exe",
      "size": 25000000,
      "sha256": "0cbb2caed726dd86fcdff9dcf8f92d922729b4bb90205e95e8112b11af7bd791"
    }
  ],
  "dependencies": [],
  "releaseNotes": null
}
```

Regras:

- `schema` precisa ser `1`;
- a `url` tem que ser **https**;
- o `size` e o `sha256` são obrigatórios;
- o resumo é conferido antes de o arquivo ser usado.

Um manifesto inválido interrompe a operação; ele nunca é usado pela
metade.
