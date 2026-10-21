import { Fragment } from "react";
import { FaCheck } from "react-icons/fa6";

export type StepState = "done" | "current" | "pending";

export type Step = {
  label: string;
  state: StepState;
};

export const StepsStrip = ({ steps }: { steps: Step[] }) => {
  return (
    <div className="steps-strip">
      {steps.map((step, index) => (
        <Fragment key={step.label}>
          <div className={`step ${step.state}`}>
            <span className="step-marker" aria-hidden="true">
              {step.state === "done" ? (
                <FaCheck />
              ) : (
                String(index + 1).padStart(2, "0")
              )}
            </span>
            <span className="step-index" aria-hidden="true">
              {String(index + 1).padStart(2, "0")}
            </span>
            <span className="step-rule" aria-hidden="true" />
            <span className="step-label">{step.label}</span>
          </div>
          {index < steps.length - 1 && (
            <span className="step-connector" aria-hidden="true" />
          )}
        </Fragment>
      ))}
    </div>
  );
};
