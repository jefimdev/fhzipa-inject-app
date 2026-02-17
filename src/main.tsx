import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { Toaster } from "sonner";
import { StoreProvider } from "./StoreContext";
import { LogProvider } from "./LogContext";
import { ErrorProvider } from "./ErrorContext";
import { DialogProvider } from "./DialogContext";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <ErrorProvider>
      <DialogProvider>
        <LogProvider>
          <StoreProvider>
            <App />
          </StoreProvider>
        </LogProvider>
      </DialogProvider>
    </ErrorProvider>
    <Toaster richColors expand />
  </React.StrictMode>,
);
