import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { FaChevronDown, FaFilter } from "react-icons/fa6";
import type { StoreCategory } from "../utilitarios/tipos-loja";

export type FeatureFilter = {
  id: string;
  match: string[];
};

export const FilterMenu = ({
  categories,
  category,
  onCategory,
  feature,
  onFeature,
  features,
  showFeatures = true,
}: {
  categories: StoreCategory[];
  category: string | null;
  onCategory: (id: string | null) => void;
  feature: string | null;
  onFeature: (id: string | null) => void;
  features: FeatureFilter[];
  /** The sources that do not carry the mods have nothing to filter by. */
  showFeatures?: boolean;
}) => {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const menuRef = useRef<HTMLDivElement | null>(null);
  const buttonRef = useRef<HTMLButtonElement | null>(null);

  useEffect(() => {
    if (!open) return;

    const onPointerDown = (event: MouseEvent) => {
      const target = event.target as Node;
      if (
        menuRef.current?.contains(target) ||
        buttonRef.current?.contains(target)
      ) {
        return;
      }
      setOpen(false);
    };

    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open]);

  const activeCategory = categories.find((entry) => entry.id === category);
  const activeFeature = features.find((entry) => entry.id === feature);
  const activeCount =
    (category ? 1 : 0) + (feature && showFeatures ? 1 : 0);

  return (
    <div className="filter-menu" ref={menuRef}>
      <button
        ref={buttonRef}
        type="button"
        className={`filter-trigger${open ? " open" : ""}${activeCount ? " active" : ""}`}
        onClick={() => setOpen((value) => !value)}
        aria-expanded={open}
        aria-haspopup="true"
      >
        <FaFilter aria-hidden="true" />
        {t("filters.label")}
        {activeCount > 0 && <span className="filter-count">{activeCount}</span>}
        <FaChevronDown className="filter-caret" aria-hidden="true" />
      </button>

      {open && (
        <div className="filter-panel" role="dialog" aria-label={t("filters.label")}>
          <div className="filter-panel-section">
            <span className="filter-panel-title">{t("filters.categories")}</span>
            <div className="filter-options">
              <button
                type="button"
                className={`filter-option${category === null ? " active" : ""}`}
                onClick={() => onCategory(null)}
              >
                {t("store.all")}
                <span className="filter-option-count">
                  {categories.reduce((sum, entry) => sum + entry.count, 0)}
                </span>
              </button>
              {categories.map((entry) => (
                <button
                  key={entry.id}
                  type="button"
                  className={`filter-option${category === entry.id ? " active" : ""}`}
                  onClick={() => onCategory(entry.id)}
                >
                  {entry.name}
                  <span className="filter-option-count">{entry.count}</span>
                </button>
              ))}
            </div>
          </div>

          {showFeatures && (
            <div className="filter-panel-section">
              <span className="filter-panel-title">{t("filters.features")}</span>
              <div className="filter-options">
                <button
                  type="button"
                  className={`filter-option${feature === null ? " active" : ""}`}
                  onClick={() => onFeature(null)}
                >
                  {t("store.all")}
                </button>
                {features.map((entry) => (
                  <button
                    key={entry.id}
                    type="button"
                    className={`filter-option${feature === entry.id ? " active" : ""}`}
                    onClick={() => onFeature(entry.id)}
                  >
                    {t(`filters.${entry.id}`)}
                  </button>
                ))}
              </div>
            </div>
          )}

          {(activeCategory || activeFeature) && (
            <div className="filter-panel-footer">
              <span className="filter-summary">
                {[activeCategory?.name, activeFeature ? t(`filters.${activeFeature.id}`) : null]
                  .filter(Boolean)
                  .join(" · ")}
              </span>
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                onClick={() => {
                  onCategory(null);
                  onFeature(null);
                }}
              >
                {t("filters.clear")}
              </button>
            </div>
          )}
        </div>
      )}
    </div>
  );
};
