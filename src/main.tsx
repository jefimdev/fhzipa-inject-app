import React from "react";
import ReactDOM from "react-dom/client";
import App from "./App";
import { Toaster } from "sonner";
import { StoreProvider } from "./StoreContext";
import { LogProvider } from "./LogContext";

ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <LogProvider>
      <StoreProvider>
        <App />
      </StoreProvider>
    </LogProvider>
    <Toaster richColors expand />
  </React.StrictMode>
);
