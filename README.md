# Planora – Project Management System

Planora is a full-stack project management platform that supports Scrum/Agile and V-Model workflows with AI-assisted requirement generation, Software Requirements Specification (SRS) generation, QA workflows, traceability, role-based access control (RBAC), and secure project collaboration.

## Overview

Planora brings planning, delivery, testing, and requirements documentation into one project workspace. Teams can organize iterative work through Scrum or follow a structured V-Model lifecycle with links between requirements, design, implementation, and testing.

This graduation-project and software engineering portfolio system demonstrates layered architecture, project-scoped authorization, human-reviewed AI generation, and automated testing. Google Gemini requests run on the server. Access to project data and actions is checked against roles, project membership, and workflow rules.

## Key Features

### Project Management

- Projects, lifecycle management, team members, and project roles.
- Dashboard with project progress and task activity.
- Notifications and activity tracking.

### Scrum

- Product backlog and sprint planning.
- Tasks and a Kanban board with authorized status transitions.
- Developer → QA workflow with pass/fail reviews and QA evidence.
- Issues, task comments, and attachments.

### V-Model

- Functional and non-functional requirements with dependencies.
- Design artifacts and implementation artifacts.
- Test cases, test executions, and phase validation.
- Requirement traceability and coverage states, including missing artifacts, verification results, and validation status.

### AI Features

- Input quality analysis and interactive refinement questions.
- AI requirement generation with human review and explicit save of selected requirements.
- AI-assisted SRS generation from approved requirements.
- SRS preview, editing, saved snapshots, and TXT/PDF exports.

AI generation is available for V-Model projects to Admins and Project Managers with the matching project role. Other project members can access permitted read-only views.

### Administration

- User management, global roles, project membership, and project roles.
- Disable/enable users and guarded user deletion: accounts with project or historical records must be disabled instead of permanently deleted.
- Audit activity, user presence, and system log browsing.

## Roles

| Role | Responsibility |
| --- | --- |
| Admin | Manages users, global access, system activity, and logs; has administrative project access. |
| Project Manager | Manages projects and membership, coordinates requirements and delivery, and uses AI documentation tools. |
| Scrum Master | Manages the backlog and sprints and coordinates Scrum planning and delivery. |
| Developer | Works on assigned tasks and implementation artifacts and submits work for review. |
| QA Tester | Reviews Scrum tasks, records QA evidence, and performs V-Model testing and validation. |

Global roles and project roles are distinct. Project membership, role checks, and workflow state determine which actions are available.

## Technology Stack

| Area | Technologies |
| --- | --- |
| Backend | ASP.NET Core MVC (.NET 10), C#, Entity Framework Core, SQL Server |
| Identity and collaboration | ASP.NET Core Identity, cookie authentication, SignalR for administrative user presence |
| Logging, documents, email | Serilog, QuestPDF, MailKit |
| Frontend | Razor Views, HTML5, CSS3, JavaScript ES Modules, Fetch/AJAX, native HTML drag-and-drop, custom dashboard charts |
| AI | Google Gemini API through a server-side client |
| Testing | xUnit unit, integration, and security test projects |

## Architecture

Planora follows Onion/Clean Architecture principles, with domain models at the center and application contracts separating business operations from infrastructure.

| Project | Purpose |
| --- | --- |
| `Planora.Domain` | Entities, enums, and domain models. |
| `Planora.Application` | Service abstractions, operation models, and shared policies. |
| `Planora.Infrastructure` | Service implementations, EF Core persistence, Identity, Gemini, SMTP, and file storage. |
| `Planora.Web` | MVC controllers, Razor Views, authorization handlers, and application composition. |

Compile-time references point inward:

```text
Planora.Application    → Planora.Domain
Planora.Infrastructure → Planora.Application + Planora.Domain
Planora.Web            → Planora.Application + Planora.Infrastructure
```

The Web composition root registers Infrastructure implementations behind Application interfaces. Domain has no project dependency on the outer layers.

## Main Workflows

**Scrum**

```text
Backlog → Sprint → Task → Developer → In Review → QA
                                                  ├─ Pass → Done
                                                  └─ Fail → Back to Development
```

**V-Model**

```text
Requirement → Design → Implementation → Test Case → Execution → Validation
      └────────────────── Traceability and coverage ──────────────────┘
```

Traceability connects the lifecycle artifacts; coverage reflects missing links and verification/validation outcomes.

**AI requirements**

```text
Input Quality Analysis → Refinement → Generate Requirements → Review → Explicit Save
```

**SRS**

```text
Approved Requirements → Generate → Preview → Edit → Save → TXT / PDF
```

Generated drafts require review and explicit save before becoming project requirements or saved SRS documents.

## Security

- Project-scoped authorization combines roles, membership, and workflow rules.
- Automatic antiforgery validation protects state-changing MVC requests against CSRF.
- Razor output encoding and text-based client rendering support XSS-safe display of user content.
- Authentication cookies use `HttpOnly`, `Secure`, and `SameSite` settings; Identity provides password hashing, lockout, and email confirmation.
- Rate limiting is configured for authentication, password recovery, and AI requirement generation endpoints.
- File uploads undergo type, size, and content validation; ZIP inspection rejects unsafe archive contents.
- Attachments and QA evidence are stored outside the static web root and served through protected downloads.
- Local secrets use .NET User Secrets. Gemini API keys stay on the server and are not exposed to the browser.

These are implemented security patterns, not a claim of formal security certification.

