import { useCallback, useEffect, useRef, useState } from "react";
import "./Device.css";
import { invoke } from "@tauri-apps/api/core";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";

export type DeviceInfo = {
  name: string;
  id: number;
  uuid: string;
  connectionType: "USB" | "Network" | "Unknown";
};

export const Device = ({
  selectedDevice,
  setSelectedDevice,
  registerRefresh,
}: {
  selectedDevice: DeviceInfo | null;
  setSelectedDevice: (device: DeviceInfo | null) => void;
  registerRefresh?: (fn?: () => void) => void;
}) => {
  const { t } = useTranslation();
  const [devices, setDevices] = useState<DeviceInfo[]>([]);

  const listingDevices = useRef<boolean>(false);

  const selectDevice = useCallback(
    (device: DeviceInfo | null) => {
      setSelectedDevice(device);
      invoke("set_selected_device", { device }).catch((err) => {
        toast.error(t("device.failed_select_prefix") + err);
      });
    },
    [setSelectedDevice, t],
  );

  const loadDevices = useCallback(async () => {
    if (listingDevices.current) return;
    const promise = new Promise<number>(async (resolve, reject) => {
      listingDevices.current = true;
      try {
        const devices = await invoke<DeviceInfo[]>("list_devices");
        setDevices(devices);
        selectDevice(devices.length > 0 ? devices[0] : null);
        listingDevices.current = false;
        resolve(devices.length);
      } catch (e) {
        setDevices([]);
        selectDevice(null);
        listingDevices.current = false;
        reject(e);
      }
    });

    toast.promise(promise, {
      loading: t("device.loading_devices"),
      success: (count) => {
        if (count === 0) {
          return t("device.no_devices_found");
        }
        return count > 1 ? t("device.found_devices") : t("device.found_device");
      },
      error: (e) => t("device.unable_load_devices_prefix") + e,
    });
  }, [setDevices, selectDevice, t]);
  useEffect(() => {
    loadDevices();
  }, [loadDevices]);

  useEffect(() => {
    registerRefresh?.(loadDevices);
    return () => registerRefresh?.(undefined);
  }, [registerRefresh, loadDevices]);

  return (
    <>
      <h2 style={{ marginTop: 0 }}>{t("device.title")}</h2>
      <div className="credentials-container">
        {devices.length === 0 && <div>{t("device.no_devices_found_period")}</div>}
        {devices.map((device) => {
          const isActive = selectedDevice?.id === device.id;
          return (
            <button
              key={device.id}
              className={"device-card card" + (isActive ? " active" : "")}
              onClick={() => selectDevice(device)}
            >
              <div className="device-meta">
                <span className="device-name">{device.name}</span>
                <span className="device-connection">
                  {device.connectionType}
                </span>
              </div>
              {isActive && (
                <span className="device-selected-pill">{t("device.selected")}</span>
              )}
            </button>
          );
        })}
        <button onClick={loadDevices}>{t("common.refresh")}</button>
      </div>
    </>
  );
};
