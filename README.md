# Planora

Planora is an ASP.NET Core MVC project-management application with Scrum and V-Model workflows. The solution targets .NET 10 and uses SQL Server through Entity Framework Core.

## Local prerequisites

- .NET SDK **10.0.400** (pinned in `global.json`)
- SQL Server LocalDB, SQL Server Express, or another SQL Server instance
- An SMTP server for email confirmation and password recovery
- A Gemini API key to use AI requirement and SRS generation

## Configure local secrets

The Web project uses .NET User Secrets. From the repository root, set the connection string:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=Planora;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project src/Planora.Web
```

Set the SMTP fields for your mail server. For a local SMTP capture tool that listens on `localhost:25` without authentication, configure an appropriate sender address, set SSL off, and leave the username and password empty:

```powershell
dotnet user-secrets set "Email:Host" "localhost" --project src/Planora.Web
dotnet user-secrets set "Email:Port" "25" --project src/Planora.Web
dotnet user-secrets set "Email:FromEmail" "planora@localhost" --project src/Planora.Web
dotnet user-secrets set "Email:FromName" "Planora" --project src/Planora.Web
dotnet user-secrets set "Email:UseSsl" "false" --project src/Planora.Web
```

For local end-to-end email verification and password recovery, install and run [Papercut SMTP](https://github.com/ChangemakerStudios/Papercut-SMTP) (`winget install ChangemakerStudios.Papercut-SMTP`). Its desktop inbox captures mail sent to `localhost:25`; open Papercut to inspect the rendered message. Do not configure a real delivery server for demo accounts.

If the SMTP server requires authentication, set both `Email:UserName` and `Email:Password`. Store passwords and API keys only in User Secrets or another secure secret store, never in committed settings files.

To enable AI features, set the Gemini key and model used by your account:

```powershell
dotnet user-secrets set "Gemini:ApiKey" "YOUR_API_KEY" --project src/Planora.Web
dotnet user-secrets set "Gemini:Model" "YOUR_GEMINI_MODEL" --project src/Planora.Web
```

To provision a local administrator, configure `BootstrapAdmin:Enabled`, `BootstrapAdmin:Email`, `BootstrapAdmin:FullName`, and `BootstrapAdmin:Password` in User Secrets. Use an account you control, and turn bootstrap off after provisioning. The configured password is passed through ASP.NET Core Identity hashing.

## Development demo accounts

After applying migrations, Development startup seeds five clearly marked accounts and a small sample dataset. Set a private password for all demo accounts in User Secrets; it must meet the normal Identity password rules (at least 10 characters, uppercase, lowercase, digit, and symbol):

```powershell
$demoPassword = Read-Host "Choose a private Planora demo password"
dotnet user-secrets set "DevelopmentDemoSeed:Password" $demoPassword --project src/Planora.Web
Remove-Variable demoPassword
```

The password is never stored in the repository and is hashed by ASP.NET Core Identity. Demo emails (all use that configured secret) are:

| Role | Sign-in email |
| --- | --- |
| Admin | `admin.demo@planora.local` |
| Project Manager | `pm.demo@planora.local` |
| Scrum Master | `scrum.demo@planora.local` |
| Developer | `developer.demo@planora.local` |
| QA Tester | `qa.demo@planora.local` |

Seeding is idempotent and runs only when `ASPNETCORE_ENVIRONMENT` is `Development`. It creates a Scrum project with backlog/sprint/task states, comments, an issue and a notification, plus a V-Model project with a requirement traced through design, implementation, verification and validation. If the demo password is not set, the application logs that seeding was skipped.

## Restore, migrate, and run

From the repository root:

```powershell
dotnet restore Planora.sln
dotnet ef database update --project src/Planora.Infrastructure --startup-project src/Planora.Web
dotnet run --project src/Planora.Web
```

The app does not apply migrations automatically at startup. The HTTPS launch profile uses `https://localhost:7063`; trust the .NET development certificate once if needed:

```powershell
dotnet dev-certs https --trust
```

## Email and accounts

Planora sends confirmation and password-recovery messages through the configured SMTP server. Papercut SMTP captures them locally so the email flow can be tested without delivering messages to real inboxes. The database migration creates the application roles; the Development seeder ensures all five exist before creating demo users.

## File attachments

Task attachments are stored under `src/Planora.Web/App_Data/TaskAttachments` by default, outside the static web root. Back up this folder alongside the database if uploaded files need to be retained.

QA review evidence is stored separately under `src/Planora.Web/App_Data/QaEvidence`, also outside the static web root, and remains associated with the individual pass/fail review.
