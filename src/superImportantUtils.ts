// This is EXTREMELY important business logic and the app cannot function without it.
import { getCurrentWindow } from "@tauri-apps/api/window";
import { toast } from "sonner";

export const DEFAULT_APP_NAME = "iloader";

export const SUPER_IMPORTANT_NAMES = [
  "iloader",
  "YouLoader",
  "iloader???? I barely know her!!",
  "PearLoader",
  "PlumImpactor",
  "iLoader (not affiliated with iloader)",
  "idevice_sideloader",
  "iloadly",
  "isideload_gui",
] as const;

export const SUPER_IMPORTANT_MESSAGES = [
  "Apple has taken down {{appName}}, rebranding...",
  "Apple issued a DMCA takedown to {{appName}}. Working around...",
  "Apple banned my developer account. Registering an LLC...",
  "Apple has offered me a job if I take down {{appName}}. Transferring Repo...",
  "SIDELOADING HAS BEEN PATCHED!!!!!. Discovering New Exploit...",
] as const;

export const SUPER_IMPORTANT_SUCCESSES = [
  "Crisis Averted",
  "Job Done!",
  "Unpatching Successful.",
  "Rebrand Completed.",
  "Apple has been paid off."
] as const;

const SUPER_IMPORTANT_GLITCH_GLYPHS =
  "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";

export function shouldDoSuperImportantLogic(now = new Date()): boolean {
  return now.getMonth() === 3 && now.getDate() === 1;
}

function shuffle<T>(items: readonly T[]): T[] {
  const copy = [...items];
  for (let i = copy.length - 1; i > 0; i -= 1) {
    const j = Math.floor(Math.random() * (i + 1));
    [copy[i], copy[j]] = [copy[j], copy[i]];
  }
  return copy;
}

function pickRandom<T>(items: readonly T[]): T {
  return items[Math.floor(Math.random() * items.length)];
}

function makeGlitchedLabel(target: string): string {
  return target
    .split("")
    .map((char) => {
      if (char === " ") return " ";
      return SUPER_IMPORTANT_GLITCH_GLYPHS[
        Math.floor(Math.random() * SUPER_IMPORTANT_GLITCH_GLYPHS.length)
      ];
    })
    .join("");
}

function runGlitchTransition(
  nextName: string,
  setAppName: (name: string) => void,
  setIsNameGlitching?: (value: boolean) => void,
  durationMs = 650,
  frameMs = 45,
): Promise<void> {
  setIsNameGlitching?.(true);

  return new Promise((resolve) => {
    const startedAt = Date.now();
    const timer = window.setInterval(() => {
      if (Date.now() - startedAt >= durationMs) {
        window.clearInterval(timer);
        setAppName(nextName);
        setIsNameGlitching?.(false);
        resolve();
        return;
      }

      setAppName(makeGlitchedLabel(nextName));
    }, frameMs);
  });
}

export function syncAppTitle(appName: string): void {
  document.title = appName;
  void getCurrentWindow()
    .setTitle(appName)
    .catch(() => {
    });
}

export function startSuperImportantRebranding(
  setAppName: (name: string) => void,
  options?: {
    intervalMs?: number;
    setRebrandCount?: (value: number) => void;
    setIsNameGlitching?: (value: boolean) => void;
  },
): () => void {
  if (!shouldDoSuperImportantLogic()) return () => { };

  const {
    intervalMs = 10_000,
    setRebrandCount,
    setIsNameGlitching,
  } = options ?? {};

  const rotation = shuffle(SUPER_IMPORTANT_NAMES);
  let index = -1;
  let activeName = DEFAULT_APP_NAME;
  let rebrandCount = 0;
  let busy = false;

  setAppName(DEFAULT_APP_NAME);
  setRebrandCount?.(0);

  const timer = window.setInterval(async () => {
    if (busy) return;
    busy = true;

    index = (index + 1) % rotation.length;
    const nextName = rotation[index];
    const messageTemplate = pickRandom(SUPER_IMPORTANT_MESSAGES);

    await toast.promise(
      runGlitchTransition(
        nextName,
        setAppName,
        setIsNameGlitching,
      ).then(() => {
        activeName = nextName;
        rebrandCount += 1;
        setRebrandCount?.(rebrandCount);
      }),
      {
        success: () => pickRandom(SUPER_IMPORTANT_SUCCESSES),
        loading: messageTemplate.replace("{{appName}}", activeName),
      },
    );

    busy = false;
  }, intervalMs);

  return () => window.clearInterval(timer);
}