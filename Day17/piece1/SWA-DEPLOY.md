# Azure Static Web Apps — Deployment Guide

## Architecture

```
Browser (Angular)
  → calls relative /api/* on SWA
  → SWA routes /api/* to Azure Functions backend (api/)
  → Function acquires MI token via DefaultAzureCredential (no secret)
  → Function proxies request + Bearer token to Container Apps API
  → Container Apps API validates token via EntraScheme (Azure AD)
  → Response flows back to browser
```

**Zero secrets in repo or app settings.** The only secret in CI/CD is
`AZURE_STATIC_WEB_APPS_API_TOKEN` — a deploy token, NOT used for API auth.

---

## Prerequisites

### ⚠️ IMPORTANT: Standard Plan Required

Managed Identity on SWA managed functions requires the **Standard plan** ($9/month).
The Free plan does not support Managed Identity in managed functions.

**Alternative (Free plan):** Use Bring-Your-Own Functions — link a separate Azure Functions
App (with its own Managed Identity) to your SWA. Steps differ from below.

---

## Step 1 — Create the Static Web App (Azure Portal — MANUAL)

1. Go to **Azure Portal** → Create a resource → **Static Web App**
2. Settings:
   - **Resource group:** `thinkschool-rg`
   - **Name:** `quotes-ui`
   - **Plan:** Standard
   - **Region:** Central India
   - **Source:** GitHub → your repo → branch `main`
   - **Build preset:** Angular
   - **App location:** `Day17/piece1/quotes-ui`
   - **Api location:** `Day17/piece1/api`
   - **Output location:** `dist/quotes-ui/browser`
3. Click **Review + create**. Azure will push the GitHub Actions workflow automatically,
   or use the `.github/workflows/azure-static-web-apps.yml` already in the repo.
4. Copy the **deployment token** from the SWA Overview page.

---

## Step 2 — Add GitHub Secret (GitHub — MANUAL)

1. Go to your GitHub repo → **Settings** → **Secrets and variables** → **Actions**
2. Add secret: `AZURE_STATIC_WEB_APPS_API_TOKEN` = paste the token from Step 1
3. Push to `main` — the workflow builds and deploys automatically.

---

## Step 3 — Enable Managed Identity (Azure Portal — MANUAL)

1. In Azure Portal → your SWA → **Identity** (left menu)
2. Turn **System assigned** to **On** → **Save**
3. Copy the **Object (principal) ID**

---

## Step 4 — Grant MI Access to the Container Apps API (Azure Portal — MANUAL)

The Container Apps API (`QuotesApi`) uses Azure AD (`EntraScheme`) with:
- **Tenant ID:** `0a0aa63d-82d0-4ba1-b909-d7986ece4c4c`
- **Client ID:** `cbd99da1-dee1-4a9c-9f82-16ffc5bb486e`

You need to authorize the SWA's MI to obtain a token for this API:

1. Go to **Azure Portal** → **App registrations** → find the App Registration for
   `cbd99da1-dee1-4a9c-9f82-16ffc5bb486e`
2. Under **Expose an API** → confirm App ID URI is set
   (e.g. `api://cbd99da1-dee1-4a9c-9f82-16ffc5bb486e`)
3. Under **App roles** (or Manifest) → add a role the MI will be assigned to
4. Go to **Enterprise applications** → find your SWA's Managed Identity (by Object ID)
   → **Permissions** → **Grant admin consent** for the API scope

**What this enables:** The SWA backend function calls
`credential.getToken('cbd99da1-dee1-4a9c-9f82-16ffc5bb486e/.default')` and gets a
token with `aud: cbd99da1-dee1-4a9c-9f82-16ffc5bb486e`, which passes the Container
Apps API's `ValidAudience` check — **no secret involved**.

---

## Step 5 — Add App Settings to SWA (Azure Portal — OPTIONAL)

If you need to override the backend URL or client ID, add these in
**SWA → Configuration → Application settings** (NOT secrets):

| Name | Value |
|---|---|
| `BACKEND_API_URL` | `https://quotes-api.happyflower-7fa5126b.centralindia.azurecontainerapps.io` |
| `BACKEND_CLIENT_ID` | `cbd99da1-dee1-4a9c-9f82-16ffc5bb486e` |

These are non-sensitive public identifiers. If not set, the function falls back to
the hardcoded defaults.

---

## Step 6 — Custom Domain (Azure Portal — MANUAL)

1. Go to your SWA → **Custom domains** → **Add**
2. Enter your domain (e.g. `quotes.yourdomain.com`)
3. Azure gives you a DNS record to add:

### DNS Records

| Type | Name | Value |
|---|---|---|
| `CNAME` | `quotes` | `<your-swa>.azurestaticapps.net` |
| `TXT` | `_dnsauth.quotes` | `<validation-token-from-azure>` |

4. Add both records in your DNS provider (GoDaddy, Cloudflare, Namecheap, etc.)
5. Wait for DNS propagation (up to 48h, usually minutes on Cloudflare)
6. Back in Azure → click **Validate** → **Add**
7. HTTPS is provisioned automatically — no certificate management needed

---

## Verification Checklist

- [ ] `https://<swa-url>/health` → `{"status":"healthy"}`
- [ ] `https://<swa-url>/api/quotes?page=1&size=10` → returns quotes array
- [ ] No `Authorization` header in Angular browser requests (all auth is backend-only)
- [ ] `Authorization: Bearer <token>` IS present in function → Container Apps calls
- [ ] Token `aud` claim = `cbd99da1-dee1-4a9c-9f82-16ffc5bb486e` (no secret)
- [ ] Lighthouse score ≥ 95 (run in Chrome DevTools → Lighthouse)
- [ ] No secrets in repo: `git grep -i "secret\|password\|key" -- "*.ts" "*.js" "*.json"`

---

## What Breaks If the API Changes

| Change | Impact |
|---|---|
| Container Apps URL changes | Update `BACKEND_API_URL` in SWA app settings — no redeploy |
| `ClientId` changes | Update `BACKEND_CLIENT_ID` in SWA app settings + re-grant MI access |
| `/api/quotes` field names change (`id`, `author`, `text`, `createdAt`) | Angular `Quote` model breaks — update `quote.model.ts` |
| API adds auth requirement to GET endpoints | SWA function already passes MI token — no change needed |
| Container Apps API auth switches away from EntraScheme | MI token no longer accepted — must re-wire auth |
