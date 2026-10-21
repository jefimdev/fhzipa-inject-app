import { useCallback, useEffect, useRef, useState } from "react";
import { invoke } from "@tauri-apps/api/core";
import { toast } from "sonner";
import { useTranslation } from "react-i18next";
import { Modal } from "../componentes/modal";
import { useError } from "../contextos/erro";
import { AppError } from "../utilitarios/erros";
import { deviceModel } from "../utilitarios/modelos-dispositivo";
import { FaArrowsRotate, FaMobileScreenButton } from "react-icons/fa6";
import "./dispositivo.css";

export type DeviceInfo = {
  name: string;
  id: number;
  uuid: string;
  connectionType: "USB" | "Network" | "Unknown";
  version: string;
  /** The model of the device, such as "iPhone14,3". */
  productType: string;
};

/**
 * Owns the device list, the current selection and the pairing handshake.
 *
 * The hook is mounted once by the shell so that every view shares the same
 * state instead of polling the device service separately.
 */
export const useDevices = () => {
  const { t } = useTranslation();
  const { err } = useError();

  const [devices, setDevices] = useState<DeviceInfo[]>([]);
  const [selectedDevice, setSelectedDevice] = useState<DeviceInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [waitingToPair, setWaitingToPair] = useState<DeviceInfo | null>(null);
  const [showPairingModal, setShowPairingModal] = useState(false);

  const listingDevices = useRef<boolean>(false);
  const pairingRequestId = useRef<number>(0);
  const pairingModalTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  const clearPairingModalTimer = useCallback(() => {
    if (pairingModalTimer.current) {
      clearTimeout(pairingModalTimer.current);
      pairingModalTimer.current = null;
    }
  }, []);

  useEffect(() => {
    return () => {
      clearPairingModalTimer();
    };
  }, [clearPairingModalTimer]);

  const selectDevice = useCallback(
    (device: DeviceInfo | null) => {
      const requestId = ++pairingRequestId.current;
      clearPairingModalTimer();
      setShowPairingModal(false);
      setWaitingToPair(device);

      if (device) {
        pairingModalTimer.current = setTimeout(() => {
          if (pairingRequestId.current === requestId) {
            setShowPairingModal(true);
          }
        }, 100);
      }

      invoke("set_selected_device", { device })
        .then(() => {
          if (pairingRequestId.current !== requestId) {
            return;
          }
          clearPairingModalTimer();
          setShowPairingModal(false);
          setWaitingToPair(null);
          setSelectedDevice(device);
        })
        .catch((e) => {
          if (pairingRequestId.current !== requestId) {
            return;
          }

          const message = String((e.message ?? e) ?? "Unknown error");
          if (message !== "Pairing cancelled") {
            toast.error(err(t("device.failed_select"), e));
          }
          clearPairingModalTimer();
          setShowPairingModal(false);
          setWaitingToPair(null);
        });
    },
    [clearPairingModalTimer, setSelectedDevice, t],
  );

  const loadDevices = useCallback(async () => {
    if (listingDevices.current) return;
    const promise = new Promise<number>(async (resolve, reject) => {
      listingDevices.current = true;
      setLoading(true);
      try {
        const results = await invoke<Array<{ Ok: DeviceInfo } | { Err: AppError }>>(
          "list_devices",
        );

        const found: DeviceInfo[] = [];
        for (const result of results) {
          if ("Ok" in result) {
            found.push(result.Ok);
          } else if ("Err" in result) {
            toast.error(err(t("device.unable_load_devices_prefix"), result.Err));
          }
        }

        setDevices(found);
        if (selectedDevice) {
          const stillAvailable = found.find((d) => d.id === selectedDevice.id);
          if (!stillAvailable) {
            selectDevice(null);
          }
        }
        if (found.length > 0) {
          const devicesWithPairing = await Promise.all(
            found.map(async (device) => {
              const hasPairing = await invoke<boolean>("has_stored_rppairing", {
                device,
              });
              return hasPairing ? device : null;
            }),
          )
            .catch(() => [])
            .then((results) =>
              results.filter((d): d is DeviceInfo => d !== null),
            );
          if (devicesWithPairing.length > 0) {
            selectDevice(devicesWithPairing[0]);
          }
        }
        listingDevices.current = false;
        setLoading(false);
        resolve(found.length);
      } catch (e) {
        setDevices([]);
        selectDevice(null);
        listingDevices.current = false;
        setLoading(false);
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
      error: (e) => err(t("device.unable_load_devices_prefix"), e),
    });
  }, [setDevices, selectDevice, t]);

  useEffect(() => {
    loadDevices();
  }, [loadDevices]);

  const cancelPairing = useCallback(async () => {
    pairingRequestId.current += 1;
    clearPairingModalTimer();
    setShowPairingModal(false);
    await invoke("cancel_pairing").catch(() => {});
    setWaitingToPair(null);
  }, [clearPairingModalTimer]);

  return {
    devices,
    selectedDevice,
    setSelectedDevice,
    loading,
    refresh: loadDevices,
    selectDevice,
    waitingToPair,
    pairingOpen: showPairingModal && waitingToPair !== null,
    cancelPairing,
  };
};

/** The blocking dialog shown while the device is being paired. */
export const PairingDialog = ({
  open,
  device,
  onCancel,
}: {
  open: boolean;
  device: DeviceInfo | null;
  onCancel: () => void;
}) => {
  const { t } = useTranslation();

  return (
    <Modal isOpen={open} close={onCancel}>
      <div className="pairing-modal-content">
        <div className="spinner" />
        <h2>
          {t("device.pairing_in_progress_header", {
            device: device?.name ?? "Unknown Device",
          })}
        </h2>
        <p>{t("device.pairing_in_progress_hint")}</p>
        <button className="btn" onClick={onCancel}>
          {t("device.pairing_cancel")}
        </button>
      </div>
    </Modal>
  );
};

/** The full device list, used by the dedicated devices view. */
export const DeviceList = ({
  devices,
  selectedDevice,
  onSelect,
  onRefresh,
  loading,
  disabled,
}: {
  devices: DeviceInfo[];
  selectedDevice: DeviceInfo | null;
  onSelect: (device: DeviceInfo) => void;
  onRefresh: () => void;
  loading: boolean;
  disabled: boolean;
}) => {
  const { t } = useTranslation();

  if (devices.length === 0) {
    return (
      <div className="device-empty">
        <span className="device-empty-glyph" aria-hidden="true">
          <FaMobileScreenButton />
        </span>
        <p className="device-empty-title">
          {loading ? t("device.searching") : t("device.none_connected")}
        </p>
        <p className="panel-hint">{t("device.connect_hint")}</p>
        <button className="btn btn-outline" onClick={onRefresh} disabled={loading}>
          <FaArrowsRotate aria-hidden="true" />
          {t("device.refresh")}
        </button>
      </div>
    );
  }

  return (
    <div className="device-list">
      {devices.map((device) => {
        const isActive = selectedDevice?.id === device.id;
        return (
          <button
            key={device.id}
            type="button"
            className={`device-card${isActive ? " active" : ""}`}
            onClick={() => onSelect(device)}
            disabled={disabled}
            aria-pressed={isActive}
          >
            <span className="device-card-glyph" aria-hidden="true">
              <FaMobileScreenButton />
            </span>
            <span className="device-meta">
              <span className="device-name">{device.name}</span>
              <span className="device-connection">
                {[deviceModel(device.productType).name, device.version, device.connectionType]
                  .filter(Boolean)
                  .join(" · ")}
              </span>
            </span>
            {isActive && (
              <span className="pill ok">
                <span className="dot" />
                {t("device.selected")}
              </span>
            )}
          </button>
        );
      })}
    </div>
  );
};
