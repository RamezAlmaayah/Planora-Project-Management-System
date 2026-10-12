# Production hardening acceptance record

Status: **not complete**. Local checks pass; live production acceptance is blocked
by connection resets from the audit environment. Do not equate Vercel READY with
successful live authentication or visual acceptance.

## dynv6 custom-domain acceptance — 2026-10-12 (Asia/Amman)

Status: **blocked; not accepted**. `planora.dynv6.net` is attached to the existing
`planora-project-management-system` Vercel project. Ownership is **Verified**,
but Vercel reports **Invalid Configuration** and sees no A records. No ownership
TXT challenge is currently required.

After the reported DNS save, Cloudflare (`1.1.1.1`), Google (`8.8.8.8`),
`ns1.dynv6.com`, and `ns2.dynv6.net` all returned NXDOMAIN for the exact hostname.
The required root A values returned by Vercel remain `216.198.79.1` and
`64.29.17.1`. Direct HTTPS fails at DNS lookup before TLS or an HTTP request.
Only Wi-Fi was active; no connected system VPN adapter was observed.

MonsterASP production SQL connectivity was rechecked successfully with encrypted
SQL authentication. The provider connection continues to skip TLS certificate
validation. No migrations, seeding, account resets, application code changes,
DNS updaters, forwarding, or additional projects were introduced.

HTTPS issuance, normal public/auth POST flows, Admin login, Dashboard reload,
Admin Users, logout, 404, live assets and responsive acceptance remain unverified
on this hostname. Resolve the authoritative DNS result before claiming production
acceptance. GitHub authentication was verified as `RamezAlmaayah`; synchronizing
the previously tested source commits does not establish successful live acceptance.

## Custom-domain acceptance — 2026-10-12 (Asia/Amman)

Status: **blocked; not accepted**. The registration was not merged. Official
is-a.dev registration PR [#56135](https://github.com/is-a-dev/register/pull/56135)
is closed with `merged = false`. The review bot reports denial as "Not Related
to Software Development". No registration request was submitted or reopened by
this verification pass.

- Current DNS, Cloudflare, and the authoritative `ns1.is-a.dev` resolve
  `planora.is-a.dev` to `104.18.4.103` / `104.18.5.103`, not the requested Vercel
  records. The `_vercel.planora.is-a.dev` TXT verification record is absent.
- HTTPS certificate validation succeeds for the currently served is-a.dev
  endpoint, but it responds HTTP 302 to
  `https://is-a.dev/available?d=planora`. This is not Planora acceptance or proof
  that a Vercel certificate has been issued for the custom domain.
- Vercel reports **Invalid Configuration**, ownership verification pending.
  The hostname remains attached to the existing
  `planora-project-management-system` project; no new project was created.
- Only Wi-Fi was active during the direct curl check; no system VPN adapter was
  connected. The placeholder endpoint is reachable, but bypass of filtering for
  the actual Planora application remains unverified.
- MonsterASP production SQL connectivity passes using encrypted SQL
  authentication. The provider connection still skips TLS certificate validation.
  No migrations, account resets, or seeding were performed.
- Public/authenticated route checks, Admin login, Dashboard reload, logout,
  production assets and responsive acceptance cannot be tested against this
  hostname while it points to the registration placeholder.
- Local GitHub authentication is working as `RamezAlmaayah`. Pending Planora
  commits have not been pushed because the requested prerequisite of successful
  live verification has not been satisfied.

Resolve the denied registration with is-a.dev manually (or choose another
hostname/provider), then repeat DNS, ownership, HTTPS and live acceptance checks.

## Confirmed issues and fixes

- Auth desktop minimum grid tracks totaled 1170px and later `!important` rules
  overrode the existing breakpoints. At 1024px the Login form and Register showcase
  were clipped inside the shell. Auth-only fluid tracks and a 1100px single-panel
  breakpoint fix this; mobile padding and touch targets remain scoped to auth.
- Late public-header spacing rules overflowed the 360px viewport by 4px. Scoped
  mobile header spacing, logo size, and wrapping keep its existing links usable.
- Dashboard used four minimum 245px board tracks at 1280px despite its narrower
  workspace. The existing responsive board now uses two bounded columns below 1500px.
- Auth SVGs depended solely on stylesheet dimensions. Explicit 24px HTML dimensions
  provide bounded fallback rendering without global SVG CSS rules. Existing CSS
  continues to set each icon's intended size. The reported live Register asset
  failure has not been reproduced against production and its root cause is unverified.
- Auth page history restoration could retain outgoing animation/submission state.
  A scoped external script clears it on pageshow and prevents repeat valid submissions.

An initial empty-CSS response was caused by the isolated Kestrel test host lacking
its static-web-assets provider. Correcting the test host restored the full CSS.
No application middleware change was retained for that fixture problem.

## Verification

- `dotnet build Planora.sln`: 0 warnings, 0 errors.
- `dotnet test Planora.sln --no-build`: 572 tests passed.
- Browser auth regressions: 47 passed, covering 1440, 1280, 1024, 768, 430,
  390, 375, 360 and 844x390 landscape.
- Local rendered fixture audit: 38 routes, 342 viewport checks, no detected document
  horizontal overflow, no oversized auth icons, no failed asset responses.
- Literal Razor asset paths: 39 checked, no missing files or case mismatches.
- Auth integration lifecycle: registration, pending confirmation, confirmation link,
  duplicate handling, recovery enumeration protection, password reset, reset-link
  replay rejection, login, Dashboard reload and logout passed. Mail is captured in
  tests; this does not verify production SMTP delivery.
- Existing cross-instance tests verify shared encrypted keys, all three auth POST
  forms, and cookie authentication after host replacement. Antiforgery and cookie
  protections are unchanged.
- GET 403/404 status preservation and POST antiforgery 400 preservation pass.

The rendered routes include landing, auth/result pages, Contact, 403, 404, Dashboard,
Projects/details/create, Members, Backlog, Sprints, Board, Tasks/details/create,
Issues/create, Requirements, Design, Implementation, Testing, Validation,
Traceability, AI generator, SRS, Notifications, Profile, Admin Users, Activity/
Presence and Logs. Fixture tables and empty states are covered; this is not an
exhaustive audit of every data-dependent state, modal or role combination.
Contact currently provides contact links, not a POST form.

## Remaining live acceptance

Verify production Login, Dashboard reload, Logout, Register, confirmation email,
Forgot Password, email delivery, Reset Password, invalid tokens, static-asset bodies
and MIME types, and the listed desktop/mobile widths. Check runtime diagnostics for
antiforgery errors and shared-key use. The production Register icon regression
remains unverified until the live page and its asset responses can be inspected.

Local uploads/evidence/profile images and process-local caches/presence still have
the deployment limitations documented in `VERCEL_DEPLOYMENT.md`.
