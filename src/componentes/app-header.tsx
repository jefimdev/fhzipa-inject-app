import { useTranslation } from "react-i18next";
import { openUrl } from "@tauri-apps/plugin-opener";
import { FaGithub } from "react-icons/fa6";
import { toast } from "sonner";
import { REPO_URL } from "../configuracoes/links";

export const AppHeader = ({
  eyebrow,
  title,
  subtitle,
}: {
  eyebrow: string;
  title: string;
  subtitle: string;
}) => {
  const { t } = useTranslation();

  const open = async (url: string) => {
    try {
      await openUrl(url);
    } catch (error) {
      console.error("Failed to open link", error);
      toast.error(t("app.open_github_failed"));
    }
  };

  return (
    <header className="app-header">
      <div className="app-header-text">
        <p className="app-eyebrow">{eyebrow}</p>
        <h1 className="app-title">{title}</h1>
        <p className="app-subtitle">{subtitle}</p>
      </div>
      <div className="app-header-actions">
        <button
          type="button"
          className="btn btn-ghost"
          onClick={() => open(REPO_URL)}
        >
          <FaGithub aria-hidden="true" />
          {t("app.github")}
        </button>
      </div>
    </header>
  );
};
