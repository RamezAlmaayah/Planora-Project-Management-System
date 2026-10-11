# Planora on Vercel

## Deployment strategy

Use Vercel's official **Container Runtime (beta)**, explicitly configured as the
single `planora` service in `vercel.json` with `Dockerfile.vercel` as its entrypoint.
This runs the existing ASP.NET Core MVC application, Razor
views and .NET libraries as an OCI container; no Node adapter or frontend rewrite
is needed. The catch-all rewrite routes all HTTP paths to this container.

Official reference: https://vercel.com/docs/functions/runtimes/container

The image uses the .NET 10 SDK to restore and publish Release for linux-x64, and
the full ASP.NET Core .NET 10 runtime image with glibc and native runtime support
for QuestPDF. It runs as the unprivileged `app` user on `0.0.0.0:$PORT` (8080 by
default). Native PDF generation must still be smoke-tested in the Linux container.
No secrets or development certificates are included. Docker is required to
validate this image locally:

```powershell
docker build -f Dockerfile.vercel -t planora:production .
```

## Vercel project and secure configuration

Create/link `planora-project-management-system` in the account/team selected by
the owner. Connect the existing GitHub repository
`RamezAlmaayah/Planora-Project-Management-System`, use the repository root, choose
Services for Framework Preset, and set the production branch to `main`. Do not add
Node build/output commands. Use Vercel's secure dashboard environment-variable
flow or interactive `vercel env add` prompts for values. Never put secrets in
command arguments, chat, source files, Docker build arguments or Git.

Set these variables for **Production** before deploying:

| Variable | Requirement/default |
| --- | --- |
| `PORT` | `8080`, required to match Vercel's upstream port to the non-root container |
| `ConnectionStrings__DefaultConnection` | Required secret; owner selected Azure SQL Database; actual database/connection not yet supplied |
| `Gemini__ApiKey` | Secret required for Gemini generation |
| `Gemini__Model` | Required for Gemini; choose an available model in the owner's account |
| `Gemini__BaseUrl` | Safe default already supplied: `https://generativelanguage.googleapis.com/v1beta` |
| `Gemini__TimeoutSeconds` | Safe default already supplied: `45` |
| `Email__Host` | For Gmail: `smtp.gmail.com` |
| `Email__Port` | For Gmail with STARTTLS: `587` |
| `Email__UserName` | Owner's SMTP identity, entered securely |
| `Email__Password` | Secret; Gmail app password, entered securely |
| `Email__FromEmail` | Owner's authorized sender address |
| `Email__FromName` | Safe default `Planora` |
| `Email__UseSsl` | `true`; current MailKit implementation uses STARTTLS, not implicit TLS on port 465 |
| `BootstrapAdmin__Enabled` | `false` (already disabled by production config); explicitly enable only for an intentional bootstrap |
| `AllowedHosts` | Recommended: production hostname(s), separated by semicolons |

Production uses standard ASP.NET Core environment-variable configuration.
Development user-secrets are not available in the production container and must
not be copied there. SMTP is necessary for email-confirmed registration and
password recovery; Gemini is necessary for AI features, not public page rendering.
No production credentials have been configured by the preparation work.

If the owner explicitly enables administrator bootstrapping, configure
`BootstrapAdmin__Email`, `BootstrapAdmin__Password` and optionally
`BootstrapAdmin__FullName` securely, then disable bootstrapping after verification.
Demo seeding remains Development-only. Production does not run migrations at
application startup.

## Azure SQL and migrations

An existing or owner-provisioned Azure SQL database is required. Use an encrypted
SQL Server connection with certificate validation (`TrustServerCertificate=False`)
and a least-privilege runtime identity. The app rejects missing connection strings
and common local SQL Server endpoints in Production. No database was provisioned
automatically. Vercel container images currently lack Static IP/Secure Compute
support; agree on a suitable Azure SQL firewall/network policy before release.
Do not assume Azure's "allow Azure services" setting admits Vercel traffic.

Generate an idempotent SQL script from the existing migrations:

```powershell
dotnet ef migrations script --idempotent --project src/Planora.Infrastructure --startup-project src/Planora.Web --output artifacts/planora-migrations.sql
```

Generation needs valid startup configuration, but does not connect to the database.
Review the generated script, back up any existing database, and apply it once
through a controlled release process with a migration identity. Verify migration
history before releasing the application. Never apply migrations concurrently
from autoscaling instances. Do not pass the connection string on the command line.

## Storage and scaling limitations

Container files and memory are **instance-local and ephemeral**. Existing local
development behavior is preserved. No external storage service was chosen.

* Protected task attachments and QA evidence use the existing storage interfaces.
  In the container they write to `/tmp/planora/attachments` and
  `/tmp/planora/qa-evidence`. Their SQL metadata can outlive files; downloads can
  fail after replacement or when routed to another instance. Existing local files
  are excluded from deployment and are not migrated.
* Profile images still write to `wwwroot/uploads/profiles`; they have the same
  durability and cross-instance limitations. Verify serving newly uploaded files
  in a published container before using this feature.
* Data-protection keys default to local storage. Login cookies, antiforgery and
  Identity tokens can become invalid after replacement or on a different instance.
  `DataProtection__KeyRingPath` supports a shared persistent directory on hosts
  that provide one; setting it to a Vercel local directory does **not** make keys
  durable. An external shared, encrypted key repository remains required for
  reliable authentication across instances. No key values are committed.
* Presence/SignalR groups, AI quality cache, SRS draft cache and rate-limit counters
  are in memory. Presence is incomplete across instances; draft/cache state is
  lost on replacement; rate limits are per instance. Durable/shared coordination
  requires a separately agreed implementation.
* Vercel WebSockets are beta and connections have duration limits. The existing
  SignalR client reconnects, but negotiate/fallback transports and cross-instance
  routing must be tested live. No backplane is configured.
* File logging is disabled in Production; logs go to stdout for Vercel observability.
  The application's local admin log reader will not show these platform logs.

This deployment can be evaluated with these limitations, but protected uploads,
long-lived authentication and shared realtime state are not yet production-durable.

## HTTPS and security

Only when Vercel's system environment variable `VERCEL=1` is present, the app
processes one hop of `X-Forwarded-Proto` and `X-Forwarded-For` before HTTPS, HSTS,
authentication and rate limiting. Vercel is the sole ingress and its proxy IPs are
dynamic. Do not set `VERCEL=1` for standalone containers exposed directly to the
internet. Configure explicit trusted proxies for other hosts.

Existing `__Host-` authentication cookies are Secure, HttpOnly, SameSite=Lax with
path `/`; global antiforgery validation and CSP/security headers remain enabled.
Production uses generic exception responses, not development exception pages.
There is no automatic demo seeding or default administrator creation.

## Release verification

```powershell
dotnet build Planora.sln
dotnet test Planora.sln --no-build
dotnet publish src/Planora.Web/Planora.Web.csproj -c Release -r linux-x64 --self-contained false -o artifacts/vercel-publish
```

After secrets, database migrations and port are configured, deploy production,
inspect build/runtime logs without disclosing secrets, and check `/`,
`/Account/Login`, `/Account/Register`, `/Account/ForgotPassword`, `/Home/Contact`,
and a nonexistent route (must return 404). With owner-provided test credentials,
verify login, Dashboard, Projects, email and PDF generation. Do not report success
until the public production URL works. Container build, runtime and public smoke
tests remain pending until the interactive prerequisites are completed.
