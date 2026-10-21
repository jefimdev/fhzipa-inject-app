import { useTranslation } from "react-i18next";
import { FaApple, FaArrowsRotate, FaUsb } from "react-icons/fa6";
import { Panel } from "./painel";
import { PhoneRender } from "./render-aparelho";
import { deviceModel } from "../utilitarios/modelos-dispositivo";
import type { DeviceInfo } from "../modulos/dispositivo";

export const DevicePanel = ({
  device,
  loading,
  onRefresh,
}: {
  device: DeviceInfo | null;
  loading: boolean;
  onRefresh: () => void;
}) => {
  const { t } = useTranslation();
  const connected = device !== null;
  // The artwork follows the model of the connected phone.
  const model = deviceModel(device?.productType);

  return (
    <Panel className="device-panel">
      <div className="device-visual">
        <PhoneRender art={model.art} />
      </div>

      <div className="device-info">
        <p className="panel-eyebrow">{t("device.active_label")}</p>

        <div className="device-name-row">
          <h2 className={`device-name-large${connected ? "" : " empty"}`}>
            {connected ? device.name : t("device.none_connected")}
          </h2>
          <span className={`pill${connected ? " ok" : ""}`}>
            <span className="dot" />
            {connected ? t("device.connected") : t("device.disconnected")}
          </span>
        </div>

        {connected && model.name && (
          <p className="device-model">{model.name}</p>
        )}

        <div className="device-facts">
          <div className="device-fact">
            <FaApple aria-hidden="true" />
            <div>
              <span>{t("device.system")}</span>
              <strong>{connected ? device.version : "—"}</strong>
            </div>
          </div>
          <div className="device-fact">
            <FaUsb aria-hidden="true" />
            <div>
              <span>{t("device.connection")}</span>
              <strong>{connected ? device.connectionType : "—"}</strong>
            </div>
          </div>
        </div>

        {!connected && <p className="panel-hint">{t("device.connect_hint")}</p>}

        <div className="row">
          <button
            type="button"
            className="btn btn-outline"
            onClick={onRefresh}
            disabled={loading}
          >
            <FaArrowsRotate aria-hidden="true" />
            {loading ? t("device.searching") : t("device.refresh")}
          </button>
        </div>
      </div>
    </Panel>
  );
};
