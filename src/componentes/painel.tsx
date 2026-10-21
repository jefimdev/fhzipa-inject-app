import { ReactNode } from "react";
import type { IconType } from "react-icons";

export const Panel = ({
  children,
  className,
  spanAll,
}: {
  children: ReactNode;
  className?: string;
  spanAll?: boolean;
}) => {
  const classes = ["panel", spanAll ? "span-all" : null, className]
    .filter(Boolean)
    .join(" ");
  return <section className={classes}>{children}</section>;
};

export const PanelHeading = ({
  icon: Icon,
  title,
  subtitle,
}: {
  icon?: IconType;
  title: string;
  subtitle?: string;
}) => {
  return (
    <div className="panel-heading">
      {Icon && (
        <span className="panel-glyph" aria-hidden="true">
          <Icon />
        </span>
      )}
      <div>
        <h2 className="panel-title">{title}</h2>
        {subtitle && <p className="panel-subtitle">{subtitle}</p>}
      </div>
    </div>
  );
};
