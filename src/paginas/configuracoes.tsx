import "./configuracoes.css";
import { useStore } from "../contextos/loja";
import { useEffect, useState } from "react";
import { LogLevel, useLogs } from "../contextos/log";
import { Modal } from "../componentes/modal";
import { Dropdown } from "../componentes/dropdown";
import { toast } from "sonner";
import { invoke } from "@tauri-apps/api/core";
import { useError } from "../contextos/erro";
import { Virtuoso } from "react-virtuoso";
import { useDialog } from "../contextos/dialogo";
import { Trans, useTranslation } from "react-i18next";
import i18n, { sortedLanguages } from "../configuracoes/i18n";
import { openUrl } from "@tauri-apps/plugin-opener";
import { DeviceInfo } from "../modulos/dispositivo";
import { TRANSLATE_URL } from "../configuracoes/links";
import {
  ANISETTE_SERVERS,
  DEFAULT_ANISETTE_SERVER,
  isDeprecatedAnisetteServer,
} from "../configuracoes/servidores-anisette";
import {
  FaArrowsRotate,
  FaClipboardList,
  FaKey,
  FaTrashCan,
} from "react-icons/fa6";

type SettingsProps = {
  ensureSelectedDevice: () => boolean;
  setSelectedDevice: (device: DeviceInfo | null) => void;
  platform: string;
  shortcutLabel: (mac: string, windows: string, linux?: string) => string;
  checkKeyring: () => Promise<void>;
};