Task attachments default to `src/Planora.Web/App_Data/TaskAttachments`; QA evidence is stored separately under `src/Planora.Web/App_Data/QaEvidence`. Back up uploaded files alongside the database.

## Email

Planora sends branded HTML **Confirm Email** and **Reset Password** messages through SMTP using MailKit. Templates include inline styling and Gmail-compatible CID-linked illustrations embedded in the message.

For local development, an SMTP capture tool such as Papercut SMTP can receive messages without sending them to real inboxes. Configure the sender, host, port, and SSL settings for your chosen SMTP server.

## Demo Data

After migrations are applied, Development startup can seed five demo accounts and sample Scrum/V-Model data. Seeding is idempotent, runs only in `Development`, and is skipped when `DevelopmentDemoSeed:Password` is not configured.

| Role | Demo email |
| --- | --- |
| Admin | `admin.demo@planora.local` |
| Project Manager | `pm.demo@planora.local` |
| Scrum Master | `scrum.demo@planora.local` |
| Developer | `developer.demo@planora.local` |
| QA Tester | `qa.demo@planora.local` |

Set a private **Development-only** demo password in User Secrets. It must contain at least 10 characters with uppercase, lowercase, a digit, and a symbol. No demo password is published here.

```powershell
dotnet user-secrets set "DevelopmentDemoSeed:Password" "<development-only-password>" --project "src/Planora.Web/Planora.Web.csproj"
```

## Local Setup

### 1. Requirements

- .NET SDK **10.0.400**, pinned by `global.json`.
- SQL Server LocalDB, SQL Server Express, or another SQL Server instance.
- EF Core CLI **10.0.11** (`dotnet-ef`) for migrations.
- An SMTP server or local SMTP capture tool for account email flows.
- A Google Gemini API key and a supported model to enable AI features.

If the EF CLI is not installed:

```powershell
dotnet tool install --global dotnet-ef --version 10.0.11
```

### 2. Clone repository

```powershell
git clone https://github.com/RamezAlmaayah/Planora-Project-Management-System.git
cd Planora-Project-Management-System
```

### 3. Restore

```powershell
dotnet restore Planora.sln
```

### 4. Configure User Secrets

Run these commands from the repository root, replacing placeholders with your local configuration:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Gemini:ApiKey" "<api-key>" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Gemini:Model" "<supported-gemini-model>" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Email:Host" "<smtp-host>" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Email:Port" "<smtp-port>" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Email:FromEmail" "<sender-email>" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Email:FromName" "Planora" --project "src/Planora.Web/Planora.Web.csproj"
dotnet user-secrets set "Email:UseSsl" "<true-or-false>" --project "src/Planora.Web/Planora.Web.csproj"
```

For authenticated SMTP, also set `Email:UserName` and `Email:Password` using placeholders and the same `--project` argument. For local demo sign-in, configure the Development-only password described above.

To provision your own administrator, configure `BootstrapAdmin:Enabled`, `BootstrapAdmin:Email`, `BootstrapAdmin:FullName`, and `BootstrapAdmin:Password` in User Secrets. Turn bootstrap off after provisioning. Keep credentials out of committed settings files.

### 5. Database migration

```powershell
dotnet ef database update --project "src/Planora.Infrastructure/Planora.Infrastructure.csproj" --startup-project "src/Planora.Web/Planora.Web.csproj"
```

The application does not automatically apply database migrations at startup.

### 6. Run application

Trust the local HTTPS development certificate once if needed:

```powershell
dotnet dev-certs https --trust
```

Then run the HTTPS profile below. It sets `ASPNETCORE_ENVIRONMENT` to `Development`.

## Run

```powershell
dotnet run --project "src/Planora.Web/Planora.Web.csproj" --launch-profile https
```

Local URL: **https://localhost:7063/**

## Testing

```powershell
dotnet build Planora.sln
dotnet test Planora.sln --no-build
```

Verified on **4 October 2026**:

- **Build:** succeeded with **0 warnings / 0 errors**.
- **Tests:** **561/561 passed**, with 0 failed and 0 skipped (559 integration, 1 unit, 1 security).

## Screenshots

The following captures are already committed in the showcase assets. They show earlier development data; the SRS capture shows generation controls, and the traceability capture shows an incomplete chain.

<details>
<summary>Dashboard</summary>

![Planora dashboard](Planora_Showcase_Edit/assets/لقطة%20شاشة%202026-09-21%20211232.png)

</details>

<details>
<summary>Scrum Board</summary>

![Scrum Kanban board](Planora_Showcase_Edit/assets/لقطة%20شاشة%202026-09-21%20211707.png)

</details>

<details>
<summary>SRS generation workspace</summary>

![SRS generation controls before a draft is generated](Planora_Showcase_Edit/assets/لقطة%20شاشة%202026-09-21%20225330.png)

</details>

<details>
<summary>V-Model Traceability</summary>

![Requirement traceability showing missing design coverage](Planora_Showcase_Edit/assets/لقطة%20شاشة%202026-09-21%20225810.png)

</details>

| Screenshot still needed | Status |
| --- | --- |
| AI Requirement Generator | Placeholder: add a capture of analysis, refinement, and generated requirements under review. |
| Admin Users | Placeholder: add a capture using only demo accounts; the older capture contains personal email addresses. |
| Generated SRS preview | Placeholder: add a reviewed document preview to complement the existing workspace capture. |

## Project Status

**Final Acceptance: PASSED** — the build and automated test suite above completed successfully. This status describes the recorded build/test verification; it is not a security certification.

## Author

**Ramez Ahmad ALMAAYE'H**<br>
Software Engineer<br>
Jordan
