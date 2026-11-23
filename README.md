# Petty Cash (Phases 1–2)

Petty cash request capture, policy enforcement (R2,000 per request, R10,000 float), hierarchical approval with optional Microsoft Graph leave rerouting, and a Nuxt 3 staff portal.

## Prerequisites

- .NET 9 SDK
- Node.js 22+ (or 24+)
- Docker Desktop (PostgreSQL)

## Quick start

Default setup uses a **dedicated dev PostgreSQL** in `.postgres/` on **port 5433** (separate from your Windows PostgreSQL on 5432). No Docker required.

```powershell
# From repo root — PostgreSQL on 5433 starts automatically when you run the API
dotnet ef database update --project src/PettyCash.Infrastructure --startup-project src/PettyCash.Api
dotnet run --project src/PettyCash.Api --launch-profile dev
```

Or: `powershell -ExecutionPolicy Bypass -File .\scripts\run-api.ps1` (same as above).

To start Postgres only: `.\scripts\init-dev-postgres.ps1`

Credentials: user `pettycash`, password `pettycash_dev`, database `pettycash`, port **5433**.

**Docker (optional):** `docker compose up -d` also exposes Postgres on 5433 if you prefer a container.

**Shared PostgreSQL on 5432:** run `.\scripts\setup-local-postgres.ps1 -PostgresPassword "..."` and set `Port=5432` in `appsettings.Development.json`.

### `password authentication failed for user "pettycash"`

That usually means EF Core reached **port 5432** (your installed PostgreSQL), not the Docker database. The Docker user `pettycash` / `pettycash_dev` only exists in the compose container.

1. Start **Docker Desktop**, then run `docker compose up -d` from the repo root.
2. Confirm the connection string uses **port 5433** in `src/PettyCash.Api/appsettings.Development.json`.
3. Run migrations from the **repo root** with the Api startup project (not only from `PettyCash.Infrastructure`):

```powershell
dotnet ef database update --project src/PettyCash.Infrastructure --startup-project src/PettyCash.Api
```

**Alternative:** use your existing PostgreSQL on 5432 — run `scripts/setup-postgres-on-local-5432.sql` as a superuser, then set `Port=5432` in `DefaultConnection`.

On first API start, the app migrates the database and seeds **129 staff accounts** from `src/PettyCash.Infrastructure/Seed/staff-directory.json`. In Development only, temporary passwords are written once to `secrets/staff-temporary-passwords.xlsx`. That folder is gitignored. Production never writes that file. Usernames are `firstname.lastname` (lowercase). Every account must change its password on first sign-in.

Frontend (requires **Node 22 LTS or 24 LTS** — Node 25 is not supported by Nuxt 4):

```powershell
# Install Node 22 (not npm — Node is separate from project packages):
powershell -ExecutionPolicy Bypass -File .\scripts\install-node-22.ps1
# Open a NEW terminal, then:
node -v          # must show v22.x (not v25)
cd frontend
npm install      # installs Nuxt/Vue dependencies only
npm run dev
```

If `node -v` still shows 25, remove `C:\Program Files\node-v25.1.0-win-x64` from your user/system **Path** in Windows settings and open a new terminal.

If you see repeated **`GET /dev-sw.js` 503** while the app is still compiling, wait until Nuxt finishes the first build. If it persists after the app loads, in Chrome DevTools → **Application** → **Service workers**, unregister workers for `localhost:3000` (leftover from another project).

Open `http://localhost:3000`, sign in, and change the password when prompted.

## Configuration

| Setting | Location | Purpose |
|--------|----------|---------|
| PostgreSQL | `appsettings.Development.json` locally; `ConnectionStrings__DefaultConnection` in production | Database |
| JWT | `Jwt:Key` in Development json locally; `Jwt__Key` in production (32+ bytes, not the dev key) | Signs the session cookie |
| Portal proxy | `NUXT_PROXY_SECRET` on Vercel and `Proxy__Secret` on Render, same value | Stops the public internet calling the API directly |
| Microsoft sign-in | `NUXT_MICROSOFT_*` on Vercel, `Auth__Microsoft__*` on Render (same tenant and client id) | Staff sign in with FoodBev Microsoft 365 (MFA stays in Microsoft) |
| CORS | `Cors__Origins__0` | HTTPS Vercel origin |
| Graph (optional) | `Graph__TenantId`, `Graph__ClientId`, `Graph__ClientSecret` | Leave checks when approvers have `EntraUpn` set |
| Email (optional) | `Email__*` environment variables | Status emails and finance form copies (see below) |

Local development values stay in `appsettings.Development.json`. Do not put production passwords, the JWT signing key, or Graph secrets in `appsettings.json`.

## Production (Vercel + Render)

The browser talks only to the Nuxt app. Nuxt holds the session in an HttpOnly cookie and calls the API with a server-side bearer token. Render and Vercel both terminate TLS.

