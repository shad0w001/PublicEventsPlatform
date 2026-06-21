# PublicEventsPlatform

Thesis project — public events platform backend (.NET 9, EF Core, PostgreSQL).

## Prerequisites

- .NET 9 SDK
- Docker Desktop
- Visual Studio 2022 with **Container development tools** workload (optional, for F5 compose)

## Quick start

1. Copy environment file:
   ```powershell
   Copy-Item .env.example .env
   ```
   Edit `.env` and set `POSTGRES_PASSWORD`.

2. Copy WebApi development settings (gitignored — holds Auth0, DB password, Stripe keys):
   ```powershell
   Copy-Item src/WebApi/appsettings.Development.example.json src/WebApi/appsettings.Development.json
   ```
   Edit `src/WebApi/appsettings.Development.json`:
   - Set `ConnectionStrings:DefaultConnection` password to match `.env`
   - Set `Auth0:Domain` and `Auth0:Audience` for your tenant
   - Set `Payments:Stripe:SecretKey` (`sk_test_...` from Stripe Dashboard → Developers → API keys)
   - After running `stripe listen`, set `Payments:Stripe:WebhookSecret` (`whsec_...`)
   - Set `Search:Embeddings:ApiKey` from [Google AI Studio](https://aistudio.google.com/apikey) (Gemini free tier; `gemini-embedding-001`, 1536 dims — see `appsettings.Development.example.json`)

   `appsettings.json` in git documents the config shape; secrets stay in `appsettings.Development.json` only.

3. Start infrastructure (PostgreSQL, Kafka, Kafka UI):
   ```powershell
   docker compose up -d
   ```

4. Run the API (use the **`http`** launch profile in Visual Studio, or CLI):
   ```powershell
   dotnet run --project src/WebApi
   ```

Migrations apply automatically on startup (WebApi retries the DB connection for ~30s first).

To apply migrations manually from the CLI:

```powershell
Set-Location src/Infrastructure
dotnet ef database update --startup-project ../WebApi
```

From the solution root (do **not** use `docker-compose` as the EF project):

```powershell
dotnet ef database update --project src/Infrastructure --startup-project src/WebApi
```

**Visual Studio Package Manager Console:** set **Default project** to `Infrastructure`, **Startup project** to `WebApi` (not `docker-compose`). Then `Update-Database` reads `appsettings.Development.json` — ensure `ConnectionStrings:DefaultConnection` password matches `.env` `POSTGRES_PASSWORD`.

**CLI `dbcontext info`:** same project flags as above; running bare `dotnet ef` from the repo root targets `docker-compose.dcproj` and fails with a circular dependency error.

Swagger UI (Development only): `http://localhost:5168/swagger` — the root URL redirects there when running locally.

## Visual Studio 2022

1. Open `PublicEventsPlatform.sln`
2. Solution → **Properties** → **Multiple startup projects**
3. Set **docker-compose** and **WebApi** to **Start**
4. Press F5

## pgAdmin

| Field | Value |
|-------|-------|
| Host | `localhost` |
| Port | `5433` |
| Database | `public_events_platform` |
| Username | `postgres` |
| Password | from `.env` / `appsettings.Development.json` |

## Docker commands

```powershell
docker compose stop      # stop container, keep data volume
docker compose down      # remove container, keep volume
docker compose down -v   # remove container and wipe data
```

## Ports

| Service | Host port |
|---------|-----------|
| Docker PostgreSQL | 5433 |
| Kafka | 9092 |
| Kafka UI | 8080 |
| WebApi (http profile) | 5168 |

Local Postgres installations can keep using port 5432 independently.

## Kafka UI (Phase 7)

After `docker compose up -d`, open **http://localhost:8080** to browse topics and messages. The WebApi (on the host) uses bootstrap server **`localhost:9092`**.

## Default profile avatar

When Auth0 does not provide a profile picture (e.g. email/password users), new users get `UserProfile:DefaultAvatarUrl` from appsettings (default: `/images/default-avatar.png`). The static file is served from `src/WebApi/wwwroot/images/default-avatar.png`.

## Stripe (test mode, Phase 6)

Backend secrets live in **`appsettings.Development.json`** (gitignored). See `appsettings.Development.example.json` for the template.

| Config key | Source |
|------------|--------|
| `Payments:Stripe:SecretKey` | Stripe Dashboard → Developers → API keys (`sk_test_...`) |
| `Payments:Stripe:WebhookSecret` | Printed when you run `stripe listen` (`whsec_...`; update if you restart the listener) |
| `Payments:Stripe:SuccessUrlBase` / `CancelUrlBase` | SPA routes after Checkout (default `http://localhost:5173/...`) |

**Stripe CLI** (webhook forwarding while the API runs on port **5168**):

```powershell
stripe listen --forward-to localhost:5168/api/webhooks/stripe
```

Keep the CLI running when testing ticket fulfillment. After payment, the CLI should show **200** for `checkout.session.completed` (not 404). Update `Payments:Stripe:WebhookSecret` in Development settings if you restart the listener (new `whsec_…`).

**End-to-end paid flow:** create order via Swagger → pay at `checkoutUrl` with test card `4242…` → webhook fulfills order (tickets + attendance in DB). Success redirect alone does not issue tickets.

**SPA publishable key** (`pk_test_...`): copy `spa.env.example` → `spa.env.local` at the solution root (gitignored). Use in Vite as `VITE_STRIPE_PUBLISHABLE_KEY`. Never commit `pk_test_` or `sk_test_` in tracked files.

Test card: `4242 4242 4242 4242`, any future expiry/CVC — [Stripe testing docs](https://docs.stripe.com/testing).
