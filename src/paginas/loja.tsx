import { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { openUrl } from "@tauri-apps/plugin-opener";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
  FaArrowDownAZ,
  FaMagnifyingGlass,
  FaSort,
  FaStore,
} from "react-icons/fa6";
import { StoreCard } from "../componentes/cartao-loja";
import { StoreDetail } from "../componentes/detalhe-loja";
import { StoreFeatured } from "../componentes/destaque-loja";
import { FilterMenu } from "../componentes/menu-filtro";
import type {
  CatalogPage,
  DownloadProgress,
  DownloadResult,
  StoreCategory,
  StoreItem,
  StoreSource,
} from "../utilitarios/tipos-loja";
import "./loja.css";

const PER_PAGE = 24;

type SortId = "newest" | "oldest" | "az" | "za";

const SORTS: { id: SortId; orderBy: string; order: string }[] = [
  { id: "newest", orderBy: "date", order: "desc" },
  { id: "oldest", orderBy: "date", order: "asc" },
  { id: "az", orderBy: "title", order: "asc" },
  { id: "za", orderBy: "title", order: "desc" },
];

/** The mod features the catalog advertises, grouped into buckets. */
const FEATURE_FILTERS: { id: string; match: string[] }[] = [
  {
    id: "premium",
    match: [
      "unlocked",
      "premium",
      "subscription",
      "features unlocked",
      "full game unlocked",
      "modified features",
      "pro unlocked",
    ],
  },
  {
    id: "noads",
    match: ["ads no", "no ads", "ads removed", "without ads", "no advertisement"],
  },
  { id: "emulator", match: ["emulator"] },
  {
    id: "gameplay",
    match: [
      "god mode",
      "damage",
      "never die",
      "unlimited",
      "currency",
      "coins",
      "gems",
      "debug menu",
      "dev tool",
      "free store",
      "multiplier",
    ],
  },
];

const matchesFeature = (modInfo: string, filterId: string) => {
  const filter = FEATURE_FILTERS.find((entry) => entry.id === filterId);
  if (!filter) return true;
  const haystack = modInfo.toLowerCase();
  return filter.match.some((needle) => haystack.includes(needle));
};

