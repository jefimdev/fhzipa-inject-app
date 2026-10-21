/**
 * Monta o instalador do FHZIPA INJECT.
 *
 * O script junta o aplicativo já compilado com a arte do instalador e chama
 * o makensis. O instalador sai com o nome e as cores do produto.
 */
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, renameSync, rmSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { buildPortrait, readIcon } from "./lib/background.mjs";
import { theme } from "./lib/theme.mjs";

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)));
const APP = process.env.FHZIPA_APP ?? resolve(ROOT, "..", "fhzipa-inject-app");
const NSIS = process.env.FHZIPA_NSIS ?? resolve(
  process.env.LOCALAPPDATA ?? "",
  "tauri",
  "NSIS",
);

const PAYLOAD = join(ROOT, "payload");
const OUT = join(ROOT, "out");

const log = (message) => console.log(`  ${message}`);

const fail = (message) => {
  console.error(`\n  ${message}\n`);
  process.exit(1);
};

// ------------------------------------------------------------------ //
// O que entra no instalador                                           //
// ------------------------------------------------------------------ //

/** O executável do aplicativo, já compilado. */
const appExecutable = () => {
  const candidates = [
    join(APP, "src-tauri", "target", "release", "fhzipa-inject.exe"),
    join(APP, "src-tauri", "target", "release", "FHZIPA INJECT.exe"),
  ];
  return candidates.find((path) => existsSync(path));
};

/** A versão declarada pelo aplicativo. */
const appVersion = () => {
  const file = join(APP, "package.json");
  if (!existsSync(file)) return "1.0.0";
  try {
    return JSON.parse(readFileSync(file, "utf8")).version ?? "1.0.0";
  } catch {
    return "1.0.0";
  }
};

/** O ícone do aplicativo, no formato que o Windows usa. */
const appIcon = () => {
  const candidates = [
    join(APP, "src-tauri", "icons", "icon.ico"),
    join(APP, "src-tauri", "icons", "128x128@2x.png"),
  ];
  return candidates.find((path) => existsSync(path));
};

// ------------------------------------------------------------------ //
// A arte                                                               //
// ------------------------------------------------------------------ //

const buildArt = () => {
  const icon = appIcon();
  if (!icon) {
    fail("O icone do aplicativo nao foi encontrado.");
  }

  const picture = join(APP, "src-tauri", "icons", "128x128@2x.png");
  if (!existsSync(picture)) {
    fail("A imagem do icone do aplicativo nao foi encontrada.");
  }

  const portrait = buildPortrait(readIcon(picture));
  portrait.write(join(ROOT, "portrait.bmp"));
  log(`portrait.bmp  ${portrait.width}x${portrait.height}`);

};

// ------------------------------------------------------------------ //
// O payload                                                            //
// ------------------------------------------------------------------ //

const buildPayload = () => {
  rmSync(PAYLOAD, { recursive: true, force: true });
  mkdirSync(PAYLOAD, { recursive: true });

  const executable = appExecutable();
  if (!executable) {
    fail(
      "O aplicativo ainda nao foi compilado.\n" +
        "  Rode no projeto do aplicativo:  npm run tauri build\n",
    );
  }

  cpSync(executable, join(PAYLOAD, "fhzipa-inject.exe"));
  log(`fhzipa-inject.exe  ${(readFileSync(executable).length / 1048576).toFixed(1)} MB`);

  const icon = appIcon();
  if (icon) {
    cpSync(icon, join(PAYLOAD, "app.ico"));
    log("app.ico");
  }
};

// ------------------------------------------------------------------ //
// A compilação                                                         //
// ------------------------------------------------------------------ //

const compile = () => {
  const makensis = join(NSIS, "makensis.exe");
  if (!existsSync(makensis)) {
    fail(`O makensis nao foi encontrado em ${NSIS}.`);
  }

  rmSync(OUT, { recursive: true, force: true });
  mkdirSync(OUT, { recursive: true });

  const output = join(OUT, "FHZIPA INJECT_Setup.exe");
  const version = appVersion();

  // O makensis nao aceita o caminho da saida com barra invertida nem com
  // espaco, entao ele escreve com o nome dele e o arquivo recebe o nome
  // definitivo depois.
  // O script e lido como UTF-8, sem isso os acentos dos textos viram lixo.
  const args = ["/INPUTCHARSET", "UTF8", `/DAPP_VERSION=${version}`, "installer.nsi"];

  log("compilando...");
  try {
    execFileSync(makensis, args, { cwd: ROOT, stdio: "pipe" });
  } catch (error) {
    const detail = [error.stdout, error.stderr]
      .filter(Boolean)
      .map((buffer) => buffer.toString())
      .join("\n");
    fail(`O makensis falhou.\n${detail}`);
  }

  const temporary = join(ROOT, "installer.exe");
  const size = readFileSync(temporary).length;
  if (size < 1024 * 1024) {
    fail("O instalador saiu vazio, alguma coisa quebrou na compilação.");
  }

  rmSync(output, { force: true });
  renameSync(temporary, output);
  log(`FHZIPA INJECT_Setup.exe  ${(size / 1048576).toFixed(2)} MB  (versao ${version})`);

  return output;
};

// ------------------------------------------------------------------ //
// A publicação                                                         //
// ------------------------------------------------------------------ //

