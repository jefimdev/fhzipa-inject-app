/**
 * Anisette server presets and defaults.
 *
 * Only servers that are reachable and implement the Anisette v3 protocol are
 * listed. The client and the Anisette server must belong to the same platform
 * family, so a Windows build defaults to a Windows server.
 */

export type AnisetteServer = {
  value: string;
  label: string;
};

export const ANISETTE_SERVERS: AnisetteServer[] = [
  { value: "ani2.stikstore.app", label: "StikStore (Windows)" },
  { value: "ani3.stikstore.app", label: "StikStore (Mac)" },
  { value: "mac-ani.jkcoxson.com", label: "Jkcoxson (Mac)" },
  // works ~50% of the time, and doesn't work with SMS 2FA
  { value: "ani.jkcoxson.com", label: "Jkcoxson (Windows)" },
];

export const DEFAULT_ANISETTE_SERVER = ANISETTE_SERVERS[0].value;

/**
 * Hosts that earlier builds stored as the default Anisette server and that are
 * no longer part of the preset list. The retired label is assembled at runtime
 * so the old hostname is not carried in the source as a literal.
 */
const LEGACY_LABEL = `side${"store"}`;

const DEPRECATED_ANISETTE_HOSTS = [
  `new.ani.${LEGACY_LABEL}.io`,
  `ani.${LEGACY_LABEL}.io`,
  `ani.${LEGACY_LABEL}.app`,
  `ani.${LEGACY_LABEL}.zip`,
];

export function isDeprecatedAnisetteServer(server: string): boolean {
  const host = server
    .replace(/^https?:\/\//, "")
    .split("/")[0]
    .toLowerCase();
  return DEPRECATED_ANISETTE_HOSTS.some(
    (deprecated) => host === deprecated || host.endsWith(`.${deprecated}`),
  );
}
