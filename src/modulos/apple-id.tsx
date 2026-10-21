import { useEffect, useRef, useState } from "react";
import "./apple-id.css";
import { invoke } from "@tauri-apps/api/core";
import { emit, listen } from "@tauri-apps/api/event";
import { load } from "@tauri-apps/plugin-store";
import { Modal } from "../componentes/modal";
import { toast } from "sonner";
import { useStore } from "../contextos/loja";
import { useError } from "../contextos/erro";
import { Certificate } from "../paginas/certificados";
import type { AppError } from "../utilitarios/erros";
import { useTranslation } from "react-i18next";
import { DEFAULT_ANISETTE_SERVER } from "../configuracoes/servidores-anisette";
import {
  FaArrowRightFromBracket,
  FaEnvelope,
  FaEye,
  FaEyeSlash,
  FaLock,
  FaUser,
} from "react-icons/fa6";

const storePromise = load("data.json");

export const AppleID = ({
  loggedInAs,
  setLoggedInAs,
  noKeyringAvailable,
}: {
  loggedInAs: string | null;
  setLoggedInAs: (id: string | null) => void;
  noKeyringAvailable: boolean;
}) => {
  const { t } = useTranslation();
  const [storedIds, setStoredIds] = useState<string[]>([]);
  const [forceUpdateIds, setForceUpdateIds] = useState<number>(0);
  const [emailInput, setEmailInput] = useState<string>("");
  const [passwordInput, setPasswordInput] = useState<string>("");
  const [showPassword, setShowPassword] = useState(false);
  const [saveCredentials, setSaveCredentials] = useState<boolean>(false);
  const [submitting, setSubmitting] = useState(false);
  const [tfaOpen, setTfaOpen] = useState<boolean>(false);
  const [tfaCode, setTfaCode] = useState<string>("");
  const [addAccountOpen, setAddAccountOpen] = useState<boolean>(false);
  const [anisetteServer] = useStore<string>(
    "newAnisetteServer",
    DEFAULT_ANISETTE_SERVER,
  );
  const [certs, setCerts] = useState<Certificate[] | null>(null);
  const [selectedSerials, setSelectedSerials] = useState<string[]>([]);
  const [chooseCertsOpen, setChooseCertsOpen] = useState<boolean>(false);
  const { err } = useError();

  useEffect(() => {
    let getLoggedInAs = async () => {
      let account = await invoke<string | null>("logged_in_as");
      setLoggedInAs(account);
    };
    let getStoredIds = async () => {
      const store = await storePromise;
      let ids = (await store.get<string[]>("ids")) ?? [];
      setStoredIds(ids);
    };

    getLoggedInAs();
    getStoredIds();
  }, [forceUpdateIds]);

  useEffect(() => {
    setSelectedSerials(certs?.map((c) => c.serialNumber) ?? []);
  }, [certs]);

  const listenerAdded = useRef<boolean>(false);
  const unlisten = useRef<() => void>(() => {});

  useEffect(() => {
    if (!listenerAdded.current) {
      (async () => {
        const unlistenFn = await listen("2fa-required", () => {
          setTfaOpen(true);
        });
        unlisten.current = unlistenFn;
      })();
      listenerAdded.current = true;
    }
    return () => {
      unlisten.current();
    };
  }, []);

  const certListenerAdded = useRef<boolean>(false);
  const certUnlisten = useRef<() => void>(() => {});

  useEffect(() => {
    if (!certListenerAdded.current) {
      (async () => {
        const unlistenFn = await listen<Certificate[]>(
          "max-certs-reached",
          (certs) => {
            setCerts(certs.payload);
          },
        );
        certUnlisten.current = unlistenFn;
      })();
      certListenerAdded.current = true;
    }
    return () => {
      certUnlisten.current();
    };
  }, []);

  const submitLogin = async (event: React.FormEvent) => {
    event.preventDefault();
    if (submitting) return;
    if (!emailInput || !passwordInput) {
      toast.warning(t("apple_id.enter_email_password"));
      return;
    }

    setSubmitting(true);
    try {
      await invoke("login_new", {
        email: emailInput,
        password: passwordInput,
        saveCredentials: saveCredentials,
        anisetteServer,
      });
      setForceUpdateIds((v) => v + 1);
      toast.success(t("apple_id.logged_in_success"));
    } catch (e) {
      toast.error(err(t("apple_id.login_failed"), e as AppError));
    } finally {
      setSubmitting(false);
    }
  };

  const showLoginForm =
    (loggedInAs === null && storedIds.length === 0) || addAccountOpen;

  return (
    <>
      <div className="panel-heading">
        <span className="panel-glyph" aria-hidden="true">
          <FaUser />
        </span>
        <div>
          <h2 className="panel-title">{t("account.title")}</h2>
          <p className="panel-subtitle">{t("account.subtitle")}</p>
        </div>
      </div>

      {loggedInAs && (
        <div className="account-summary">
          <p className="panel-eyebrow">{t("account.signed_in_as")}</p>
          <div className="account-summary-row">
            <span className="account-email">{loggedInAs}</span>
            <button
              type="button"
              className="btn btn-sm btn-danger"
              onClick={async () => {
                let promise = async () => {
                  await invoke("invalidate_account");
                  setForceUpdateIds((v) => v + 1);
                };
                toast.promise(promise, {
                  loading: t("apple_id.signing_out"),
                  error: (e) => err(t("apple_id.sign_out_failed"), e),
                  success: t("apple_id.signed_out_success"),
                });
              }}
            >
              <FaArrowRightFromBracket aria-hidden="true" />
              {t("apple_id.sign_out")}
            </button>
          </div>
        </div>
      )}

      {showLoginForm && (
        <form className="account-form" onSubmit={submitLogin}>
          <div className="field">
            <span className="field-icon" aria-hidden="true">
              <FaEnvelope />
            </span>
            <input
              className="input"
              type="email"
              autoComplete="username"
              aria-label={t("account.email")}
              placeholder={t("account.email")}
              value={emailInput}
              onChange={(e) => setEmailInput(e.target.value)}
            />
          </div>

          <div className="field">
            <span className="field-icon" aria-hidden="true">
              <FaLock />
            </span>
            <input
              className={`input${showPassword ? "" : " has-trailing"}`}
              type={showPassword ? "text" : "password"}
              autoComplete="current-password"
              aria-label={t("account.password")}
              placeholder={t("account.password")}
              value={passwordInput}
              onChange={(e) => setPasswordInput(e.target.value)}
            />
            <button
              type="button"
              className="icon-button field-toggle"
              aria-label={
                showPassword ? t("account.hide_password") : t("account.show_password")
              }
              onClick={() => setShowPassword((v) => !v)}
            >
              {showPassword ? <FaEyeSlash /> : <FaEye />}
            </button>
          </div>

          {noKeyringAvailable ? (
            <p className="account-warning">{t("apple_id.no_keyring_available")}</p>
          ) : (
            <label className="checkbox-row" htmlFor="save-credentials">
              <input
                type="checkbox"
                id="save-credentials"
                checked={saveCredentials}
                onChange={(e) => setSaveCredentials(e.target.checked)}
              />
              <span>{t("account.save_credentials")}</span>
            </label>
          )}

          <button
            type="submit"
            className="btn btn-primary btn-lg btn-block"
            disabled={submitting}
          >
            {submitting && <span className="button-spinner" aria-hidden="true" />}
            {submitting ? t("apple_id.logging_in") : t("account.submit")}
          </button>

          <p className="account-helper">{t("account.helper")}</p>

          {addAccountOpen && storedIds.length > 0 && (
            <button
              type="button"
              className="btn btn-sm btn-ghost"
              onClick={() => setAddAccountOpen(false)}
            >
              {t("common.cancel")}
            </button>
          )}
        </form>
      )}

      {storedIds.length > 0 && (
        <div className="stored-ids">
          <p className="panel-eyebrow">{t("account.saved_logins")}</p>
          <div className="stored-container">
            {storedIds.map((id) => (
              <div key={id} className="stored">
                <span className="stored-email">{id}</span>
                <div className="row">
                  {!loggedInAs && (
                    <button
                      type="button"
                      className="btn btn-sm"
                      onClick={() => {
                        let promise = async () => {
                          await invoke("login_stored", {
                            email: id,
                            anisetteServer,
                          });
                          setForceUpdateIds((v) => v + 1);
                        };
                        toast.promise(promise, {
                          loading: t("apple_id.logging_in"),
                          success: t("apple_id.logged_in_success"),
                          error: (e) => err(t("apple_id.login_failed"), e),
                        });
                      }}
                    >
                      {t("apple_id.sign_in")}
                    </button>
                  )}
                  <button
                    type="button"
                    className="btn btn-sm btn-danger"
                    onClick={async () => {
                      let promise = async () => {
                        await invoke("delete_account", { email: id });
                        setForceUpdateIds((v) => v + 1);
                      };
                      toast.promise(promise, {
                        loading: t("apple_id.deleting"),
                        error: (e) => err(t("apple_id.deletion_failed"), e),
                        success: t("apple_id.deleted_success"),
                      });
                    }}
                  >
                    {t("common.delete")}
                  </button>
                </div>
              </div>
            ))}
            {!addAccountOpen && (
              <button
                type="button"
                className="btn btn-sm btn-ghost"
                onClick={() => setAddAccountOpen(true)}
              >
                {t("apple_id.add_account")}
              </button>
            )}
          </div>
        </div>
      )}

      <Modal sizeFit isOpen={tfaOpen} zIndex={2000}>
        <h2>{t("apple_id.two_factor_title")}</h2>
        <p>{t("apple_id.two_factor_prompt")}</p>
        <form
          onSubmit={async (e) => {
            e.preventDefault();
            if (tfaCode.length !== 6) {
              toast.warning(t("apple_id.valid_6digit"));
              return;
            }
            await emit("2fa-recieved", tfaCode);
            setTfaOpen(false);
            setTfaCode("");
          }}
        >
          <input
            className="input"
            type="text"
            inputMode="numeric"
            placeholder={t("apple_id.verification_placeholder")}
            value={tfaCode}
            onChange={(e) => setTfaCode(e.target.value)}
          />
          <button type="submit" className="btn btn-primary">
            {t("apple_id.submit")}
          </button>
        </form>
      </Modal>

      <Modal sizeFit isOpen={certs !== null} zIndex={2000}>
        <h2 className="cert-header">{t("apple_id.max_certs_title")}</h2>
        <p className="certs-desc">{t("apple_id.max_certs_desc")}</p>
        <p
          className="certs-see"
          role="button"
          tabIndex={0}
          onClick={() => setChooseCertsOpen((v) => !v)}
        >
          {chooseCertsOpen
            ? t("apple_id.hide_certificate_list")
            : t("apple_id.choose_what_to_revoke")}
        </p>
        {chooseCertsOpen && certs && (
          <div className="certs-list">
            {certs.map((cert) => (
              <div
                key={cert.serialNumber}
                className="cert-item"
                onClick={() => {
                  setSelectedSerials((prev) => {
                    if (prev.includes(cert.serialNumber)) {
                      return prev.filter((s) => s !== cert.serialNumber);
                    }
                    return [...prev, cert.serialNumber];
                  });
                }}
              >
                <input
                  type="checkbox"
                  id={cert.serialNumber}
                  name={cert.serialNumber}
                  value={cert.serialNumber}
                  checked={selectedSerials.includes(cert.serialNumber)}
                />
                <label htmlFor={cert.serialNumber}>
                  {cert.name} - {cert.machineName}
                </label>
              </div>
            ))}
          </div>
        )}
        <div className="certs-buttons">
          <button
            className="btn btn-primary"
            onClick={async () => {
              await emit(
                "max-certs-response",
                selectedSerials.length > 0 ? selectedSerials : null,
              );
              setCerts(null);
              setChooseCertsOpen(false);
            }}
          >
            {t("apple_id.continue")}
          </button>
          <button
            className="btn btn-danger"
            onClick={async () => {
              await emit("max-certs-response", null);
              setCerts(null);
              setChooseCertsOpen(false);
            }}
          >
            {t("common.cancel")}
          </button>
        </div>
      </Modal>
    </>
  );
};
