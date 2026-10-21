# Segurança

## O que o instalador garante

### Integridade

- O aplicativo embutido tem o SHA-256 gravado em um recurso ao lado.
  O resumo é conferido **antes** de qualquer escrita no disco.
- Quando o manifesto remoto é usado, o resumo vem do manifesto e a
  transferência só por HTTPS, com a validação do certificado ligada.
- A retomada de download confere `Content-Range` e o tamanho esperado:
  pedaço de outra versão nunca é junto.

### Privilégio mínimo

- O instalador roda com o token do usuário (`asInvoker`).
- A elevação só é pedida quando a pasta escolhida está dentro de
  `Program Files` ou `Windows`, e só no momento em que a instalação vai
  começar.
- Nada é escrito em `HKLM`; o registro do desinstalador fica em `HKCU`.

### Proteção contra escrita fora da área

- `PathValidator.IsInside` confere que um caminho filho fica dentro da
  pasta gerenciada.
- O caminho de instalação é validado: absoluto, sem caracteres que o
  Windows recusa, sem nomes reservados, sem junções ou links simbólicos,
  dentro do limite de 260 caracteres.
- A pasta temporária é criada sob `%TEMP%` com um identificador único e
  apagada no final, mesmo quando a instalação falha.

### Instalações simultâneas

- Um mutex nomeado garante que só exista um instalador rodando. A segunda
  janela avisa e sai.

### Integridade da transação

- O registro do que foi feito (`journal.json`) é gravado **antes** da
  primeira escrita.
- Falha em qualquer etapa dispara o rollback: os arquivos instalados são
  removidos, o arquivo anterior é restaurado, os atalhos criados são
  apagados e as pastas criadas são removidas.
- O rollback é idempotente.
- Uma instalação interrompida deixa o registro em
  `%LOCALAPPDATA%\FHZIPA INJECT\journal.json` e é identificada na próxima
  execução.

### Segredos

- O instalador **não** pede conta Apple, senha, token ou cookie.
- O log nunca registra credencial: nem senha, nem token, nem chave privada.
- Nenhuma informação sai do computador. Não há telemetria.

### Execução

- O aplicativo é aberto com `ProcessStartInfo` e argumentos que vêm do
  próprio instalador, nunca de entrada do usuário.
- Só arquivos `.exe` existentes são abertos.

## O que o instalador **não** faz

| Não faz | Por quê |
|---|---|
| Não desativa proteção do Windows | A validação de TLS e o UAC ficam como estão. |
| Não pede exclusão no antivírus | Isso é decisão da pessoa, não do instalador. |
| Não instala drivers | O driver do iPhone é responsabilidade do app e do iTunes. |
| Não cria persistência oculta | Nada é escrito fora das pastas documentadas. |
| Não executa tarefas administrativas sem consentimento | A elevação é sempre solicitada ao UAC. |

## Limitações conhecidas

### Sem assinatura Authenticode

O instalador **não** tem certificado. O Windows SmartScreen vai mostrar o
aviso de "aplicativo não reconhecido" para quem não conhece o editor.

Isso é o comportamento esperado de qualquer programa sem assinatura.
Nenhuma técnica é usada para contornar o aviso, e nenhum certificado
autoassinado foi criado para fingir confiança.

Para publicar com assinatura, veja [BUILD.md](BUILD.md#assinatura-digital).

### Sem instalador de Bootstrapping

O instalador não baixa drivers nem componentes de terceiros. Ele
instala o aplicativo e informa o que está faltando.

### `win-arm64` não testado

O projeto tem a arquitetura declarada, mas ela não foi validada em
hardware ARM. Não anuncie suporte a ARM64 antes de testar.

### Sem proteção contra o usuário trocando o executável

O instalador confere a integridade do **pacote** (o que ele próprio
transporta). Ele não pode impedir que alguém altere o executável depois
de instalado — nem nothing pode.

## Como reportar um problema

Inclua:

1. a versão do Windows (`winver`);
2. o conteúdo de `%LOCALAPPDATA%\FHZIPA INJECT\Logs\instalador.log`;
3. o que você esperava e o que aconteceu;
4. o código do erro que apareceu na tela (por exemplo `FHZ-3003`).
