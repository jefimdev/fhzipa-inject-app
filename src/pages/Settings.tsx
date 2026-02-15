import "./Settings.css";
import { useStore } from "../StoreContext";
import { useState } from "react";
import { useLogs } from "../LogContext";
import { LogLevel } from "@fltsci/tauri-plugin-tracing";
import { Modal } from "../components/Modal";
import { Dropdown } from "../components/Dropdown";

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
    "ani.sidestore.io"
  );

  const [logsOpen, setLogsOpen] = useState(false);
  const [logLevelFilter, setLogLevelFilter] = useState("3");
  const logs = useLogs();

  const anisetteOptions = anisetteServers.map(([value, label]) => ({
    value,
    label,
  }));
  const logLevelOptions = [
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
        <button onClick={() => setLogsOpen(true)}>
          View Logs
        </button>
        <Modal isOpen={logsOpen} close={() => setLogsOpen(false)}>
          <div className="log-outer">
            <div className="log-header">
              <h2>Logs</h2>
              <button onClick={() => {
                const logText = filteredLogs.map(log => `[${LogLevel[log.level]}] ${log.message}`).join("\n");
                navigator.clipboard.writeText(logText);
              }}>Copy to clipboard</button>
            </div>
            <Dropdown
              label="Log Level:"
              labelId="log-level-label"
              options={logLevelOptions}
              value={logLevelFilter}
              onChange={setLogLevelFilter}
            />
            <pre className="log-inner">
              {filteredLogs.length > 0 ? filteredLogs.map((log, index) => (
                <div key={`${index}`}>
                  {getHtmlForLevel(log.level)} {log.message}
                </div>
              )) : "No logs yet."}
            </pre>
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
      return <span style={{ color: "gray" }}>[TRACE]</span>;
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
