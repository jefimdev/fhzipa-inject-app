/**
 * Checks the GitHub release counters and reports every new download.
 *
 * The previous counters are kept in `download-state.json` at the root of the
 * repository, so the very first run only establishes a baseline and never
 * reports the whole existing history as new downloads.
 *
 * The channels are read from the repository secrets:
 *   DISCORD_WEBHOOK, TELEGRAM_TOKEN, TELEGRAM_CHAT_ID, NTFY_SERVER, NTFY_TOPIC
 */

import { readFileSync, writeFileSync, existsSync } from "node:fs";
import { resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..", "..");
const STATE_FILE = resolve(ROOT, "download-state.json");

const owner = process.env.GITHUB_REPOSITORY_OWNER ?? "fhzipa";
const repo = process.env.GITHUB_REPOSITORY?.split("/")[1] ?? "fhzipa-inject";
const token = process.env.GITHUB_TOKEN;

const headers = {
  Accept: "application/vnd.github+json",
  "X-GitHub-Api-Version": "2022-11-28",
  "User-Agent": "fhzipa-download-notifier",
  ...(token ? { Authorization: `Bearer ${token}` } : {}),
};

const readState = () => {
  if (!existsSync(STATE_FILE)) return { counters: {} };
  try {
    return JSON.parse(readFileSync(STATE_FILE, "utf8"));
  } catch {
    return { counters: {} };
  }
};

const writeState = (state) => {
  writeFileSync(STATE_FILE, `${JSON.stringify(state, null, 2)}\n`, "utf8");
};

const formatSize = (bytes) => {
  const units = ["B", "KB", "MB", "GB"];
  let value = bytes || 0;
  let unit = 0;
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024;
    unit += 1;
  }
  return `${value.toFixed(unit === 0 ? 0 : 1)} ${units[unit]}`;
};

const post = async (url, init) => {
  const response = await fetch(url, init);
  if (!response.ok) {
    const body = await response.text().catch(() => "");
    throw new Error(`${response.status} ${body.slice(0, 200)}`);
  }
};

const notifyDiscord = async (webhook, text) => {
  await post(webhook, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ username: "FHZIPA MONITOR", content: text }),
  });
};

const notifyTelegram = async (botToken, chatId, text) => {
  await post(`https://api.telegram.org/bot${botToken}/sendMessage`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ chat_id: chatId, text }),
  });
};

const notifyNtfy = async (server, topic, text) => {
  const base = (server || "ntfy.sh").replace(/\/+$/, "");
  await post(`${base}/${topic}`, {
    method: "POST",
    headers: { Title: "FHZIPA MONITOR", Priority: "high", Tags: "tada" },
    body: text,
  });
};

const main = async () => {
  const response = await fetch(
    `https://api.github.com/repos/${owner}/${repo}/releases?per_page=100`,
    { headers },
  );

  if (!response.ok) {
    throw new Error(
      `GitHub respondeu ${response.status} para ${owner}/${repo}`,
    );
  }

  const releases = await response.json();
  const state = readState();
  const counters = state.counters ?? {};
  const events = [];

  for (const release of releases) {
    if (release.draft) continue;

    for (const asset of release.assets ?? []) {
      const key = `${release.tag_name}|${asset.name}`;
      const previous = counters[key];
      const delta =
        typeof previous === "number"
          ? Math.max(0, asset.download_count - previous)
          : 0;

      counters[key] = asset.download_count;

      if (delta > 0) {
        events.push({
          tag: release.tag_name,
          name: release.name || release.tag_name,
          asset: asset.name,
          size: asset.size,
          total: asset.download_count,
          delta,
          assetUrl: asset.browser_download_url,
          releaseUrl: release.html_url,
        });
      }
    }
  }

  state.counters = counters;
  state.lastCheck = new Date().toISOString();
  writeState(state);

  if (events.length === 0) {
    console.log("Nenhum download novo.");
    return;
  }

  const totalDelta = events.reduce((sum, event) => sum + event.delta, 0);
  console.log(`${events.length} evento(s), ${totalDelta} download(s).`);

  for (const event of events) {
    const text = [
      "🎉 Novo download do FHZIPA INJECT",
      "",
      `Release: ${event.name}`,
      `Arquivo: ${event.asset}`,
      `Tamanho: ${formatSize(event.size)}`,
      `Downloads: ${event.total} (+${event.delta})`,
      `Detectado em: ${new Date().toLocaleString("pt-BR")}`,
      "",
      event.releaseUrl,
      event.assetUrl,
    ].join("\n");

    const jobs = [];

    if (process.env.DISCORD_WEBHOOK) {
      jobs.push(
        notifyDiscord(process.env.DISCORD_WEBHOOK, text).catch((e) =>
          console.error(`Discord falhou: ${e.message}`),
        ),
      );
    }

    if (process.env.TELEGRAM_TOKEN && process.env.TELEGRAM_CHAT_ID) {
      jobs.push(
        notifyTelegram(
          process.env.TELEGRAM_TOKEN,
          process.env.TELEGRAM_CHAT_ID,
          text,
        ).catch((e) => console.error(`Telegram falhou: ${e.message}`)),
      );
    }

    if (process.env.NTFY_TOPIC) {
      jobs.push(
        notifyNtfy(
          process.env.NTFY_SERVER,
          process.env.NTFY_TOPIC,
          text,
        ).catch((e) => console.error(`ntfy falhou: ${e.message}`)),
      );
    }

    await Promise.all(jobs);
  }
};

main().catch((e) => {
  console.error(e.message ?? e);
  process.exitCode = 1;
});
