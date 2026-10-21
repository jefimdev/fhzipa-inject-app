import { useState } from "react";
import { useTranslation } from "react-i18next";
import { openUrl } from "@tauri-apps/plugin-opener";
import {
  FaArrowUpRightFromSquare,
  FaDownload,
  FaFileArrowUp,
  FaStar,
} from "react-icons/fa6";
import type { DownloadProgress, StoreItem } from "../utilitarios/tipos-loja";
import { formatSize } from "../utilitarios/formato";

export const StoreCard = ({
  item,
  progress,
  onDownload,
  onInstall,
  onOpen,
}: {
  item: StoreItem;
  progress?: DownloadProgress;
  onDownload: (item: StoreItem) => void;
  onInstall: (item: StoreItem, path: string) => void;
  onOpen: (item: StoreItem) => void;
}) => {
  const [iconFailed, setIconFailed] = useState(false);
  const { t } = useTranslation();

  const downloading = progress !== undefined && !progress.done;
  const finished = progress?.done === true;
  const percent =
    progress && progress.total > 0
      ? Math.min(100, Math.round((progress.received / progress.total) * 100))
      : null;

  // Once the file is on disk the real size is known, and it is often bigger
  // than the size the source advertises.
  const realSize = finished ? progress!.received : 0;
  const sizeLabel = realSize > 0 ? formatSize(realSize) : item.size;
  const sizeIsReal = realSize > 0 && item.size !== sizeLabel;

  return (
    <article className="store-card">
      <button
        type="button"
        className="store-card-icon"
        onClick={() => onOpen(item)}
        aria-label={item.name}
      >
        {item.icon && !iconFailed ? (
          <img
            src={item.icon}
            alt=""
            loading="lazy"
            onError={() => setIconFailed(true)}
          />
        ) : (
          <FaFileArrowUp aria-hidden="true" />
        )}
      </button>

      <div className="store-card-body">
        <button
          type="button"
          className="store-card-title"
          onClick={() => onOpen(item)}
        >
          {item.name}
        </button>

        <span className="store-card-publisher">
          {item.publisher || item.category || item.sourceId}
        </span>

        <div className="store-card-tags">
          {item.modInfo && (
            <span className="store-tag mod">{item.modInfo}</span>
          )}
          {item.version && (
            <span className="store-tag">v{item.version}</span>
          )}
          {sizeLabel && (
            <span className="store-tag" title={sizeIsReal ? "Tamanho real do arquivo" : undefined}>
              {sizeLabel}
            </span>
          )}
          {item.rating !== null && (
            <span className="store-tag rating">
              <FaStar aria-hidden="true" />
              {item.rating.toFixed(1)}
            </span>
          )}
        </div>

        {downloading && (
          <div className="store-progress">
            <div className="store-progress-bar">
              <span style={{ width: `${percent ?? 8}%` }} />
            </div>
            <span className="store-progress-text">
              {percent !== null ? `${percent}%` : t("store.preparing")} ·{" "}
              {formatSize(progress.received)}
              {progress.mirror ? ` · ${progress.mirror}` : ""}
            </span>
          </div>
        )}

        {finished && progress?.mirror && (
          <span className="store-progress-text">
            {progress.mirror}
            {progress.speed > 0 && ` · ${progress.speed.toFixed(1)} MB/s`}
          </span>
        )}
      </div>

      <div className="store-card-actions">
        {finished && progress?.path ? (
          <button
            type="button"
            className="btn btn-primary btn-sm"
            onClick={() => onInstall(item, progress.path!)}
          >
            {t("store.install")}
          </button>
        ) : item.direct ? (
          <button
            type="button"
            className="btn btn-sm"
            onClick={() => onDownload(item)}
            disabled={downloading}
          >
            <FaDownload aria-hidden="true" />
            {t("store.download")}
          </button>
        ) : (
          // The source only lists the app, the page is where the file is.
          <button
            type="button"
            className="btn btn-sm"
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
    </article>
  );
};
