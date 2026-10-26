import { PublicClientApplication } from "@azure/msal-browser";

const clientId = import.meta.env.VITE_AAD_CLIENT_ID as string | undefined;
const tenantId = (import.meta.env.VITE_AAD_TENANT_ID as string | undefined) ?? "common";
const apiScope = import.meta.env.VITE_API_SCOPE as string | undefined;

// Sign-in is optional: without a client id the UI talks to the API anonymously (local dev).
export const authEnabled = Boolean(clientId && apiScope);

export const msal = authEnabled
  ? new PublicClientApplication({
      auth: {
        clientId: clientId!,
        authority: `https://login.microsoftonline.com/${tenantId}`,
        redirectUri: window.location.origin,
      },
      cache: { cacheLocation: "sessionStorage" },
    })
  : null;

export const loginRequest = { scopes: apiScope ? [apiScope] : [] };

export async function getAccessToken(): Promise<string | null> {
  if (!msal) return null;
  const account = msal.getActiveAccount() ?? msal.getAllAccounts()[0];
  if (!account) return null;
  try {
    const result = await msal.acquireTokenSilent({ ...loginRequest, account });
    return result.accessToken;
  } catch {
    await msal.acquireTokenRedirect(loginRequest);
    return null;
  }
}
