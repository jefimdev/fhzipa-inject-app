import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { open as openFileDialog } from "@tauri-apps/plugin-dialog";
import { invoke } from "@tauri-apps/api/core";
import { getCurrentWebview } from "@tauri-apps/api/webview";
import { toast } from "sonner";
import { FaFileArrowUp, FaFolderOpen, FaTrashCan } from "react-icons/fa6";
import { Panel } from "./painel";
import { formatSize } from "../utilitarios/formato";

export type IpaInfo = {
  displayName: string | null;
  bundleId: string | null;
  version: string | null;
};

export type IpaSelection = {
  path: string;
  name: string;
  size: number;
  info: IpaInfo | null;
};

const fileNameOf = (path: string) => {
  const parts = path.split(/[\\/]/);
  return parts[parts.length - 1] || path;
};

export const ImportPanel = ({
  selection,
  onSelect,
  onClear,
  onInstall,
  canInstall,
}: {
  selection: IpaSelection | null;
  onSelect: (selection: IpaSelection) => void;
  onClear: () => void;
  onInstall: () => void;
  canInstall: boolean;
}) => {
  const { t } = useTranslation();
  const [dragging, setDragging] = useState(false);
  const dropRef = useRef<HTMLDivElement | null>(null);

  const accept = async (path: string) => {
    if (!path.toLowerCase().endsWith(".ipa")) {
      toast.error(t("import.invalid"));
      return;
    }

    let size = 0;
    let info: IpaInfo | null = null;
    try {
      const meta = await invoke<{ size: number; info: IpaInfo }>(
        "read_ipa_info",
        { appPath: path },
      );
      size = meta.size;
      info = meta.info;
    } catch {
      // The metadata is a nice to have, the import still works without it.
    }

    onSelect({ path, name: fileNameOf(path), size, info });
  };

  useEffect(() => {
    let unlisten: (() => void) | undefined;
    let cancelled = false;

    getCurrentWebview()
      .onDragDropEvent((event) => {
        const payload = event.payload;
        if (payload.type === "enter") {
          setDragging(true);
        } else if (payload.type === "leave") {
          setDragging(false);
        } else if (payload.type === "drop") {
          setDragging(false);
          const path = payload.paths?.[0];
          if (path) {
            accept(path);
          }
        }
      })
      .then((fn) => {
        if (cancelled) fn();
        else unlisten = fn;
      })
      .catch(() => {});

    return () => {
      cancelled = true;
      unlisten?.();
    };
  }, []);

  const pick = async () => {
    const path = await openFileDialog({
      multiple: false,
      filters: [{ name: t("app.ipa_files"), extensions: ["ipa"] }],
    });
    if (!path) return;
    await accept(path as string);
  };

  return (
    <Panel className="import-panel">
      <div
        ref={dropRef}
        className={`dropzone${dragging ? " dragging" : ""}`}
        onDragOver={(event) => {
          event.preventDefault();
          setDragging(true);
        }}
        onDragLeave={() => setDragging(false)}
        onDrop={(event) => {
          event.preventDefault();
          setDragging(false);
        }}
      >
        <span className="ipa-glyph" aria-hidden="true">
          <FaFileArrowUp />
          <span className="ipa-glyph-label">IPA</span>
        </span>

        <div className="dropzone-text">
          <h2 className="panel-title">{t("import.title")}</h2>
          {selection ? (
            <>
              <p className="panel-subtitle">{selection.name}</p>
              <p className="panel-hint">
                {[
                  selection.size ? formatSize(selection.size) : null,
                  selection.info?.displayName ??
                    selection.info?.bundleId ??
                    null,
                  selection.info?.version
                    ? `${t("import.version")} ${selection.info.version}`
                    : null,
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
            </>
          ) : (
            <p className="panel-subtitle">
              {dragging ? t("import.drop_active") : t("import.subtitle")}
            </p>
          )}
        </div>

        <div className="dropzone-actions">
          {selection ? (
            <>
              <button
                type="button"
                className="btn btn-primary"
                onClick={onInstall}
                disabled={!canInstall}
              >
                {t("import.install")}
              </button>
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                onClick={onClear}
              >
                <FaTrashCan aria-hidden="true" />
                {t("import.remove")}
              </button>
            </>
          ) : (
            <button type="button" className="btn btn-primary" onClick={pick}>
              <FaFolderOpen aria-hidden="true" />
              {t("import.button")}
            </button>
          )}
        </div>
      </div>
    </Panel>
  );
};
