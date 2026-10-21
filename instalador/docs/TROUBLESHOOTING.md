# Solução de problemas

## Onde ficam os logs

```
%LOCALAPPDATA%\FHZIPA INJECT\Logs\instalador.log
```

O log é criado no primeiro arranque. Ele tem no máximo 2 MB e gira em até
5 arquivos (`instalador.1.log` …).

Para ver o log completo:

```powershell
Get-Content "$env:LOCALAPPDATA\FHZIPA INJECT\Logs\instalador.log"
```

## Mensagens da tela

| Código | O que significa | O que fazer |
|---|---|---|
| `FHZ-1002` | Já existe um instalador aberto | Feche a outra janela e rode de novo. |
| `FHZ-1003` | O aplicativo não veio no instalador | Baixe o instalador de novo e não mova os arquivos da pasta. |
| `FHZ-2001` | Windows sem suporte | Use Windows 10 ou 11 de 64 bits. |
| `FHZ-2002` | Espaço insuficiente | Libere espaço na unidade. |
| `FHZ-2003` | Sem permissão de escrita | Escolha outra pasta ou rode como administrador. |
| `FHZ-2004` | Caminho inválido | Escolha outra pasta. |
| `FHZ-2005` | WebView2 ausente | Instale o WebView2 pela página da Microsoft. |
| `FHZ-2006` | Sem internet | O pacote local cobre a instalação; a rede só é necessária para manifesto remoto. |
| `FHZ-3001` | Servidor indisponível | Tente de novo em alguns minutos. |
| `FHZ-3002` | Download interrompido | A retomada é automática; tente de novo se precisar. |
| `FHZ-3003` | Resumo não confere | O arquivo foi descartado. Baixe o instalador de novo. |
| `FHZ-3004` | Tempo esgotado | Tente com uma conexão melhor. |
| `FHZ-3005` | Falha de TLS | Confira a data e a hora do computador e o certificado da rede. |
| `FHZ-4001` | Falha ao extrair | Verifique o espaço e tente de novo. |
| `FHZ-4002` | Arquivo em uso | Feche o FHZIPA INJECT e o gerenciador de arquivos. |
| `FHZ-4003` | Falha ao copiar | Verifique as permissões da pasta. |
| `FHZ-4004` | Falha ao criar o atalho | A instalação continua; crie o atalho depois. |
| `FHZ-5001` | Dependência ausente | Rode com a internet ligada. |
| `FHZ-6002` | O aplicativo não abriu | Tente pelo menu Iniciar. |
| `FHZ-7001` | Falha ao desfazer | Rode o instalador de novo para reparar. |
| `FHZ-7002` | Instalação precisa de reparo | Rode o instalador de novo. |
| `FHZ-8001` | Cancelado | Nada foi deixado pela metade. |

## Problemas comuns

### "Já existe uma instalação em andamento"

Outro instalador está aberto. Feche a outra janela. Se não houver janela
visível, abra o Gerenciador de Tarefas e encerre `FHZIPA_INJECT_Setup.exe`.

### O Windows pergunta se deve permitir alterações

É o UAC. A pasta escolhida (normalmente `Program Files`) exige
administrador. Responda sim e o instalador continua sozinho.

### O SmartScreen avisa que o programa não é reconhecido

O instalador não tem assinatura Authenticode. Clique em
**Mais informações → Executar assim mesmo** se você confia na origem.
A assinatura resolve isso — ver [BUILD.md](BUILD.md#assinatura-digital).

### O antivírus bloqueou o instalador

Alguns programas de segurança bloqueiam instaladores sem assinatura. Não
adicione exclusões automáticas: decida você, conscientemente.

### A instalação foi interrompida

Rode o instalador de novo. Ele encontra a instalação pela metade (o
registro `journal.json` fica em `%LOCALAPPDATA%\FHZIPA INJECT\`) e instala
por cima, reparando.

Para forçar a limpeza manual:

```powershell
Remove-Item "$env:LOCALAPPDATA\FHZIPA INJECT\journal.json" -Force -ErrorAction SilentlyContinue
```

### O aplicativo instalado não abre

1. Tente pelo menu Iniciar.
2. Confira se o WebView2 está instalado.
3. Veja o log do **aplicativo**, que fica em
   `%LOCALAPPDATA%\FHZIPA INJECT\Logs\`.

### O desinstalador não remove tudo

Feche o FHZIPA INJECT e o Gerenciador de Arquivos antes de desinstalar.
Arquivos em uso não podem ser apagados pelo Windows.

### Quero instalar sem ver as telas

```powershell
FHZIPA_INJECT_Setup.exe --install-dir="C:\Apps\FHZIPA" --desktop-shortcut=0 --start-menu-shortcut=1
```

## Quando nada resolve

Reúna estas informações antes de pedir ajuda:

```powershell
$log = "$env:LOCALAPPDATA\FHZIPA INJECT\Logs\instalador.log"
Get-Content $log
[System.Environment]::OSVersion.VersionString
Get-FileHash "C:\caminho\FHZIPA_INJECT_Setup.exe" -Algorithm SHA256
```
