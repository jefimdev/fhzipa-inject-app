import { useTranslation } from "react-i18next";
import {
  FaCircleCheck,
  FaClock,
  FaFileArrowUp,
  FaSpinner,
  FaTriangleExclamation,
  FaUser,
} from "react-icons/fa6";
import { Panel } from "./painel";

export type StatusKind =
  | "waiting-device"
  | "waiting-account"
  | "need-ipa"
  | "ready"
  | "installing"
  | "done"
  | "failed";

const ICONS = {
  "waiting-device": FaClock,
  "waiting-account": FaUser,
  "need-ipa": FaFileArrowUp,
  ready: FaCircleCheck,
  installing: FaSpinner,
  done: FaCircleCheck,
  failed: FaTriangleExclamation,
} as const;

export const StatusPanel = ({
  kind,
  detail,
}: {
  kind: StatusKind;
  detail?: string;
}) => {
  const { t } = useTranslation();
  const Icon = ICONS[kind];

  const tone =
    kind === "done" || kind === "ready"
      ? "ok"
      : kind === "failed"
        ? "danger"
        : kind === "installing"
          ? "busy"
          : "idle";

  return (
    <Panel className="status-panel">
      <span className={`status-icon ${tone}`} aria-hidden="true">
        <Icon className={kind === "installing" ? "status-spinner" : undefined} />
      </span>
      <div className="status-text">
        <h2 className="panel-title">{t(`status.${kind}`)}</h2>
        <p className="panel-subtitle">{detail ?? t(`status.${kind}_detail`)}</p>
      </div>
    </Panel>
  );
};
