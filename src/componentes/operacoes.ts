import { AppError } from "../utilitarios/erros";

export type Operation = {
  id: string;
  titleKey: string;
  successMessageKey?: string;
  successTitleKey?: string;
  steps: OperationStep[];
};

export type OperationStep = {
  id: string;
  titleKey: string;
};

export type OperationState = {
  current: Operation;
  completed: string[];
  started: string[];
  failed: {
    stepId: string;
    extraDetails: AppError;
  }[];
};

type OperationInfoUpdate = {
  updateType: "started" | "finished";
  stepId: string;
};

type OperationFailedUpdate = {
  updateType: "failed";
  stepId: string;
  extraDetails: AppError;
};

export type OperationUpdate = OperationInfoUpdate | OperationFailedUpdate;

export const sideloadOperation = {
  id: "sideload",
  titleKey: "operations.sideload_title",
  steps: [
    {
      id: "prepare",
      titleKey: "operations.sideload_step_prepare",
    },
    {
      id: "install",
      titleKey: "operations.sideload_step_install",
    },
  ],
};
