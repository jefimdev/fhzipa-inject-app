export type StoreSource = {
  id: string;
  name: string;
  homepage: string;
  kind: string;
  /** Whether the entries come with a direct link to the `.ipa` file. */
  direct: boolean;
};

/** Another host that serves the same file, used to pick the fastest one. */
export type StoreMirror = {
  url: string;
  name: string;
};

export type StoreCategory = {
  id: string;
  name: string;
  count: number;
};

export type StoreItem = {
  id: string;
  sourceId: string;
  name: string;
  publisher: string;
  subtitle: string;
  description: string;
  version: string;
  size: string;
  modInfo: string;
  bundleId: string;
  icon: string | null;
  rating: number | null;
  votes: number | null;
  downloadUrl: string;
  pageUrl: string;
  /** Whether `downloadUrl` points straight at the `.ipa` file. */
  direct: boolean;
  category: string;
  categories: string[];
  updated: string;
  screenshots: string[];
  mirrors: StoreMirror[];
  /** The digest of the file, when the source publishes one. */
  digest: string | null;
};

export type CatalogPage = {
  items: StoreItem[];
  total: number;
  page: number;
  totalPages: number;
  sourceId: string;
};

export type DownloadProgress = {
  /** The catalog item the download belongs to. */
  itemId: string | null;
  downloadUrl: string;
  received: number;
  total: number;
  done: boolean;
  path: string | null;
  /** The mirror that served the file, when it is not the requested one. */
  mirror: string | null;
  /** The measured speed, in megabytes per second. */
  speed: number;
};

export type DownloadResult = {
  itemId: string | null;
  path: string;
  /** The real size of the file on disk, in bytes. */
  size: number;
  /** The mirror the file came from. */
  mirror: string | null;
  /** The measured speed, in megabytes per second. */
  speed: number;
};
