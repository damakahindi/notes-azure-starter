import React from "react";
import ReactDOM from "react-dom/client";
import { MsalProvider } from "@azure/msal-react";
import App from "./App";
import { msal } from "./auth";

async function start() {
  if (msal) {
    await msal.initialize();
    await msal.handleRedirectPromise();
  }
  const app = <App />;
  ReactDOM.createRoot(document.getElementById("root")!).render(
    <React.StrictMode>{msal ? <MsalProvider instance={msal}>{app}</MsalProvider> : app}</React.StrictMode>,
  );
}

void start();
