import { useCallback, useEffect, useMemo, useState } from "react";
import "./estilos/app.css";
import { AppleID } from "./modulos/apple-id";
import { invoke } from "@tauri-apps/api/core";
import { listen } from "@tauri-apps/api/event";
import { getVersion } from "@tauri-apps/api/app";
import { toast } from "sonner";
import { FaGear } from "react-icons/fa6";
import { Modal } from "./componentes/modal";
import { Certificates } from "./paginas/certificados";
import { AppIds } from "./paginas/app-ids";
import { Pairing } from "./paginas/pareamento";
import { Preferences } from "./paginas/configuracoes";
import { Store } from "./paginas/loja";
import { useTranslation } from "react-i18next";
import { usePlatform } from "./contextos/plataforma";
import { AppHeader } from "./componentes/app-header";
import { Sidebar } from "./componentes/barra-lateral";
import type { AppView } from "./componentes/barra-lateral";
import { StepsStrip } from "./componentes/faixa-passos";
import type { Step } from "./componentes/faixa-passos";
import { DevicePanel } from "./componentes/painel-dispositivo";
import { ImportPanel } from "./componentes/painel-importacao";
import type { IpaSelection } from "./componentes/painel-importacao";
import { StatusPanel } from "./componentes/painel-status";
import type { StatusKind } from "./componentes/painel-status";
import { DeviceList, PairingDialog, useDevices } from "./modulos/dispositivo";
import {
  sideloadOperation,
  Operation,
  OperationState,
  OperationUpdate,
} from "./componentes/operacoes";
import OperationView from "./componentes/vista-operacao";

