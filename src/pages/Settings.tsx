import "./Settings.css";
import { useStore } from "../StoreContext";
import { useState } from "react";
import { LogLevel, useLogs } from "../LogContext";
import { Modal } from "../components/Modal";
import { Dropdown } from "../components/Dropdown";
import { toast } from "sonner";
import { invoke } from "@tauri-apps/api/core";
import { useError } from "../ErrorContext";
import { Virtuoso } from "react-virtuoso";
import { useDialog } from "../DialogContext";

type SettingsProps = {
  showHeading?: boolean;
};

let anisetteServers = [
  ["ani.sidestore.io", "SideStore (.io)"],
  ["ani.stikstore.app", "StikStore"],
  ["ani.sidestore.app", "SideStore (.app)"],
  ["ani.sidestore.zip", "SideStore (.zip)"],
  ["ani.846969.xyz", "SideStore (.xyz)"],
  ["ani.neoarz.xyz", "neoarz"],
  ["ani.xu30.top", "SteX"],
  ["anisette.wedotstud.io", "WE. Studio"],
];
export const Settings = ({ showHeading = true }: SettingsProps) => {
  const [anisetteServer, setAnisetteServer] = useStore<string>(
    "anisetteServer",
    "ani.sidestore.io",
  );

  const [logsOpen, setLogsOpen] = useState(false);
  const [logLevelFilter, setLogLevelFilter] = useState("3");
  const logs = useLogs();
  const { err } = useError();
  const { confirm } = useDialog();

  const anisetteOptions = anisetteServers.map(([value, label]) => ({
    value,
    label,
  }));
  const logLevelOptions = [
    // { value: String(LogLevel.Trace), label: "Trace" },
    { value: String(LogLevel.Debug), label: "Debug" },
    { value: String(LogLevel.Info), label: "Info" },
    { value: String(LogLevel.Warn), label: "Warn" },
    { value: String(LogLevel.Error), label: "Error" },
  ];
  const filteredLogs = logs.filter((log) => {
    return log.level >= Number(logLevelFilter);
  });

  return (
    <>
      {showHeading && <h2>Settings</h2>}
      <div className="settings-container">
        <Dropdown
          label="Anisette Server:"
          labelId="anisette-label"
          options={anisetteOptions}
          value={anisetteServer}
          onChange={setAnisetteServer}
          allowCustom
          defaultCustomValue="ani.yourserver.com"
          customPlaceholder="Custom Anisette Server"
          customLabel="Custom"
          customToggleLabel="Use custom Anisette server"
          presetToggleLabel="Back to preset servers"
        />
        <div className="settings-buttons">
          <button
            className="action-button danger"
            onClick={() =>
              confirm(
                "Reset Anisette State",
                "Are you sure you want to reset the anisette state? You will be required to enter your 2FA code again.",
                () =>
                  toast.promise(invoke("reset_anisette_state"), {
                    loading: "Resetting anisette state...",
                    success: "Anisette state reset successfully",
                    error: (e) => err("Failed to reset anisette state", e),
                  }),
              )
            }
          >
            Reset anisette state
          </button>
          <button onClick={() => setLogsOpen(true)}>View Logs</button>
        </div>
        <Modal isOpen={logsOpen} close={() => setLogsOpen(false)}>
          <div className="log-outer">
            <div className="log-header">
              <h2>Logs</h2>
              <button
                onClick={() => {
                  const logText = filteredLogs
                    .map(
                      (log) =>
                        `[${log.timestamp}] [${LogLevel[log.level]}] ${log.target ? `<${log.target}>` : ""} ${log.message}`,
                    )
                    .join("\n");
                  navigator.clipboard.writeText(logText);
                  toast.success("Logs copied to clipboard");
                }}
              >
                Copy to clipboard
              </button>
            </div>
            <Dropdown
              label="Log Level:"
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
                <div className="log-entry">No logs yet.</div>
              </pre>
            )}
          </div>
        </Modal>
        {/* <div>
          <label className="settings-label">
            Allow App ID deletion:
            <input
              type="checkbox"
              checked={appIdDeletion}
              onChange={(e) => {
                setAppIdDeletion(e.target.checked);
              }}
            />
          </label>
          <span className="settings-hint">
            Not recommended for free dev accounts, this just hides them from the
            list. You still need to wait for them to expire to free up space.
          </span>
        </div> */}
      </div>
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
