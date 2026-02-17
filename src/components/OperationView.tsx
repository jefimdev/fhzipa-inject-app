import { OperationState } from "./operations";
import "./OperationView.css";
import { Modal } from "./Modal";
import {
  FaCircleExclamation,
  FaCircleCheck,
  FaCircleMinus,
} from "react-icons/fa6";
import { useState } from "react";
import { toast } from "sonner";
import { openUrl } from "@tauri-apps/plugin-opener";

export default ({
  operationState,
  closeMenu,
}: {
  operationState: OperationState;
  closeMenu: () => void;
}) => {
  const operation = operationState.current;
  const opFailed = operationState.failed.length > 0;
  const done =
    (opFailed &&
      operationState.started.length ==
        operationState.completed.length + operationState.failed.length) ||
    operationState.completed.length == operation.steps.length;

  const [moreDetailsOpen, setMoreDetailsOpen] = useState(false);

  return (
    <Modal
      isOpen={true}
      close={() => {
        if (done) closeMenu();
      }}
      hideClose={!done}
      sizeFit
    >
      <div className="operation-header">
        <h2>
          {done && !opFailed && operation.successTitle
            ? operation?.successTitle
            : operation?.title}
        </h2>
        <p>
          {done
            ? opFailed
              ? "Operation failed."
              : "Operation completed"
            : "Please wait..."}
        </p>
      </div>
      <div className="operation-content-container">
        <div className="operation-content">
          {operation.steps.map((step) => {
            let failed = operationState.failed.find((f) => f.stepId == step.id);
            let completed = operationState.completed.includes(step.id);
            let started = operationState.started.includes(step.id);
            let notStarted = !failed && !completed && !started;

            // a little bit gross but it gets the job done.
            let lines =
              failed?.extraDetails
                ?.split("\n")
                .filter((line) => line.includes("●")) ?? [];
            let errorShort =
              lines[lines.length - 1]?.replace(/●\s*/, "").trim() ?? "";

            return (
              <div className="operation-step" key={step.id}>
                <div className="operation-step-icon">
                  {failed && (
                    <FaCircleExclamation className="operation-error" />
                  )}
                  {!failed && completed && (
                    <FaCircleCheck className="operation-check" />
                  )}
                  {!failed && !completed && started && (
                    <div className="loading-icon" />
                  )}
                  {notStarted && !opFailed && <div className="waiting-icon" />}
                  {notStarted && opFailed && (
                    <FaCircleMinus className="operation-skipped" />
                  )}
                </div>

                <div className="operation-step-internal">
                  <p>{step.title}</p>
                  {failed && (
                    <>
                      <pre className="operation-extra-details">
                        {errorShort ?? failed.extraDetails.replace(/^\n+/, "")}
                      </pre>
                      <p
                        className="operation-more-details"
                        role="button"
                        tabIndex={0}
                        onClick={() => setMoreDetailsOpen(!moreDetailsOpen)}
                      >
                        More Details {moreDetailsOpen ? "▲" : "▼"}
                      </p>
                      {moreDetailsOpen && (
                        <pre className="operation-extra-details">
                          {failed.extraDetails.replace(/^\n+/, "")}
                        </pre>
                      )}
                    </>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      </div>
      {done && !opFailed && operation.successMessage && (
        <p className="operation-success-message">{operation.successMessage}</p>
      )}
      {done && !(!opFailed && operation.successMessage) && <p></p>}
      {opFailed && done && (
        <>
          <p style={{ margin: "1.25rem 0 0.5rem 0" }}>
            If the issue persists, copy and send the error to{" "}
            <span
              onClick={() => openUrl("https://discord.gg/gjH8RaqhMr")}
              role="link"
              className="error-link"
            >
              Discord
            </span>{" "}
            or a{" "}
            <span
              onClick={() =>
                openUrl("https://github.com/nab138/iloader/issues")
              }
              role="link"
              className="error-link"
            >
              GitHub issue
            </span>{" "}
            for support.
          </p>
          <button
            style={{ marginBottom: "1.25rem", width: "100%" }}
            className="action-button primary"
            onClick={() => {
              navigator.clipboard.writeText(
                operationState.failed[0]?.extraDetails?.replace(/^\n+/, "") ??
                  "No error",
              );
              toast.success("Logs copied to clipboard");
            }}
          >
            Copy error to clipboard
          </button>
        </>
      )}
      {done && (
        <button style={{ width: "100%" }} onClick={closeMenu}>
          Dismiss
        </button>
      )}
    </Modal>
  );
};
