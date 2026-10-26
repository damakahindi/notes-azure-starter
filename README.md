# notes-azure-starter

A minimal full-stack starter on Azure: **UI + REST API + NoSQL**, hosted the way a small team would run it in production.

```
 CLIENT            |  EDGE            |  SERVER             |  DATA
 ------------------+------------------+---------------------+-----------------
 Browser           |  HTTPS / DNS     |  App Service (API)  |  Cosmos DB
 React UI (Static  |  (Azure DNS,     |  .NET 10 minimal    |  (serverless,
 Web Apps)         |   custom domain) |  API + JWT auth     |   partition=userId)
                   |                  |  Managed identity --+-> RBAC, no secrets
                                      |  Logs -> App Insights / Log Analytics
```

## What is in here

| Path | What |
|------|------|
| `src/Notes.Api` | .NET 10 REST API (`/api/notes` CRUD, `/health`), Cosmos or in-memory store |
| `tests/Notes.Api.Tests` | xUnit tests |
| `ui` | React + Vite + TypeScript notes UI |
| `infra/main.bicep` | Log Analytics, App Insights, Cosmos (serverless), managed identity, App Service plan + API, Static Web App |
| `.github/workflows` | `ci.yml` (build/test), `deploy.yml` (manual deploy) |

## Hosting pattern

- One App Service plan, one web app per service.
- Managed identity reaches the database with RBAC, so there are no connection strings.
- HTTPS only, TLS 1.2, FTP disabled, health check at `/health`.
- Optional `staging` slot when the plan is S1 or higher, for swap-based releases.
- One Log Analytics workspace per environment; App Insights for traces and metrics.
- Infrastructure as code (Bicep), one parameter file per environment.

## Run locally (no Azure needed)

```bash
dotnet run --project src/Notes.Api --urls http://localhost:5080   # in-memory store
cd ui && npm install && npm run dev                              # http://localhost:5173
```

Without `Cosmos:Endpoint` configured the API uses an in-memory repository. Set `Cors:AllowedOrigins` if you use a different UI port.

## Deploy to Azure

```bash
az group create -n rg-notes-dev -l westeurope
az deployment group create -g rg-notes-dev -f infra/main.bicep -p infra/main.bicepparam
dotnet publish src/Notes.Api -c Release -o out/api
# zip and deploy: az webapp deploy -g rg-notes-dev -n <api-app-name> --src-path out/api.zip --type zip
```

Build the UI with `VITE_API_URL=<apiUrl output>` and upload it to the Static Web App.

## Add sign-in (SSO)

1. Register an app in Microsoft Entra ID (or another OIDC provider) for the API and one for the SPA.
2. Set `Auth:Enabled=true` plus the `AzureAd` section (`Instance`, `TenantId`, `ClientId`, `Audience`).
3. In the UI add `@azure/msal-react`, sign in with PKCE, and send the access token as `Authorization: Bearer`.
4. The API takes the user id from the `oid`/`sub` claim, which is also the Cosmos partition key.

## Cost notes

Cosmos serverless and Static Web Apps Free cost close to nothing when idle. The App Service plan (B1) is the main cost. Delete the resource group after demos: `az group delete -n rg-notes-dev`.

## Next steps to grow it

Add environments with separate resource groups, protect `main` with PR checks, add feature flags, a queue plus a search service (functional partitioning), and a CDN in front of the UI.
