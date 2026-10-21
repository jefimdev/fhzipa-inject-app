import React from "react";
import ReactDOM from "react-dom/client";
import App from "./app-principal";
import { Toaster } from "sonner";
import { StoreProvider } from "./contextos/loja";
import { LogProvider } from "./contextos/log";
import { ErrorProvider } from "./contextos/erro";
import { DialogProvider } from "./contextos/dialogo";
import "./configuracoes/i18n";
import { PlatformProvider } from "./contextos/plataforma";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <PlatformProvider>
      <StoreProvider>
        <ErrorProvider>
          <DialogProvider>
            <LogProvider>
              <App />
            </LogProvider>
          </DialogProvider>
        </ErrorProvider>
      </StoreProvider>
    </PlatformProvider>
    <Toaster richColors expand />
  </React.StrictMode>,
);
