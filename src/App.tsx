import { useCallback, useState } from "react";
import "./App.css";
import { AppleID } from "./AppleID";
import { Device, DeviceInfo } from "./Device";
import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import {
  sideloadOperation,
  installSideStoreOperation,
  Operation,
  OperationState,
  OperationUpdate,
} from "./components/operations";
import { listen } from "@tauri-apps/api/event";
import OperationView from "./components/OperationView";
import { toast } from "sonner";
import { Modal } from "./components/Modal";
import { Certificates } from "./Certificates";

function App() {
  const [operationState, setOperationState] = useState<OperationState | null>(
    null
  );
  const [loggedInAs, setLoggedInAs] = useState<string | null>(null);
  const [selectedDevice, setSelectedDevice] = useState<DeviceInfo | null>(null);
  const [openModal, setOpenModal] = useState<null | "certificates" | "appids">(
    null
  );

  const startOperation = useCallback(
    async (
      operation: Operation,
      params: { [key: string]: any }
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
          }
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
    [setOperationState]
  );

  const ensuredLoggedIn = useCallback((): boolean => {
    if (loggedInAs) return true;
    toast.error("You must be logged in!");
    return false;
  }, [loggedInAs]);

  const ensureSelectedDevice = useCallback((): boolean => {
    if (selectedDevice) return true;
    toast.error("You must select a device!");
    return false;
  }, [selectedDevice]);

  return (
    <main className="container">
      <h1>iloader</h1>
      <div className="cards-container">
        <div className="card-dark">
          <AppleID loggedInAs={loggedInAs} setLoggedInAs={setLoggedInAs} />
        </div>
        <div className="card-dark">
          <Device
            selectedDevice={selectedDevice}
            setSelectedDevice={setSelectedDevice}
          />
        </div>
        <div className="card-dark buttons-container">
          <h2>Actions</h2>
          <div className="buttons">
            <button
              onClick={() => {
                if (!ensuredLoggedIn() || !ensureSelectedDevice()) return;
                startOperation(installSideStoreOperation, {
                  nightly: false,
                });
              }}
            >
              Install SideStore (Stable)
            </button>
            <button
              onClick={() => {
                if (!ensuredLoggedIn() || !ensureSelectedDevice()) return;
                startOperation(installSideStoreOperation, {
                  nightly: true,
                });
              }}
            >
              Install SideStore (Nightly)
            </button>
            <button
              onClick={async () => {
                if (!ensuredLoggedIn() || !ensureSelectedDevice()) return;
                let path = await open({
                  multiple: false,
                  filters: [{ name: "IPA Files", extensions: ["ipa"] }],
                });
                if (!path) return;
                startOperation(sideloadOperation, {
                  appPath: path as string,
                });
              }}
            >
              Install Other
            </button>
            <button>Manage Pairing File</button>
            <button
              onClick={() => {
                if (!ensuredLoggedIn()) return;
                setOpenModal("certificates");
              }}
            >
              Manage Certificates
            </button>
            <button>Manage App IDs</button>
          </div>
        </div>
      </div>
      {operationState && (
        <OperationView
          operationState={operationState}
          closeMenu={() => setOperationState(null)}
        />
      )}
      <Modal
        isOpen={openModal === "certificates"}
        close={() => setOpenModal(null)}
      >
        <Certificates />
      </Modal>
    </main>
  );
}

export default App;
