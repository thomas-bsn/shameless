import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import "../index.css";
import { WizardApp } from "./wizard-app";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <WizardApp />
  </StrictMode>,
);
