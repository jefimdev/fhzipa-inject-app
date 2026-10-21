import { useState } from "react";
import { openUrl } from "@tauri-apps/plugin-opener";
import { Modal } from "./modal";
import {
  FaArrowUpRightFromSquare,
  FaDownload,
  FaFileArrowUp,
  FaStar,
} from "react-icons/fa6";
import type { DownloadProgress, StoreItem } from "../utilitarios/tipos-loja";
import { formatSize } from "../utilitarios/formato";

export const StoreDetail = ({
  item,
  progress,
  onClose,
  onDownload,
  onInstall,
}: {
  item: StoreItem | null;
  progress?: DownloadProgress;
  onClose: () => void;
  onDownload: (item: StoreItem) => void;
  onInstall: (item: StoreItem, path: string) => void;
}) => {
  const [iconFailed, setIconFailed] = useState(false);

  if (!item) return null;

  const downloading = progress !== undefined && !progress.done;
  const finished = progress?.done === true;
  const realSize = finished ? progress!.received : 0;
  const declaredSize = item.size;
  const differs = realSize > 0 && formatSize(realSize) !== declaredSize;

  return (
    <Modal isOpen close={onClose} sizeFit>      <div className="store-detail">
        <div className="store-detail-head">
          <span className="store-detail-icon">
            {item.icon && !iconFailed ? (
              <img
                src={item.icon}
                alt=""
                onError={() => setIconFailed(true)}
              />
            ) : (
              <FaFileArrowUp aria-hidden="true" />
            )}
          </span>
          <div>
            <h2 className="store-detail-title">{item.name}</h2>
            <p className="store-detail-publisher">
              {item.publisher || "—"}
              {item.version ? ` · v${item.version}` : ""}
              {item.size ? ` · ${item.size}` : ""}
            </p>
          </div>
        </div>

        {item.screenshots.length > 0 && (
          <div className="store-detail-shots">
            {item.screenshots.slice(0, 4).map((shot) => (
              <img key={shot} src={shot} alt="" loading="lazy" />
            ))}
          </div>
        )}

        <div className="store-detail-tags">
          {item.modInfo && <span className="store-tag mod">{item.modInfo}</span>}
          {item.category && (
            <span className="store-tag">{item.category}</span>
          )}
          {item.rating !== null && (
            <span className="store-tag rating">
              <FaStar aria-hidden="true" />
              {item.rating.toFixed(1)}
              {item.votes ? ` (${item.votes})` : ""}
            </span>
          )}
          {item.bundleId && (
            <span className="store-tag mono">{item.bundleId}</span>
          )}
        </div>

        {(declaredSize || realSize > 0) && (
          <div className="store-detail-sizes">
            {declaredSize && (
              <span>
                <em>Tamanho informado:</em> {declaredSize}
              </span>
            )}
            {realSize > 0 && (
              <span>
                <em>Tamanho real do arquivo:</em> {formatSize(realSize)}
                {differs && (
                  <b title="A fonte informa um tamanho diferente do arquivo">
                    {declaredSize && "≠"}
                  </b>
                )}
              </span>
            )}
          </div>
        )}

        {item.description && (
          <p className="store-detail-description">{item.description}</p>
        )}

        <div className="store-detail-actions">
          {finished && progress?.path ? (
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => onInstall(item, progress.path!)}
            >
              Instalar no dispositivo
            </button>
          ) : item.direct ? (
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => onDownload(item)}
              disabled={downloading}
            >
              <FaDownload aria-hidden="true" />
              {downloading ? "Baixando…" : "Baixar IPA"}
            </button>
          ) : (
            // The source only lists the app, the page is where the file is.
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => openUrl(item.pageUrl)}
              disabled={!item.pageUrl}
            >
              <FaArrowUpRightFromSquare aria-hidden="true" />
              Ver na fonte
            </button>
          )}

          {item.pageUrl && item.direct && (
            <button
              type="button"
              className="btn btn-ghost"
              onClick={() => openUrl(item.pageUrl)}
            >
              <FaArrowUpRightFromSquare aria-hidden="true" />
              Ver na fonte
            </button>
          )}
        </div>
      </div>
    </Modal>
  );
};