export const Store = ({
  onDownloaded,
  onInstall,
}: {
  onDownloaded: (path: string, item: StoreItem, size: number) => void;
  onInstall: (path: string) => void;
}) => {
  const { t } = useTranslation();
  const [sources, setSources] = useState<StoreSource[]>([]);
  const [sourceId, setSourceId] = useState("ipaomtk");
  const [categories, setCategories] = useState<StoreCategory[]>([]);
  const [category, setCategory] = useState<string | null>(null);
  const [feature, setFeature] = useState<string | null>(null);
  const [sort, setSort] = useState<SortId>("newest");
  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [page, setPage] = useState(1);
  const [items, setItems] = useState<StoreItem[]>([]);
  const [featured, setFeatured] = useState<StoreItem[]>([]);
  const [total, setTotal] = useState(0);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [progress, setProgress] = useState<Record<string, DownloadProgress>>({});
  const [detail, setDetail] = useState<StoreItem | null>(null);

  const searchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    invoke<StoreSource[]>("store_sources")
      .then(setSources)
      .catch(() => setSources([]));
  }, []);

  useEffect(() => {
    const unlisten = listen<DownloadProgress>(
      "store-download-progress",
      (event) => {
        const payload = event.payload;
        // The progress belongs to the catalog item, the mirror can change the
        // url that is being downloaded.
        const key = payload.itemId ?? payload.downloadUrl;
        setProgress((current) => ({
          ...current,
          [key]: payload,
        }));
      },
    );
    return () => {
      unlisten.then((fn) => fn());
    };
  }, []);

  useEffect(() => {
    invoke<StoreCategory[]>("store_categories", { sourceId })
      .then((list) => {
        setCategories(list);
        setCategory(null);
      })
      .catch((e) => setError(String(e)));
  }, [sourceId]);

  useEffect(() => {
    invoke<StoreItem[]>("store_featured", { sourceId })
      .then(setFeatured)
      .catch(() => setFeatured([]));
  }, [sourceId]);

  const currentSort = SORTS.find((entry) => entry.id === sort) ?? SORTS[0];

  // Only the WordPress catalogues can be put in order, the others bring
  // their own order.
  const sourceKind = sources.find((entry) => entry.id === sourceId)?.kind;
  const sortable = sourceKind === "wordpress";
  // The archives of GitHub do not say what was changed.
  const showFeatures = sourceKind === "wordpress" || sourceKind === "iappstores";

  const load = useCallback(
    async (targetPage: number, append: boolean) => {
      setLoading(true);
      setError(null);
      try {
        const result = await invoke<CatalogPage>("store_page", {
          sourceId,
          category,
          search: search || null,
          orderBy: currentSort.orderBy,
          order: currentSort.order,
          page: targetPage,
          perPage: PER_PAGE,
        });
        setItems((current) =>
          append ? [...current, ...result.items] : result.items,
        );
        setTotal(result.total);
        setTotalPages(result.totalPages);
        setPage(result.page);
      } catch (e) {
        setError(String((e as { message?: string })?.message ?? e));
      } finally {
        setLoading(false);
      }
    },
    [sourceId, category, search, currentSort.orderBy, currentSort.order],
  );

  useEffect(() => {
    void load(1, false);
  }, [load]);

  const onSearchChange = (value: string) => {
    setSearchInput(value);
    if (searchTimer.current) clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => setSearch(value.trim()), 350);
  };

  const download = async (item: StoreItem) => {
    // Some sources only list the app, the page is where the file is.
    if (!item.direct) {
      if (item.pageUrl) await openUrl(item.pageUrl);
      return;
    }

    if (!item.downloadUrl) {
      toast.error(t("store.no_direct"));
      return;
    }

    // Immediate feedback, the mirror is measured before the transfer starts.
    setProgress((current) => ({
      ...current,
      [item.id]: {
        itemId: item.id,
        downloadUrl: item.downloadUrl,
        received: 0,
        total: 0,
        done: false,
        path: null,
        mirror: null,
        speed: 0,
      },
    }));

    try {
      const result = await invoke<DownloadResult>("store_download", {
        downloadUrl: item.downloadUrl,
        fileName: item.name,
        appName: item.name,
        bundleId: item.bundleId || null,
        appVersion: item.version || null,
        itemId: item.id,
        sourceId: item.sourceId,
        mirrors: item.mirrors,
        digest: item.digest,
      });
      toast.success(`${item.name} baixado.`);
      if (result.mirror) {
        toast.message(
          `Mirror mais rápido: ${result.mirror} · ${result.speed.toFixed(1)} MB/s`,
        );
      }
      onDownloaded(result.path, item, result.size);
    } catch (e) {
      setProgress((current) => {
        const next = { ...current };
        delete next[item.id];
        return next;
      });
      toast.error(String((e as { message?: string })?.message ?? e));
    }
  };

  const install = (_item: StoreItem, path: string) => {    onInstall(path);
    setDetail(null);
  };

  const visibleCategories = useMemo(
    () => categories.filter((entry) => entry.count > 0).slice(0, 40),
    [categories],
  );

  const visibleItems = useMemo(
    () =>
      feature
        ? items.filter((item) => matchesFeature(item.modInfo, feature))
        : items,
    [items, feature],
  );

  return (
    <>
      <div className="panel store-toolbar">
        <div className="store-search">
          <span className="field-icon" aria-hidden="true">
            <FaMagnifyingGlass />
          </span>
          <input
            className="input"
            type="search"
            value={searchInput}
            onChange={(event) => onSearchChange(event.target.value)}
            placeholder={t("store.search_placeholder")}
            aria-label={t("store.search_placeholder")}
          />
        </div>

        {sources.length > 1 && (
          <div className="store-sources">
            {sources.map((source) => (
              <button
                key={source.id}
                type="button"
                className={`store-source${source.id === sourceId ? " active" : ""}`}
                onClick={() => {
                  setSourceId(source.id);
                  setItems([]);
                  // The filter of the mods does not apply to every source.
                  if (source.kind !== "wordpress" && source.kind !== "iappstores") {
                    setFeature(null);
                  }
                }}
              >
                {source.name}
              </button>
            ))}
          </div>
        )}

        {sortable && (
          <div className="store-sorts">
            <FaSort aria-hidden="true" />
            {SORTS.map((entry) => (
              <button
                key={entry.id}
                type="button"
                className={`store-sort${entry.id === sort ? " active" : ""}`}
                onClick={() => setSort(entry.id)}
              >
                {entry.id === "az" || entry.id === "za" ? (
                  <FaArrowDownAZ aria-hidden="true" />
                ) : null}
                {t(`sort.${entry.id}`)}
              </button>
            ))}
          </div>
        )}

        <FilterMenu
          categories={visibleCategories}
          category={category}
          onCategory={setCategory}
          feature={feature}
          onFeature={setFeature}
          features={FEATURE_FILTERS}
          showFeatures={showFeatures}
        />
      </div>

      <StoreFeatured
        items={featured}
        progress={progress}
        onDownload={download}
        onInstall={install}
        onOpen={setDetail}
      />

      {error && (
        <div className="panel">
          <p className="store-error">{error}</p>
        </div>
      )}

      {visibleItems.length === 0 && !loading && !error && (
        <div className="panel store-empty">
          <FaStore aria-hidden="true" />
          <p>{t("store.empty")}</p>
        </div>
      )}

      <div className="store-grid">
        {visibleItems.map((item) => (
          <StoreCard
            key={item.id}
            item={item}
            progress={progress[item.id]}
            onDownload={download}
            onInstall={install}
            onOpen={setDetail}
          />
        ))}
      </div>

      {loading && (
        <div className="store-loading">
          <div className="button-spinner" aria-hidden="true" />
          {t("store.loading")}
        </div>
      )}

      {!loading && visibleItems.length > 0 && (
        <div className="store-more">
          <span className="store-count">
            {t("store.count", {
              shown: visibleItems.length,
              total: total.toLocaleString("pt-BR"),
            })}
          </span>
          {page < totalPages && (
            <button
              type="button"
              className="btn"
              onClick={() => load(page + 1, true)}
            >
              {t("store.load_more")}
            </button>
          )}
        </div>
      )}

      <StoreDetail
        item={detail}
        progress={detail ? progress[detail.id] : undefined}
        onClose={() => setDetail(null)}
        onDownload={download}
        onInstall={install}
      />
    </>
  );
};