1. In Render, create a Blueprint from `render.yaml`. That creates Postgres and the API, with a disk for uploads and mailbox connections.
2. Register a **separate** Entra app for sign-in (keep the Graph mail app as-is): SPA platform, redirect URIs `https://your-app.vercel.app/auth/microsoft/callback` and `http://localhost:3000/auth/microsoft/callback`, scopes `openid profile email`. Set Render `Auth__Microsoft__TenantId` and `Auth__Microsoft__ClientId`, and the same two values on Vercel as `NUXT_MICROSOFT_TENANT_ID` and `NUXT_MICROSOFT_CLIENT_ID`.
3. Set `Cors__Origins__0` and `Email__PublicAppBaseUrl` to the Vercel URL, for example `https://your-app.vercel.app`.
4. Copy Render's generated `Proxy__Secret` into the Vercel project as `NUXT_PROXY_SECRET` (at least 32 characters, the same value on both sides).
5. Set Vercel `NUXT_API_BASE` to the Render HTTPS URL, for example `https://pettycash-api.onrender.com`. Do not set `NUXT_PUBLIC_API_BASE`.
6. On Vercel, set the root directory to `frontend`. The project includes `frontend/vercel.json`.

Production API startup **requires** Microsoft sign-in settings. Email/password login stays available locally only when `Auth:Microsoft` is not set. `Seed__InitialPassword` is optional in production when Microsoft sign-in is configured (new staff rows get an internal random password and sign in through Microsoft).

The API adds SSL to the Render database URL when that URL does not already require it. `Jwt__Key` must be at least 32 bytes. If Render's generated value is rejected at startup, replace it with a longer random string. Leave `Graph__TenantId`, `Graph__ClientId`, and `Graph__ClientSecret` empty until Microsoft 365 mail should send. People connect a mailbox again under Account after the first production sign-in.

### Microsoft Graph (leave routing)

1. Register an app in Microsoft Entra ID.
2. Add application permissions: `User.Read.All`, `Calendars.Read`, `MailboxSettings.Read`, and grant admin consent.
3. Create a client secret and set tenant ID, client ID, and secret with environment variables or user secrets. Do not commit them.
4. Link each approver’s Entra UPN on their user record (admin tooling or direct DB update) so Graph can be queried.

If Graph is not configured, approvers without a UPN are treated as available and routing uses the primary approver.

### Email notifications

When SMTP is enabled, the app sends updates at each workflow step:

| Event | Recipients |
|--------|------------|
| Requester submits (signed form) | Requester + assigned department senior manager |
| Department SM approves | Requester + Finance senior manager + **Boitumelo Moripe** & **Busisiwe Ntuli** (Word form attached) |
| Finance SM approves | Requester |
| Rejected | Requester |

Configure `Email` with environment variables or **user secrets**. In Development, until SMTP or Graph is enabled, messages are saved as `.eml` files under `secrets/email-outbox` (open in Outlook to preview). In production those messages are not written to disk and are not delivered until SMTP or Graph is set.

**Option A — Office 365 SMTP (simplest for pilot)**

```powershell
.\scripts\configure-email-smtp.ps1 -SmtpPassword "YOUR_APP_PASSWORD_OR_SMTP_PASSWORD"
dotnet run --project src/PettyCash.Api --launch-profile dev
```

On API startup, look for: `Petty cash email transport: SMTP (real inbox delivery)`.

**Option B — Microsoft Graph**

Set `Graph:TenantId`, `Graph:ClientId`, `Graph:ClientSecret` (same as leave checks), add application permission **Mail.Send** (admin consent), then:

```json
"Email": { "UseMicrosoftGraph": true, "GraphSenderUpn": "thabang@foodbev.co.za" }
```

Use `RedirectAllTo` in dev to capture all mail in one inbox. `PublicAppBaseUrl` must match your Nuxt URL so links to the form work.

Recipients use `EntraUpn` when set, otherwise FoodBev format **`{firstname}{firstLetterOfSurname}@foodbev.co.za`** from the portal username (`firstname.surname`), e.g. `mpumelelo.qomiso` → `mpumeleloq@foodbev.co.za`. Exceptions go in `Email:AddressOverrides` (e.g. `thabang.motswenyane` → `thabang@foodbev.co.za`).

`RedirectAllTo` is empty, so each notification is addressed to that person’s own mailbox. Set it only when you want every message copied to one inbox for a test. Without SMTP or Graph, Development saves copies as `.eml` files in `secrets/email-outbox`. Production does not write those files.

## Build while the API is running

If `dotnet build` fails with **MSB3027** / file locked by **PettyCash.Api**, stop the API first (Ctrl+C in that terminal), or run:

```powershell
.\scripts\stop-dev.ps1
dotnet build
# or
.\scripts\build.ps1
```

## Tests

```powershell
dotnet test
```

## Solution layout

- `src/PettyCash.Domain` — entities and policy constants
- `src/PettyCash.Application` — interfaces and DTOs
- `src/PettyCash.Infrastructure` — EF Core, Identity, Graph, seed
- `src/PettyCash.Api` — HTTP API
- `frontend` — Nuxt 3 portal
- `tests/PettyCash.Tests` — policy, float, and routing tests
