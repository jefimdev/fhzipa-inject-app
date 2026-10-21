/**
 * The artwork of the installer.
 *
 * The portrait that the welcome page shows on the side carries the icon of
 * the application on the red halo of the brand.
 */
import { readFileSync } from "node:fs";
import { Bitmap } from "./bitmap.mjs";
import { mix, rgb, theme } from "./theme.mjs";
import { decodePng } from "./png.mjs";

/** The size of the portrait of the welcome page. */
export const PORTRAIT_W = 164;
export const PORTRAIT_H = 314;

/** Reads the icon of the application. */
export const readIcon = (path) =>
  decodePng(readFileSync(path));

/**
 * The portrait of the welcome page: the icon of the application on the red
 * halo of the brand.
 */
export const buildPortrait = (icon) => {
  const bitmap = new Bitmap(PORTRAIT_W, PORTRAIT_H, rgb(theme.bgElevated));

  bitmap.gradientV(0, 0, PORTRAIT_W, PORTRAIT_H, rgb(theme.panel), rgb(theme.bg));
  bitmap.glow(PORTRAIT_W / 2, 96, 150, rgb(theme.red), 0.34);

  // The icon, in the middle.
  const size = 116;
  bitmap.drawImage(icon, (PORTRAIT_W - size) / 2, 38, size, size);

  // The red line of the brand under the icon.
  bitmap.roundRect(PORTRAIT_W / 2 - 26, 176, 52, 4, 2, rgb(theme.red));

  return bitmap;
};
