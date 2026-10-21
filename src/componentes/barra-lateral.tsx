import { useTranslation } from "react-i18next";
import {
  FaGear,
  FaHouse,
  FaMobileScreenButton,
  FaShieldHalved,
  FaStore,
  FaTableCells,
} from "react-icons/fa6";
import type { IconType } from "react-icons";
import logo from "../recursos/imagens/fhzipa-inject.svg";
import "./barra-lateral.css";

export type AppView =
  | "overview"
  | "store"
  | "devices"
  | "certificates"
  | "appids"
  | "settings";

const NAV_ITEMS: { id: AppView; icon: IconType; labelKey: string }[] = [
  { id: "overview", icon: FaHouse, labelKey: "nav.overview" },
  { id: "store", icon: FaStore, labelKey: "nav.store" },
  { id: "devices", icon: FaMobileScreenButton, labelKey: "nav.devices" },
  { id: "certificates", icon: FaShieldHalved, labelKey: "nav.certificates" },
  { id: "appids", icon: FaTableCells, labelKey: "nav.app_ids" },
  { id: "settings", icon: FaGear, labelKey: "nav.settings" },
];

export const Sidebar = ({
  view,
  setView,
  version,
}: {
  view: AppView;
  setView: (view: AppView) => void;
  version: string;
}) => {
  const { t } = useTranslation();

  return (
    <aside className="app-sidebar">
      <div className="brand">
        <img className="brand-logo" src={logo} alt={t("app.logo_alt")} />
        <div className="brand-text">
          <span className="brand-name">FHZIPA INJECT</span>
          <span className="brand-sub">{t("app.brand_subtitle")}</span>
        </div>
      </div>

      <nav className="nav" aria-label={t("nav.label")}>
        {NAV_ITEMS.map((item) => {
          const Icon = item.icon;
          const active = view === item.id;
          return (
            <button
              key={item.id}
              type="button"
              className={`nav-item${active ? " active" : ""}`}
              aria-current={active ? "page" : undefined}
              onClick={() => setView(item.id)}
            >
              <Icon aria-hidden="true" />
              <span>{t(item.labelKey)}</span>
            </button>
          );
        })}
      </nav>

      <div className="sidebar-footer">
        {t("version")} {version}
      </div>
    </aside>
  );
};