function App() {
  const { t } = useTranslation();
  const { platform } = usePlatform();

  const [view, setView] = useState<AppView>("overview");
  const [operationState, setOperationState] = useState<OperationState | null>(
    null,
  );
  const [loggedInAs, setLoggedInAs] = useState<string | null>(null);
  const [noKeyringAvailable, setNoKeyringAvailable] = useState<boolean>(false);
  const [version, setVersion] = useState<string>("");
  const [pairingOpen, setPairingOpen] = useState(false);
  const [ipa, setIpa] = useState<IpaSelection | null>(null);

  const device = useDevices();
  const {
    devices,
    selectedDevice,
    setSelectedDevice,
    loading: devicesLoading,
    refresh: refreshDevices,
    selectDevice,
    waitingToPair,
    pairingOpen: devicePairingOpen,
    cancelPairing,
  } = device;

  const checkKeyring = useCallback(async () => {
    try {
      let available = await invoke<boolean>("keyring_available");
      setNoKeyringAvailable(!available);
    } catch (e) {
      console.error("Unable to check keyring availability:", e);
      setNoKeyringAvailable(true);
    }
  }, []);

  useEffect(() => {
    checkKeyring();
  }, [checkKeyring]);

  useEffect(() => {
    const fetchVersion = async () => {
      const version = await getVersion();
      setVersion(version);
    };
    fetchVersion();
  }, []);

  const shortcutLabel = useCallback(
    (mac: string, windows: string, linux?: string) => {
      if (platform === "mac") return mac;
      if (platform === "linux") return linux ?? windows;
      return windows;
    },
    [platform],
  );

  const startOperation = useCallback(
    async (
      operation: Operation,
      params: { [key: string]: any },
    ): Promise<void> => {
      setOperationState({
        current: operation,
        started: [],
        failed: [],
        completed: [],
      });
      return new Promise<void>(async (resolve, reject) => {
        const unlistenFn = await listen<OperationUpdate>(
          "operation_" + operation.id,
          (event) => {
            setOperationState((old) => {
              if (old == null) return null;
              if (event.payload.updateType === "started") {
                return {
                  ...old,
                  started: [...old.started, event.payload.stepId],
                };
              } else if (event.payload.updateType === "finished") {
                return {
                  ...old,
                  completed: [...old.completed, event.payload.stepId],
                };
              } else if (event.payload.updateType === "failed") {
                return {
                  ...old,
                  failed: [
                    ...old.failed,
                    {
                      stepId: event.payload.stepId,
                      extraDetails: event.payload.extraDetails,
                    },
                  ],
                };
              }
              return old;
            });
          },
        );
        try {
          await invoke(operation.id + "_operation", params);
          unlistenFn();
          resolve();
        } catch (e) {
          unlistenFn();
          reject(e);
        }
      });
    },
    [setOperationState],
  );

  const ensuredLoggedIn = useCallback((): boolean => {
    if (loggedInAs) return true;
    toast.error(t("app.must_be_logged_in"));
    return false;
  }, [loggedInAs, t]);

  const ensureSelectedDevice = useCallback((): boolean => {
    if (selectedDevice) return true;
    toast.error(t("app.must_select_device"));
    return false;
  }, [selectedDevice, t]);

  const installFile = useCallback(
    (path: string) => {
      if (!ensuredLoggedIn() || !ensureSelectedDevice()) return;
      startOperation(sideloadOperation, { appPath: path }).catch((e) => {
        console.log(e.type);
        console.error(e.message);
      });
    },
    [ensuredLoggedIn, ensureSelectedDevice, startOperation],
  );

  const install = useCallback(() => {
    if (!ipa) return;
    installFile(ipa.path);
  }, [ipa, installFile]);

  useEffect(() => {
    const handler = (event: KeyboardEvent) => {
      if (event.key === undefined) return;
      const key = event.key.toLowerCase();
      const primaryPressed = platform === "mac" ? event.metaKey : event.ctrlKey;
      if (!primaryPressed) return;

      if (!event.shiftKey && key === "p") {
        event.preventDefault();
        setPairingOpen(true);
      } else if (event.shiftKey && key === "c") {
        event.preventDefault();
        setView("certificates");
      } else if (event.shiftKey && key === "a") {
        event.preventDefault();
        setView("appids");
      } else if (!event.shiftKey && key === "r") {
        event.preventDefault();
        refreshDevices();
      }
    };

    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, [platform, refreshDevices]);

  const steps = useMemo<Step[]>(() => {
    const deviceDone = selectedDevice !== null;
    const accountDone = loggedInAs !== null;
    const ipaDone = ipa !== null;

    const state = (done: boolean, isNext: boolean) =>
      done ? ("done" as const) : isNext ? ("current" as const) : ("pending" as const);

    return [
      { label: t("steps.device"), state: state(deviceDone, !deviceDone) },
      {
        label: t("steps.account"),
        state: state(accountDone, deviceDone && !accountDone),
      },
      {
        label: t("steps.ipa"),
        state: state(ipaDone, deviceDone && accountDone && !ipaDone),
      },
    ];
  }, [selectedDevice, loggedInAs, ipa, t]);

  const statusKind: StatusKind = useMemo(() => {
    if (operationState) {
      if (operationState.failed.length > 0) return "failed";
      if (
        operationState.completed.length ===
        operationState.current.steps.length
      ) {
        return "done";
      }
      return "installing";
    }
    if (!selectedDevice) return "waiting-device";
    if (!loggedInAs) return "waiting-account";
    if (!ipa) return "need-ipa";
    return "ready";
  }, [operationState, selectedDevice, loggedInAs, ipa]);

  const headerCopy = {
    overview: {
      eyebrow: t("home.eyebrow"),
      title: t("home.title"),
      subtitle: t("home.subtitle"),
    },
    store: {
      eyebrow: t("nav.store"),
      title: t("store.title"),
      subtitle: t("store.subtitle"),
    },
    devices: {
      eyebrow: t("nav.devices"),
      title: t("devices.title"),
      subtitle: t("devices.subtitle"),
    },
    certificates: {
      eyebrow: t("nav.certificates"),
      title: t("certificates.manage"),
      subtitle: t("certificates.subtitle"),
    },
    appids: {
      eyebrow: t("nav.app_ids"),
      title: t("app_ids.manage"),
      subtitle: t("app_ids.subtitle"),
    },
    settings: {
      eyebrow: t("nav.settings"),
      title: t("prefs.title"),
      subtitle: t("prefs.subtitle"),
    },
  }[view];

  return (
    <div className="app-shell">
      <Sidebar view={view} setView={setView} version={version} />

      <main className="app-main">
        <AppHeader {...headerCopy} />

        {view === "overview" && (
          <>
            <StepsStrip steps={steps} />

            <div className="dashboard">
              <DevicePanel
                device={selectedDevice}
                loading={devicesLoading}
                onRefresh={refreshDevices}
              />

              <section className="panel">
                <AppleID
                  loggedInAs={loggedInAs}
                  setLoggedInAs={setLoggedInAs}
                  noKeyringAvailable={noKeyringAvailable}
                />
              </section>

              <ImportPanel
                selection={ipa}
                onSelect={setIpa}
                onClear={() => setIpa(null)}
                onInstall={install}
                canInstall={!!loggedInAs && !!selectedDevice}
              />

              <StatusPanel kind={statusKind} />

              <section className="panel span-all">
                <div className="panel-heading">
                  <span className="panel-glyph" aria-hidden="true">
                    <FaGear />
                  </span>
                  <div>
                    <h2 className="panel-title">{t("prefs.title")}</h2>
                    <p className="panel-subtitle">{t("prefs.subtitle")}</p>
                  </div>
                </div>
                <Preferences
                  ensureSelectedDevice={ensureSelectedDevice}
                  setSelectedDevice={setSelectedDevice}
                  platform={platform}
                  shortcutLabel={shortcutLabel}
                  checkKeyring={checkKeyring}
                />
              </section>
            </div>
          </>
        )}

        {view === "store" && (
          <Store
            onDownloaded={(path, item, size) => {
              setIpa({
                path,
                name: item.name,
                size,
                info: {
                  displayName: item.name,
                  bundleId: item.bundleId || null,
                  version: item.version || null,
                },
              });
              toast.success(t("store.ready_to_install"));
            }}
            onInstall={(path) => installFile(path)}
          />
        )}

        {view === "devices" && (
          <section className="panel">
            <DeviceList
              devices={devices}
              selectedDevice={selectedDevice}
              onSelect={selectDevice}
              onRefresh={refreshDevices}
              loading={devicesLoading}
              disabled={waitingToPair !== null}
            />
            <div className="row">
              <button
                type="button"
                className="btn btn-outline"
                onClick={() => setPairingOpen(true)}
                disabled={!selectedDevice}
              >
                {t("app.manage_pairing_file")}
                <span className="text-muted">
                  {` (${shortcutLabel("⌘P", "Ctrl+P")})`}
                </span>
              </button>
            </div>
          </section>
        )}

        {view === "certificates" && (
          <section className="panel">
            <Certificates />
          </section>
        )}

        {view === "appids" && (
          <section className="panel">
            <AppIds />
          </section>
        )}

        {view === "settings" && (
          <section className="panel">
            <Preferences
              ensureSelectedDevice={ensureSelectedDevice}
              setSelectedDevice={setSelectedDevice}
              platform={platform}
              shortcutLabel={shortcutLabel}
              checkKeyring={checkKeyring}
            />
          </section>
        )}
      </main>

      <PairingDialog
        open={devicePairingOpen}
        device={waitingToPair}
        onCancel={cancelPairing}
      />

      <Modal isOpen={pairingOpen} close={() => setPairingOpen(false)}>
        <Pairing />
      </Modal>

      {operationState && (
        <OperationView
          operationState={operationState}
          closeMenu={() => setOperationState(null)}
        />
      )}
    </div>
  );
}

export default App;
