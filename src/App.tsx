import { useCallback, useState } from "react";
import "./App.css";
import { AppleID } from "./AppleID";
import { Device } from "./Device";
import { toast } from "sonner";
import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import {
  installSideStoreOperation,
  Operation,
  OperationState,
  OperationUpdate,
} from "./components/operations";
import { listen } from "@tauri-apps/api/event";
import OperationView from "./components/OperationView";

function App() {
  const [operationState, setOperationState] = useState<OperationState | null>(
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

  return (
    <main className="container">
      <h1>iloader</h1>
      <div className="cards-container">
        <div className="card-dark">
          <AppleID />
        </div>
        <div className="card-dark">
          <Device />
        </div>
        <div className="card-dark buttons-container">
          <h2>Actions</h2>
          <div className="buttons">
            <button
              onClick={() =>
                startOperation(installSideStoreOperation, {
                  nightly: false,
                })
              }
            >
              Install SideStore
            </button>
            <button
              onClick={async () => {
                let path = await open({
                  multiple: false,
                  filters: [{ name: "IPA Files", extensions: ["ipa"] }],
                });
                if (!path) return;
                toast.promise(invoke("sideload", { appPath: path as string }), {
                  loading: "Installing...",
                  success: "App installed successfully!",
                  error: (e) => {
                    console.error(e);
                    return e;
                  },
                });
              }}
            >
              Install Other
            </button>
            <button>Manage Pairing File</button>
            <button>Manage Certificates</button>
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
    </main>
  );
}

export default App;
