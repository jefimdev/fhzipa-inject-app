# Instalador em NSIS (substituído)

Este projeto é o **instalador antigo**, escrito em NSIS/MUI2, e ele foi
**substituído** pelo instalador em WPF (.NET 10) que está em `src/`.

Ele continua aqui por dois motivos:

1. nenhuma funcionalidade existente é removida sem registro;
2. ele serve de referência visual das telas e das decisões de identidade.

## Por que foi substituído

| | NSIS (antigo) | WPF (atual) |
|---|---|---|
| Interface | desenhada em bitmap, limitada | vetorial, com DPI e temas do Windows |
| Manutenção | scripts Node + gerador de arte | C#/.NET, com testes automatizados |
| Desinstalador | script gerado | executavel próprio, com confirmação |
| Acessibilidade | limitada | ordem de tabulação, `AutomationProperties`, navegação por teclado |
| Verificação de integridade | inexistente | SHA-256 do aplicativo conferido antes de instalar |

## Como usar (não é o caminho oficial)

```powershell
cd legacy-nsis
npm install
node build.mjs
```

O resultado sai em `legacy-nsis/out/`.

> **Atenção:** este caminho não é mais mantido. O instalador oficial é
> gerado por `scripts\build.ps1` na raiz do projeto.