export const Preferences = ({
  ensureSelectedDevice,
  setSelectedDevice,
  shortcutLabel,
  checkKeyring,
}: SettingsProps) => {
  const { t } = useTranslation();
  const [anisetteServer, setAnisetteServer] = useStore<string>(
    "newAnisetteServer",
    DEFAULT_ANISETTE_SERVER,
  );

  const [overrideKeyring, setOverrideKeyring] = useStore<boolean>(
    "overrideKeyring",
    false,
  );
  const [logsOpen, setLogsOpen] = useState(false);
  const [logLevelFilter, setLogLevelFilter] = useState("3");
  const logs = useLogs();
  const { err } = useError();
  const { confirm } = useDialog();

  const anisetteOptions = ANISETTE_SERVERS.map(({ value, label }) => ({
    value,
    label,
  }));
  const logLevelOptions = [
    { value: String(LogLevel.Debug), label: t("settings.debug") },
    { value: String(LogLevel.Info), label: t("settings.info") },
    { value: String(LogLevel.Warn), label: t("settings.warn") },
    { value: String(LogLevel.Error), label: t("settings.error") },
  ];
  const filteredLogs = logs.filter((log) => {
    return log.level >= Number(logLevelFilter);
  });

  const [lang, setLang] = useStore<string>("lang", "pt_br");

  useEffect(() => {
    i18n.changeLanguage(lang);
  }, [lang]);

  useEffect(() => {
    (async () => {
      await invoke("force_disable_keyring", { force: overrideKeyring });
      checkKeyring();
    })();
  }, [overrideKeyring]);

  // A preference stored by an earlier build may point at a server that is no
  // longer offered. Reset it instead of carrying it over.
  useEffect(() => {
    if (isDeprecatedAnisetteServer(anisetteServer)) {
      setAnisetteServer(DEFAULT_ANISETTE_SERVER);
    }
  }, [anisetteServer, setAnisetteServer]);

  return (
    <>
      <div className="prefs-grid">
        <div className="pref">
          <Dropdown
            label={t("prefs.anisette")}
            labelId="anisette-label"
            options={anisetteOptions}
            value={anisetteServer}
            onChange={setAnisetteServer}
            allowCustom
            defaultCustomValue="ani.yourserver.com"
            customPlaceholder={t("settings.custom_anisette_placeholder")}
            customLabel={t("settings.custom_anisette")}
            customToggleLabel={t("settings.use_custom_anisette")}
            presetToggleLabel={t("settings.back_preset_servers")}
          />
        </div>

        <div className="pref">
          <Dropdown
            label={t("prefs.language")}
            labelId="language"
            options={sortedLanguages.map(([value, label]) => ({
              value,
              label,
            }))}
            value={lang}
            onChange={setLang}
          />
          <p className="settings-hint">
            <Trans
              i18nKey="settings.language_hint"
              components={{
                translation: (
                  <span
                    onClick={() => openUrl(TRANSLATE_URL)}
                    role="link"
                    className="error-link"
                  />
                ),
              }}
            />
          </p>
        </div>

        <div className="pref pref-inline">
          <span className="pref-icon" aria-hidden="true">
            <FaKey />
          </span>
          <div className="pref-inline-text">
            <span className="pref-label">{t("prefs.keyring")}</span>
            <span className="pref-hint">{t("prefs.keyring_hint")}</span>
          </div>
          <button
            type="button"
            role="switch"
            aria-checked={!overrideKeyring}
            aria-label={t("prefs.keyring")}
            className="switch"
            onClick={() => setOverrideKeyring(!overrideKeyring)}
          >
            <span className="switch-knob" />
          </button>
        </div>
      </div>

      <div className="pref-actions">
        <button
          type="button"
          className="btn btn-sm btn-ghost"
          onClick={() =>
            confirm(
              t("settings.reset_anisette_title"),
              t("settings.reset_anisette_message"),
              () =>
                toast.promise(invoke("reset_anisette_state"), {
                  loading: t("settings.resetting_anisette_state"),
                  success: (didReset) =>
                    didReset
                      ? t("settings.anisette_state_reset_success")
                      : t("settings.anisette_state_not_found"),
                  error: (e) =>
                    err(t("settings.failed_reset_anisette_state"), e),
                }),
            )
          }
        >
          <FaArrowsRotate aria-hidden="true" />
          {t("settings.reset_anisette_title")}
        </button>

        <button
          type="button"
          className="btn btn-sm btn-danger"
          onClick={() => {
            if (!ensureSelectedDevice()) return;
            confirm(
              t("settings.delete_stored_rppairing"),
              t("settings.delete_stored_rppairing_message"),
              () =>
                toast.promise(
                  async () => {
                    await invoke("delete_stored_rppairing");
                    await invoke("set_selected_device");
                    setSelectedDevice(null);
                  },
                  {
                    loading: t("settings.deleting_stored_rppairing"),
                    success: t("settings.stored_rppairing_deleted_success"),
                    error: (e) =>
                      err(t("settings.failed_delete_stored_rppairing"), e),
                  },
                ),
            );
          }}
        >
          <FaTrashCan aria-hidden="true" />
          {t("settings.delete_stored_rppairing")}
        </button>

        <button
          type="button"
          className="btn btn-sm btn-ghost"
          onClick={() => setLogsOpen(true)}
        >
          <FaClipboardList aria-hidden="true" />
          {t("settings.view_logs")}
          <span className="text-muted">
            {` (${shortcutLabel("⌘L", "Ctrl+L")})`}
          </span>
        </button>
      </div>

      <Modal isOpen={logsOpen} close={() => setLogsOpen(false)} zIndex={9999999999}>
        <div className="log-outer">
          <div className="log-header">
            <h2>{t("settings.logs")}</h2>
            <button
              className="btn btn-sm"
              onClick={() => {
                const logText = filteredLogs
                  .map(
                    (log) =>
                      `[${log.timestamp}] [${LogLevel[log.level]}] ${log.target ? `<${log.target}>` : ""} ${log.message}`,
                  )
                  .join("\n");
                navigator.clipboard.writeText("```\n" + logText + "\n```");
                toast.success(t("common.copied_success"));
              }}
            >
              {t("common.copy_to_clipboard")}
            </button>
          </div>
          <Dropdown
            label={t("settings.log_level")}
            labelId="log-level-label"
            options={logLevelOptions}
            value={logLevelFilter}
            onChange={setLogLevelFilter}
          />
          {filteredLogs.length > 0 ? (
            <Virtuoso
              className="log-inner"
              data={filteredLogs}
              followOutput="smooth"
              initialTopMostItemIndex={filteredLogs.length - 1}
              itemContent={(_index, log) => (
                <div className="log-entry">
                  <span style={{ color: "gray" }}>[{log.timestamp}]</span>{" "}
                  {getHtmlForLevel(log.level)}{" "}
                  {log.target ? (
                    <span style={{ color: "#aaa" }}>{log.target}</span>
                  ) : (
                    ""
                  )}{" "}
                  {log.message}
                </div>
              )}
            />
          ) : (
            <pre className="log-inner">
              <div className="log-entry">{t("settings.no_logs_yet")}</div>
            </pre>
          )}
        </div>
      </Modal>
    </>
  );
};

// convert level to a properly colored html string
function getHtmlForLevel(level: LogLevel) {
  switch (level) {
    case LogLevel.Trace:
      return <span style={{ color: "purple" }}>[TRACE]</span>;
    case LogLevel.Debug:
      return <span style={{ color: "blue" }}>[DEBUG]</span>;
    case LogLevel.Info:
      return <span style={{ color: "green" }}>[INFO]</span>;
    case LogLevel.Warn:
      return <span style={{ color: "orange" }}>[WARN]</span>;
    case LogLevel.Error:
      return <span style={{ color: "red" }}>[ERROR]</span>;
    default:
      return <span>[UNKNOWN]</span>;
  }
}
