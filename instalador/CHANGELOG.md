# Histórico

## 1.1.0

Reconstrução visual do instalador: identidade fiel à referência, janela
compacta, nove telas e eliminação das piscadas.

### Adicionado

- **Janela compacta**: um terço da área útil do monitor (640 × 348 neste
  computador), com o mínimo funcional de 760 × 520. O tamanho pedido, o
  aplicado, o motivo, o DPI e a resolução ficam registrados no log.
- **Fundo em seis camadas**, todas vetoriais: preto profundo, brilho
  radial, curvas vermelhas com contorno luminoso, iluminação do canto
  inferior esquerdo e vinheta escura.
- **Logo vetórica**: quadrado vermelho com gradiente, símbolo de download,
  brilho interno e sombra vermelha.
- **Painel lateral premium**: o vermelho do painel, a onda inferior, a
  marca grande, o nome em duas cores, o subtitulo com letras espaçadas
  (`INSTALADOR PARA WINDOWS`), os destaques e o indicador de etapas com
  o estado embaixo do nome.
- **Nove telas**, no lugar de sete: a instalação foi dividida em
  instalação, componentes e finalização, e a tela de abertura do
  aplicativo passou a ser a nona.
- **Tela de conclusão centralizada**, com o selo verde, o título, a
  descrição, o botão grande de abertura e a confirmação no rodapé.
- **Tela de abertura** com a captura real do aplicativo, o botão
  *Abrir agora*, o *Agora não* e a opção de atalho (que cria o atalho
  de verdade).
- **Velocidade e tempo restante** reais na tela de obtainção.
- **Cópia com progresso real**: a barra da instalação mostra bytes
  verdadeiros, não porcentagens inventadas.
- **Aviso âmbar** para as verificações que são aviso, em vez de erro.
- Teste que garante toda chave de texto usada no código existe no arquivo de
  recursos, e que o inglês tem as mesmas chaves do português.

### Alterado

- A navegação passou a usar o `ViewLocator`, que guarda a instância de cada
  tela. Recriar a tela a cada etapa era a origem das piscadas.
- O progresso passou a ser limitado a dez atualizações por segundo, para
  a interface não saltar durante a instalação.
- Os botões têm gradiente vermelho, brilho e estados de foco, hover e
  pressionado.
- Os cartões seguem a mesma borda, o mesmo espaçamento e a mesma
  tipografia.

### Corrigido

- O template do campo de texto engolia o texto (o `ScrollViewer` interno
  media sem altura).
- O `GridLength` com `%` não era aceito nesta cultura; a proporção do
  painel passou a ser controlada pelo código.
- Os recursos de brilho precisam vir antes dos estilos que os usam.
- O GUID do WebView2 estava com um caractere trocado e dava um falso aviso.
- O cálculo da fase do progresso estava em unidade errada.

### Pendente

- Assinatura Authenticode: sem certificado, o SmartScreen avisa.
- `win-arm64` declarado, mas não validado em hardware ARM.
- Tela de recuperação de instalação interrompida: o registro é gravado e
  lido, mas não há tela dedicada.

## 1.0.0

O instalador oficial do FHZIPA INJECT, reescrito do zero.

### Adicionado

- Instalador em WPF (.NET 10) com MVVM, injeção de dependência e
  máquina de estados com transições validadas.
- Verificação real de Windows, espaço, permissões, WebView2 e rede.
- Motor de instalação com registro de rollback, atalhos, registro do
  desinstalador e restauração do arquivo anterior.
- Aplicativo embutido com verificação do resumo SHA-256 antes de instalar.
- Desinstalador próprio (.NET Framework 4.8, 14 KB) que preserva os dados.
- Elevação pedida apenas quando o diretório exige administrador.
- Log com rotação, catálogo de erros `FHZ-XXXX`, 53 testes automatizados,
  build reproduzível e documentação.

### Alterado

- O instalador em NSIS foi **substituído** e está arquivado em
  `legacy-nsis/`. Ele continua no repositório como registro, mas não é mais
  mantido nem distribuído.
