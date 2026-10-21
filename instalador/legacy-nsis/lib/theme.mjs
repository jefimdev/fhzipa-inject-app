/**
 * The colour palette of the installer.
 *
 * It mirrors the tokens of the application, so the installer looks like a
 * piece of the product and not a stranger next to it.
 */
export const theme = {
  bg: "#080b10",
  bgElevated: "#0d1117",
  panel: "#11171f",
  panelAlt: "#0f151c",
  surface: "#18212b",
  surfaceHover: "#1d2833",
  border: "#2a3542",
  borderStrong: "#384556",
  text: "#f5f7fa",
  textMuted: "#a6b2c2",
  textDim: "#788596",
  red: "#ff2343",
  redPress: "#d90d30",
  green: "#13d990",
  amber: "#f5b942",
};

/** The colours as numbers, for the bitmap writer. */
export const rgb = (hex) => {
  const value = hex.replace("#", "");
  return [
    parseInt(value.slice(0, 2), 16),
    parseInt(value.slice(2, 4), 16),
    parseInt(value.slice(4, 6), 16),
  ];
};

/** Mixes two colours, the first one weighted. */
export const mix = (a, b, weight) => [
  Math.round(a[0] * weight + b[0] * (1 - weight)),
  Math.round(a[1] * weight + b[1] * (1 - weight)),
  Math.round(a[2] * weight + b[2] * (1 - weight)),
];
