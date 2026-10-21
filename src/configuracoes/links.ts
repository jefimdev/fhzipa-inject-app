/**
 * Centralised project links.
 *
 * Every outbound link used by the UI goes through this module so that the
 * project can be re-pointed at a different repository without touching
 * component code. Override any value at build time through a `.env` file:
 *
 *   VITE_REPO_URL=https://github.com/<owner>/<repo>
 *   VITE_ISSUES_URL=https://github.com/<owner>/<repo>/issues
 *   VITE_SPONSOR_URL=https://github.com/sponsors/<owner>
 *   VITE_DISCORD_URL=https://discord.gg/<invite>
 *   VITE_TRANSLATE_URL=https://github.com/<owner>/<repo>#translations
 */
const env = import.meta.env;

export const REPO_URL: string =
  env.VITE_REPO_URL ?? "https://github.com/jefimdev";

export const ISSUES_URL: string = env.VITE_ISSUES_URL ?? `${REPO_URL}/issues`;

export const SPONSOR_URL: string =
  env.VITE_SPONSOR_URL ?? "https://github.com/sponsors/jefimdev";

export const DISCORD_URL: string =
  env.VITE_DISCORD_URL ?? "https://discord.gg/EA6yVgydBz";

export const TRANSLATE_URL: string =
  env.VITE_TRANSLATE_URL ?? `${REPO_URL}#translations`;
