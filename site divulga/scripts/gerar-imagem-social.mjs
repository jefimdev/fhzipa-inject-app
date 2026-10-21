/**
 * Gera a imagem de compartilhamento (Open Graph / Twitter Card).
 *
 *   node scripts/gerar-imagem-social.mjs
 *
 * Requer o playwright-core instalado no projeto (npm i -D playwright-core)
 * ou o pacote global do Playwright MCP. Ajusta CAMINHO_PLAYWRIGHT se o
 * modulo estiver em outro lugar. A imagem gerada fica em
 * img/social-compartilhamento.png (1200x630).
 */
import { createRequire } from "node:module";
import { fileURLToPath } from "node:url";
import path from "node:path";
import fs from "node:fs";

const require = createRequire(import.meta.url);
const RAIZ = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");

const CAMINHO_PLAYWRIGHT =
  process.env.CAMINHO_PLAYWRIGHT ||
  "C:/Users/fheiz/AppData/Roaming/npm/node_modules/@playwright/mcp/node_modules/playwright-core";

const { chromium } = require(CAMINHO_PLAYWRIGHT);

const NAVEGADOR =
  process.env.NAVEGADOR ||
  process.env.LOCALAPPDATA + "\\ms-playwright\\chromium-1248\\chrome-win64\\chrome.exe";

const modelo = path.join(RAIZ, "scripts", "modelo-imagem-social.html");
const destino = path.join(RAIZ, "img", "social-compartilhamento.png");

const navegador = await chromium.launch({ executablePath: NAVEGADOR });
const pagina = await navegador.newPage({ viewport: { width: 1200, height: 630 } });
await pagina.goto("file:///" + modelo.replace(/\\/g, "/"), { waitUntil: "networkidle" });
await pagina.waitForTimeout(400);
await pagina.screenshot({ path: destino, scale: "css" });
await navegador.close();

console.log("Imagem de compartilhamento gerada:", destino, fs.statSync(destino).size, "bytes");
