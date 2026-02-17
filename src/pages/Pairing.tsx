import "./Certificates.css";
import { invoke } from "@tauri-apps/api/core";
import { useCallback, useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { useError } from "../ErrorContext";
import { useDialog } from "../DialogContext";

type PairingAppInfo = {
  name: string;
  bundleId: string;
  path: string;
};

export const Pairing = () => {
  const [apps, setApps] = useState<PairingAppInfo[]>([]);

  const [loading, setLoading] = useState<boolean>(false);
  const loadingRef = useRef<boolean>(false);
  const { err } = useError();
  const { confirm } = useDialog();

  const loadApps = useCallback(async () => {
    if (loadingRef.current) return;
    const promise = async () => {
      loadingRef.current = true;
      setLoading(true);
      let list = await invoke<PairingAppInfo[]>("installed_pairing_apps");
      setApps(list);
      setLoading(false);
      loadingRef.current = false;
    };
    toast.promise(promise, {
      loading: "Loading Apps...",
      success: "Apps loaded successfully!",
      error: (e) => err("Failed to load Apps", e),
    });
  }, [setApps]);

  const pair = useCallback(
    async (app: PairingAppInfo) => {
      const promise = invoke<void>("place_pairing_cmd", {
        bundleId: app.bundleId,
        path: app.path,
      });
      toast.promise(promise, {
        loading: "Placing pairing file...",
        success: "Pairing file placed successfully!",
        error: (e) => err("Failed to place pairing", e),
      });
    },
    [setApps, loadApps],
  );

  useEffect(() => {
    loadApps();
  }, []);

  return (
    <>
      <h2>Manage Pairing File</h2>
      {apps.length === 0 ? (
        <div>{loading ? "Loading App..." : "No Supported Apps found."}</div>
      ) : (
        <div className="card">
          <div className="certificate-table-container">
            <table className="certificate-table">
              <thead>
                <tr className="certificate-item">
                  <th className="cert-item-part">Name</th>
                  <th className="cert-item-part">Bundle ID</th>
                  <th>Place Pairing File</th>
                </tr>
              </thead>
              <tbody>
                {apps.map((app, i) => (
                  <tr
                    key={app.bundleId}
                    className={
                      "certificate-item" +
                      (i === apps.length - 1 ? " cert-item-last" : "")
                    }
                  >
                    <td className="cert-item-part">{app.name}</td>
                    <td className="cert-item-part">{app.bundleId}</td>
                    <td
                      className="pairing-place"
                      onClick={() => pair(app)}
                      role="button"
                      tabIndex={0}
                    >
                      Place
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
      <button
        style={{ marginTop: "1em", width: "100%" }}
        onClick={() => {
          confirm(
            "Advanced: Export Pairing File",
            `This is not reccomended unless you know what you're doing. Press "Place" next to an app to transfer it automatically instead. Are you sure you still want to export your pairing file?`,
            () => {
              const promise = invoke<void>("export_pairing_cmd");
              toast.promise(promise, {
                loading: "Exporting pairing file...",
                success: "Pairing file exported successfully!",
                error: (e) => err("Failed to export pairing file", e),
              });
            },
          );
        }}
      >
        Export (Not Reccomended)
      </button>
      <button
        style={{ marginTop: "1em", width: "100%" }}
        onClick={loadApps}
        disabled={loading}
      >
        Refresh Installed Apps
      </button>
    </>
  );
};