// A pasta onde os arquivos ficam para sair, a mesma que o usuario
// distribui.
const DIST = process.env.FHZIPA_DIST ?? resolve(ROOT, "..", "FHZIPA INJECT");

/** O texto que acompanha os arquivos. */
const readme = (versao) =>
  [
    "FHZIPA INJECT " + versao,
    "======================",
    "",
    "Assistente de sideloading para iPhone/iPad no Windows.",
    "",
    "COMO USAR",
    "---------",
    "1. Instale o iTunes ou o Apple Devices (necessario para o servico",
    "   Apple Mobile Device).",
    "2. Conecte o iPhone por USB, desbloqueie e toque em \"Confiar\".",
    "3. Abra o FHZIPA INJECT.",
    "4. Clique em \"Atualizar dispositivos\" e selecione o aparelho.",
    "5. Entre com seu Apple ID (a Apple pedira o codigo de 2FA).",
    "6. Na aba \"Baixar apps\" escolha o aplicativo e clique em \"Instalar\".",
    "   Ou use o painel \"Importar IPA\" com um arquivo .ipa.",
    "",
    "AVISO SOBRE A INSTALACAO DE APLICATIVOS NO iPHONE",
    "--------------------------------------------------",
    "O FHZIPA INJECT assina os aplicativos com a sua conta Apple ID e os",
    "instala no aparelho. Aplicativos assinados assim se comportam como",
    "qualquer app da loja:",
    "",
    "- a assinatura expira em 7 dias e o aplicativo precisa ser reinstalado;",
    "- um iPhone costuma aceitar 3 aplicativos assinados por conta;",
    "- a Apple pode revogar a assinatura a qualquer momento;",
    "- instalar aplicativos modificados viola os Termos de Servico da App Store.",
    "",
    "O QUE VEM NESTA PASTA",
    "---------------------",
    "- FHZIPA-INJECT-Setup.exe ... instalador, com atalho no menu Iniciar.",
    "- FHZIPA INJECT.exe ......... versao portatil, nao precisa instalar.",
    "",
    "Recomendado: use o instalador. Ele cria o atalho e registra o app para",
    "desinstalacao. A versao portatil tambem funciona: basta executar o .exe.",
    "",
    "REQUISITOS",
    "----------",
    "- Windows 10/11 64 bits.",
    "- iTunes/Apple Devices com o servico Apple Mobile Device em execucao.",
    "- Cabo USB e iPhone/iPad desbloqueado e confiado.",
    "- Internet para o login da Apple, para o Anisette e para a instalacao.",
    "- Em iOS 16+, pode ser preciso ativar Ajustes > Privacidade e",
    "  Seguranca > Modo de Desenvolvedor e reiniciar o aparelho.",
    "",
    "CONTA APPLE GRATUITA",
    "--------------------",
    "- Os apps assinados expiram em 7 dias.",
    "- Ate 3 apps assinados por dispositivo.",
    "- A Apple limita a criacao de App IDs (cerca de 10 por semana).",
    "- Uma conta paga do Apple Developer Program remove o limite de 7 dias.",
    "",
    "PRIVACIDADE",
    "-----------",
    "- Suas credenciais vao para os servicos da Apple durante o login; o app",
    "  nao possui servidor proprio de coleta.",
    "- O servidor Anisette padrao e ani2.stikstore.app e pode ser trocado nos",
    "  Ajustes.",
    "- Use apenas na sua propria conta e no seu proprio computador.",
    "",
    "LOGS",
    "----",
    "%APPDATA%\\app.fhzipa.inject\\logs",
    "",
    "SOBRE O APLICATIVO",
    "-------------------",
    "O FHZIPA INJECT nao e oficial e nao e afiliado a Apple. Use somente com",
    "aplicativos que voce tem o direito de instalar. O sideloading pode",
    "violar os Termos de Servico da Apple. Sem garantias.",
    "",
  ].join("\r\n");

/** Copia os arquivos para a pasta de distribuicao. */
const publish = (versao) => {
  if (!existsSync(DIST)) {
    log(`a pasta de distribuicao ${DIST} nao existe, pulando`);
    return;
  }

  // O instalador novo, no nome de sempre.
  const setup = join(OUT, "FHZIPA INJECT_Setup.exe");
  cpSync(setup, join(DIST, "FHZIPA-INJECT-Setup.exe"));
  log(`FHZIPA-INJECT-Setup.exe  ${(readFileSync(setup).length / 1048576).toFixed(2)} MB`);

  // A versao portatil, o aplicativo compilado.
  const portable = appExecutable();
  if (portable) {
    cpSync(portable, join(DIST, "FHZIPA INJECT.exe"));
    log(`FHZIPA INJECT.exe  ${(readFileSync(portable).length / 1048576).toFixed(1)} MB`);
  }

  // O texto de acompanhamento.
  writeFileSync(join(DIST, "LEIA-ME.txt"), readme(versao), "utf8");
  log("LEIA-ME.txt");

  log(`publicado em ${DIST}`);
};

// ------------------------------------------------------------------ //

console.log("\nInstalador FHZIPA INJECT\n");
console.log(`  cores: ${theme.red} sobre ${theme.bg}`);
buildArt();
buildPayload();
const output = compile();
publish(appVersion());
console.log(`\n  pronto: ${output}\n`);
