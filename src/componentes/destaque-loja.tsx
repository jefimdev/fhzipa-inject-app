import { useTranslation } from "react-i18next";
import { openUrl } from "@tauri-apps/plugin-opener";
import { FaArrowUpRightFromSquare, FaDownload, FaFileArrowUp, FaSpinner } from "react-icons/fa6";
import type { DownloadProgress, StoreItem } from "../utilitarios/tipos-loja";
import { formatSize } from "../utilitarios/formato";

export const StoreFeatured = ({
  items,
  progress,
  onDownload,
  onInstall,
  onOpen,
}: {
  items: StoreItem[];
  progress: Record<string, DownloadProgress>;
  onDownload: (item: StoreItem) => void;
  onInstall: (item: StoreItem, path: string) => void;
  onOpen: (item: StoreItem) => void;
}) => {
  const { t } = useTranslation();

  if (items.length === 0) return null;

  return (
    <section className="store-featured">
      <div className="store-featured-head">
        <h2 className="panel-title">{t("featured.title")}</h2>
        <p className="panel-hint">{t("featured.subtitle")}</p>
      </div>

      <div className="store-featured-grid">
        {items.map((item) => {
          const state = progress[item.id];
          const downloading = state !== undefined && !state.done;
          const percent =
            state && state.total > 0
              ? Math.min(100, Math.round((state.received / state.total) * 100))
              : null;

          return (
            <article className="store-featured-card" key={item.id}>
              <div className="store-featured-top">
                <button
                  type="button"
                  className="store-featured-icon"
                  onClick={() => onOpen(item)}
                  aria-label={item.name}
                >
                  {item.icon ? (
                    <img src={item.icon} alt="" loading="lazy" />
                  ) : (
                    <FaFileArrowUp aria-hidden="true" />
                  )}
                </button>

                <div className="store-featured-body">
                  <button
                    type="button"
                    className="store-featured-name"
                    onClick={() => onOpen(item)}
                  >
                    {item.name}
                  </button>
                  <span className="store-featured-meta">
                    {[
                      item.version ? `v${item.version}` : null,
                      item.size,
                      item.modInfo,
                    ]
                      .filter(Boolean)
                      .join(" · ")}
                  </span>
                </div>
              </div>

              <div className="store-featured-actions">
                {downloading ? (
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    disabled
                  >
                    <FaSpinner aria-hidden="true" />
                    {t("store.downloading")}
                  </button>
                ) : state?.done && state.path ? (
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    onClick={() => onInstall(item, state.path!)}
                  >
                    {t("store.install")}
                  </button>
                ) : item.direct ? (
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    onClick={() => onDownload(item)}
                  >
                    <FaDownload aria-hidden="true" />
                    {t("store.download")}
                  </button>
                ) : (
                  // The source only lists the app, the page is where the
                  // file is.
                  <button
                    type="button"
                    className="btn btn-primary btn-sm"
                    onClick={() => openUrl(item.pageUrl)}
                    disabled={!item.pageUrl}
                  >
                    <FaArrowUpRightFromSquare aria-hidden="true" />
                    {t("store.view_source")}
                  </button>
                )}
                {item.pageUrl && item.direct && (
                  <button
                    type="button"
                    className="icon-button"
                    title={t("store.open_page")}
                    onClick={() => openUrl(item.pageUrl)}
                  >
                    <FaArrowUpRightFromSquare />
                  </button>
                )}
              </div>

              {downloading && state && (
                <div className="store-progress">
                  <div className="store-progress-bar">
                    <span
                      style={{
                        width: `${percent ?? 8}%`,
                      }}
                    />
                  </div>
                  <span className="store-progress-text">
                    {percent !== null
                      ? `${percent}%`
                      : t("store.preparing")}{" "}
                    · {formatSize(state.received)}
                    {state.mirror ? ` · ${state.mirror}` : ""}
                  </span>
                </div>
              )}
            </article>
          );
        })}
      </div>
    </section>
  );
};
