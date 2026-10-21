# Instalação

## O que o instalador faz, tela por tela

### 1. Boas-vindas

Explica o que o instalador faz e deixa claro o que **não** é dele: instalar
aplicativos no iPhone é responsabilidade do FHZIPA INJECT, com a conta
Apple do usuário.

O aviso em vermelho explica que a assinatura do aplicativo depende do tipo
de conta Apple, dos certificados, das regras de provisionamento, da
plataforma e da validade da assinatura — e que aplicativos assinados com
conta gratuita podem ter validade limitada.

Os três destaques (Privado, Leve, Reversível) são fatos verificáveis do
instalador, não garantias sobre o aplicativo.

Botões: **Instalar** e **Cancelar**.

### 2. Diretório de instalação

- O caminho padrão é `%ProgramFiles%\FHZIPA INJECT`, resolvido pela API do
  Windows (nada de `C:` fixo).
- **Procurar…** abre o seletor de pastas do próprio Windows.
- Espaço necessário e disponível são medidos na unidade escolhida.
- Os atalhos da área de trabalho e do menu Iniciar podem ser desmarcados.
- Se já existe uma instalação na pasta, um aviso informa que instalar por
  cima repara a instalação sem apagar os dados.

Ao clicar em **Instalar**:

- se a pasta exigir administrador, o Windows pede a autorização e o
  instalador reabre sozinho com o mesmo caminho;
- se não exigir, a instalação começa na hora.

### 3. Verificação dos requisitos

Cinco verificações reais, uma por linha, com o resultado à direita:
sistema operacional, espaço em disco, permissões de administrador,
dependências (WebView2) e conexão com a internet. As verificações críticas
impedem o avanço; os avisos são explicados, com o ícone âmbar. O botão
**Verificar de novo** refaz a checagem.

### 4. Obtenção dos arquivos

O aplicativo viaja dentro do instalador. O progresso mostra bytes reais, a
velocidade e o tempo restante, e o resumo SHA-256 é conferido antes de
qualquer escrita.

### 5. Instalação

As operações que estão acontecendo de verdade, uma por linha, com o estado
de cada uma: preparar a área, extrair o aplicativo, copiar os arquivos,
criar os atalhos e registrar o desinstalador. A barra mostra os bytes
reais, não uma porcentagem inventada.

### 6. Componentes

Cada componente mostra o estado verdadeiro: o aplicativo, os atalhos, o
registro do desinstalador e o desinstalador em si. Componentes obrigatórios
não são ignorados em silêncio.

### 7. Finalização

As três verificações finais: executável principal presente, atalhos
criados e desinstalador registrado. **A instalação só é declarada
concluída depois que as três passam.**

### 8. Conclusão

O conteúdo fica centralizado no painel: o selo verde, o título, a
descrição, a versão e a pasta, o botão grande **Abrir FHZIPA INJECT**, o
**Concluir** e a confirmação no rodapé.

### 9. Abertura do aplicativo

A captura real do aplicativo, a pergunta *Deseja abrir agora?*, os botões
**Abrir agora** e **Agora não**, e a opção de criar o atalho na área de
trabalho (que cria o atalho de verdade).

## O que fica no computador

| Item | Onde |
|---|---|
| O aplicativo | `%ProgramFiles%\FHZIPA INJECT\fhzipa-inject.exe` |
| O desinstalador | `%ProgramFiles%\FHZIPA INJECT\desinstalar.exe` |
| Atalho | Área de trabalho e Menu Iniciar (opcionais) |
| Registro | `HKCU\Software\Microsoft\Windows\CurrentVersion\Uninstall\FHZIPA INJECT` |
| Logs | `%LOCALAPPDATA%\FHZIPA INJECT\Logs\` |

Nada é escrito em `HKLM`, nada altera o Windows, nenhum driver é
instalado.

## Desinstalação

Em *Configurações → Aplicativos → FHZIPA INJECT → Desinstalar*, ou
executando `desinstalar.exe` na pasta de instalação.

O desinstalador:

1. pede confirmação;
2. remove os atalhos;
3. remove a entrada do registro;
4. apaga a pasta de instalação.

**Os seus dados e configurações são preservados.**

## Repetir a instalação

Rodar o instalador de novo por cima **repara** a instalação: o arquivo
antigo é guardado como `.fhzipa-antigo` e restaurado se a nova cópia
falhar.

## Cancelar

O botão **Cancelar** durante a instalação interrompe a operação, desfaz o
que já foi feito e apaga os temporários. Fechar a janela durante uma
instalação pede confirmação antes.

## O instalador em modo silencioso

O instalador aceita argumentos:

```powershell
FHZIPA_INJECT_Setup.exe --install-dir="C:\Apps\FHZIPA" --desktop-shortcut=1 --start-menu-shortcut=0
```

| Argumento | Efeito |
|---|---|
| `--install-dir="<caminho>"` | instala direto na pasta, sem passar pelas telas |
| `--desktop-shortcut=1\|0` | cria ou não o atalho da área de trabalho |
| `--start-menu-shortcut=1\|0` | cria ou não o atalho do menu Iniciar |
| `--uninstall` | roda o modo de desinstalação |

Com `--install-dir`, o instalador ainda pede a elevação quando a pasta
exige administrador, e continua mostrando o progresso na tela.
