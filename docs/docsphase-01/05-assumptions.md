\# Planora — Assumptions and Implementation Decisions



\## 1. Purpose



This document separates:



1\. Requirements explicitly defined by the Planora Master Specification

2\. Implementation decisions that will be made in later phases

3\. Open or ambiguous details that must not be silently invented



The purpose is to prevent accidental requirement changes during implementation.



\---



\# 2. Explicitly Defined Requirements



The following decisions are already defined by the Master Specification and are not open for redesign unless a real technical issue requires justification.



\---



\## 2.1 Application Type



Planora is a web application.



It must use:



```text

ASP.NET Core MVC

C#

Razor Views

```



It must not use:



```text

React

Angular

Vue

or another SPA framework

```



\---



\## 2.2 Architecture



Planora will use:



```text

Onion Architecture

\+

Clean Architecture principles

\+

SOLID

\+

Dependency Inversion

\+

Separation of Concerns

```



The system will remain a modular monolith.



Microservices and unnecessary infrastructure must not be introduced.



\---



\## 2.3 Main Projects



The solution is expected to contain:



```text

Planora.sln



src/

├── Planora.Domain

├── Planora.Application

├── Planora.Infrastructure

└── Planora.Web



tests/

├── Planora.UnitTests

├── Planora.IntegrationTests

└── Planora.SecurityTests

```



\---



\## 2.4 Database Technology



The application will use:



```text

SQL Server

Entity Framework Core

EF Core Migrations

```



For local development, the specification allows:



```text

SQL Server LocalDB

or

SQL Server Express

```



The exact local SQL Server edition will be decided before database implementation.



\---



\## 2.5 Authentication



Planora will use:



```text

ASP.NET Core Identity

\+

Cookie-Based Authentication

```



Authentication data must not be stored in localStorage.



\---



\## 2.6 Roles



The five required roles are:



```text

Admin

Project Manager

Scrum Master

Developer

QA Tester

```



\---



\## 2.7 Authorization Model



Authorization is not role-only.



It must consider:



```text

Authenticated User

&#x20;     ↓

Role Permission

&#x20;     ↓

Project Membership

&#x20;     ↓

Resource Ownership / Assignment

&#x20;     ↓

Workflow-State Permission

```



Backend authorization is authoritative.



\---



\## 2.8 Project Methodologies



Planora supports:



```text

Scrum / Agile

V-Model

```



\---



\## 2.9 Scrum Task Statuses



The primary task board statuses are:



```text

ToDo

InProgress

InReview

Done

```



\---



\## 2.10 Developer → QA Workflow



The required workflow is:



```text

ToDo

&#x20;↓

InProgress

&#x20;↓

InReview

&#x20;↓

QA Review

&#x20;↙      ↘

Fail    Pass

&#x20;↓        ↓

InProgress

&#x20;         ↓

&#x20;        Done

```



\---



\## 2.11 Completed Sprint Rule



A completed sprint becomes read-only for new task creation.



The UI must hide or disable Create Task.



The server must reject attempts to bypass this restriction.



\---



\## 2.12 AI Provider



The initial AI provider is:



```text

Google Gemini API

```



Gemini communication must remain server-side.



The API key must never be exposed in browser code or Git.



\---



\## 2.13 File Size



Task Attachments:



```text

Maximum = 10 MB per file

```



QA Evidence:



```text

Maximum = 10 MB per file

```



\---



\## 2.14 Local Deployment Scope



The initial application scope is:



```text

Local development / local demo only

```



Cloud deployment and public hosting are currently out of scope.



\---



\# 3. Implementation Decisions to Be Made Later



The following are required areas, but the Master Specification does not fully define their exact implementation.



These decisions should be made in the phase where they become relevant.



\---



\## DEC-01 — .NET SDK Version



The specification requires ASP.NET Core MVC but does not define an exact .NET SDK version.



Decision will be made in Phase 2 after checking the installed SDK using:



```text

dotnet --info

```



The selected version should be supported and appropriate for the project.



Status:



```text

OPEN — Decide in Phase 2

```



\---



\## DEC-02 — SQL Server Local Edition



The specification allows:



```text

SQL Server LocalDB

or

SQL Server Express

```



We will choose the available and simplest option on the development machine.



Status:



```text

OPEN — Decide before Phase 3

```



\---



\## DEC-03 — Local Email Strategy



The specification allows two local approaches.



Option A:



```text

Local SMTP capture tool

such as Papercut SMTP or Mailtrap sandbox

```



Option B:



```text

Development-only password reset / email verification shortcut

```



The email abstraction must remain replaceable regardless of the chosen local provider.



Status:



```text

OPEN — Decide in Phase 4

```



\---



\## DEC-04 — Registration Policy



The specification states:



```text

Register where allowed

```



but does not completely define whether normal users can publicly self-register or whether Admin creates all users.



This must be decided before implementing Authentication.



Status:



```text

OPEN — Phase 4

```



\---



\## DEC-05 — Email Verification



The specification states:



```text

Email verification if configured

```



Therefore email verification is supported but its exact local/demo behavior depends on the chosen email strategy.



Status:



```text

OPEN — Phase 4

```



\---



\## DEC-06 — Cookie Lifetime



The specification requires:



\* HttpOnly cookies

\* Secure cookies in HTTPS

\* Appropriate SameSite

\* Controlled lifetime

\* Session expiration



However, the exact lifetime is not defined.



Status:



```text

OPEN — Phase 4

```



\---



\## DEC-07 — Sliding Expiration



The specification permits sliding expiration only if deliberately configured.



We will decide whether it improves the local/demo authentication experience without weakening security.



Status:



```text

OPEN — Phase 4

```



\---



\## DEC-08 — Password Policy



ASP.NET Core Identity is required, but the exact:



\* Minimum password length

\* Required characters

\* Lockout threshold

\* Lockout duration



are not explicitly fixed by the specification.



Secure practical values will be selected in Phase 4.



Status:



```text

OPEN — Phase 4

```



\---



\## DEC-09 — Project Ownership Representation



The specification clearly defines:



\* Project creation

\* Project membership

\* Project-level authorization



but does not fully define whether Project ownership should be represented as:



```text

Project.OwnerId

```



or only through membership / project role information.



The final model will be selected during database design without weakening authorization rules.



Status:



```text

OPEN — Phase 3

```



\---



\## DEC-10 — Project Archive Implementation



The system must support:



```text

Archive Project

```



The specification does not explicitly define whether archive means:



\* Status change only

\* Soft-delete-like behavior

\* Separate archive timestamp



This will be finalized during database design.



Status:



```text

OPEN — Phase 3

```



\---



\## DEC-11 — Physical Delete Behavior



The specification requires correct delete behavior and protection from orphaned records, but does not define every entity's exact:



```text

Cascade

Restrict

SetNull

Soft Delete

```



behavior.



These rules will be defined per relationship in Phase 3.



Status:



```text

OPEN — Phase 3

```



\---



\## DEC-12 — Identifier Type



The specification requires primary keys but does not state whether domain entities should use:



```text

int

Guid

long

```



This will be standardized during Phase 3.



Status:



```text

OPEN — Phase 3

```



\---



\## DEC-13 — Requirement ID Generation



The specification gives examples:



```text

FR-001

FR-002

NFR-001

NFR-002

```



but does not define exactly how these identifiers are generated.



Potential implementation concerns include:



\* Per-project numbering

\* Concurrency

\* Deleted requirements

\* Generated AI requirements



Status:



```text

OPEN — Phase 9

```



\---



\## DEC-14 — Project Status Values



The specification requires Project Status but does not provide the final enum values.



They will be defined before Project implementation.



Status:



```text

OPEN — Phase 5

```



\---



\## DEC-15 — Backlog Status Values



Backlog status is required.



The specification explicitly references:



```text

In Sprint

Completed

```



but does not fully define every possible backlog status.



Status:



```text

OPEN — Phase 6

```



\---



\## DEC-16 — Sprint Status Values



The specification requires sprint lifecycle operations such as:



```text

Create

Start

Complete

```



but does not provide a final exact enum list.



Status:



```text

OPEN — Phase 6

```



\---



\## DEC-17 — Priority Levels



Several entities use Priority, but the specification does not define one universal priority scale.



Examples needing a decision:



\* Backlog Item

\* Task

\* Requirement

\* Issue



The final enums will be centralized.



Status:



```text

OPEN — Relevant feature phase

```



\---



\## DEC-18 — Issue Severity Values



Issue Severity is required, but exact severity levels are not explicitly defined.



Status:



```text

OPEN — Phase 10

```



\---



\## DEC-19 — Exact V-Model Phase Representation



The specification provides the expected V-Model phases and requires the configured model to be centralized.



It does not mandate whether those phases are represented by:



```text

Enum

Configuration

Database reference data

```



The simplest maintainable approach will be selected in Phase 8.



Status:



```text

OPEN — Phase 8

```



\---



\## DEC-20 — Traceability Data Model



The specification requires a Traceability Matrix but does not completely define the database structure behind every trace type.



We must decide how links between:



```text

Requirement

Design

Implementation

Test

Validation

```



are represented.



Status:



```text

OPEN — Phase 9

```



\---



\## DEC-21 — Task Attachment Storage Location



The specification requires safe file storage and prefers files outside direct public web access.



The exact local folder structure is not fixed.



Status:



```text

OPEN — Phase 10

```



\---



\## DEC-22 — Final Task Attachment Extension List



The specification provides an example set:



```text

pdf

docx

xlsx

png

jpg

jpeg

zip

```



The final allowed list must be centralized/configurable.



Status:



```text

OPEN — Phase 10

```



\---



\## DEC-23 — Final QA Evidence Extension List



The specification provides an example set:



```text

png

jpg

jpeg

mp4

pdf

txt

log

```



The final list must be centralized/configurable.



Status:



```text

OPEN — Phase 7 / 10

```



\---



\## DEC-24 — Gemini Model



The specification requires Google Gemini but does not define the exact Gemini model name.



The model will be selected when Gemini integration is implemented based on:



\* Structured output support

\* Availability

\* Appropriate cost / limits

\* Reliability



Status:



```text

OPEN — Phase 11

```



\---



\## DEC-25 — Gemini Timeout



Timeout handling is required, but the exact timeout duration is not specified.



Status:



```text

OPEN — Phase 11

```



\---



\## DEC-26 — Gemini Retry Policy



Controlled retry is allowed for safe transient failures.



The exact:



\* Retry count

\* Delay

\* Backoff



must be selected in Phase 11.



Status:



```text

OPEN — Phase 11

```



\---



\## DEC-27 — AI Quality Score Threshold



The AI input quality system must return a score and prevent final generation when input is insufficient.



The exact numerical threshold is not explicitly specified.



Status:



```text

OPEN — Phase 11

```



\---



\## DEC-28 — SRS PDF Library



PDF export is required.



The specification does not mandate a particular .NET PDF library.



The library will be selected in Phase 12 based on:



\* Maintenance

\* License suitability

\* Server-side support

\* Rendering quality



Status:



```text

OPEN — Phase 12

```



\---



\## DEC-29 — Report PDF/Export Format



Project reports are required, but the exact report export formats are not completely specified beyond SRS TXT/PDF.



Status:



```text

OPEN — Phase 12

```



\---



\## DEC-30 — Pagination Page Size



Server-side pagination is required for large lists.



The default page size is not specified.



Status:



```text

OPEN — Define centrally when pagination is implemented

```



\---



\## DEC-31 — Search Debounce



The specification gives approximately:



```text

300 ms

```



as the intended search debounce target.



The exact implementation may be tuned later after testing.



Status:



```text

IMPLEMENTATION DETAIL

```



\---



\## DEC-32 — SignalR Usage



SignalR must be used for Admin Online / Offline Presence.



Outside that feature, SignalR should only be introduced where real-time behavior provides genuine value.



Normal notifications must not force the entire system to depend on SignalR.



Status:



```text

PARTIALLY DEFINED



Presence → SignalR required

Other features → Decide only if justified

```



\---



\## DEC-33 — HTMX



HTMX is optional.



It may be used only if it genuinely reduces JavaScript complexity.



There is no requirement to use it.



Status:



```text

OPTIONAL — Not selected yet

```



\---



\## DEC-34 — Bootstrap Usage



Bootstrap utilities/components may be used where useful.



The specification does not require the entire visual system to be Bootstrap-based.



The final UI should use a reusable Planora design system and must not look like raw Bootstrap scaffolding.



Status:



```text

DECIDE DURING UI IMPLEMENTATION

```



\---



\## DEC-35 — SortableJS



SortableJS may be used for Kanban drag-and-drop.



It is not mandatory if another simple safe implementation meets the requirement.



Status:



```text

OPEN — Phase 6

```



\---



\## DEC-36 — Chart.js



Chart.js may be used for charts.



It is not mandatory if an equivalent lightweight approach is selected.



Status:



```text

OPEN — Dashboard / Reports phases

```



\---



\# 4. Assumptions We Will Use During Development



These are working assumptions used to organize development.



They do not override the Master Specification.



\---



\## ASM-01 — Server Is Always Authoritative



Whenever UI behavior and server validation overlap:



```text

Server validation wins.

```



\---



\## ASM-02 — Project Scope Is Fundamental



Project-scoped resources will always be checked against the correct project before operations are accepted.



\---



\## ASM-03 — Security Is Included From the Beginning



Security will not be postponed until Phase 15.



Phase 15 is a final audit.



Secure design begins during architecture, database, authentication, and feature development.



\---



\## ASM-04 — Database Integrity Complements Application Validation



Important business invariants should be protected at more than one level where useful.



Example:



```text

Duplicate Project Membership

```



should be prevented by:



```text

Application Validation

\+

Database Unique Constraint

```



\---



\## ASM-05 — UI Hiding Does Not Replace Authorization



Buttons may be hidden for UX.



The server still verifies the operation.



\---



\## ASM-06 — External Integrations Are Replaceable



Gemini, email, file storage, and similar external capabilities will remain behind abstractions when appropriate.



\---



\## ASM-07 — Local-First Demo



Development is optimized for a reliable local graduation-project demonstration.



Production/cloud architecture will not be introduced unless project scope later changes.



\---



\## ASM-08 — Demo Data Is Development-Only



Demo seed data must never run outside the Development environment.



\---



\# 5. Things We Must Not Assume



The following must NOT be silently invented.



\---



\## NO-ASSUME-01



Do not assume every Project Manager can access every project.



\---



\## NO-ASSUME-02



Do not assume every Developer can access every development task.



\---



\## NO-ASSUME-03



Do not assume QA can review every project.



\---



\## NO-ASSUME-04



Do not assume Scrum Master can access V-Model functionality.



\---



\## NO-ASSUME-05



Do not assume Task status changes are unrestricted.



\---



\## NO-ASSUME-06



Do not assume a completed Sprint can still be edited normally.



\---



\## NO-ASSUME-07



Do not assume AI output is automatically approved.



\---



\## NO-ASSUME-08



Do not assume an uploaded file is safe because the browser accepted it.



\---



\## NO-ASSUME-09



Do not assume IDs received from the browser are trusted.



\---



\## NO-ASSUME-10



Do not assume performance goals have been achieved until measured.



\---



\## NO-ASSUME-11



Do not claim Planora is:



```text

100% secure

zero latency

zero defects

```



Final validation must be based on actual tests.



\---



\# 6. Decisions by Phase



For easier tracking:



```text

Phase 2

├── .NET version

└── Base architecture details



Phase 3

├── SQL Server LocalDB vs Express

├── Primary key strategy

├── Delete behaviors

├── Project ownership representation

└── Project archive representation



Phase 4

├── Registration policy

├── Local email approach

├── Email verification behavior

├── Password policy

├── Cookie lifetime

└── Sliding expiration



Phase 5

└── Final Project status enum



Phase 6

├── Sprint status enum

├── Backlog status enum

├── Priority definitions

└── Kanban implementation details



Phase 8

└── V-Model phase representation



Phase 9

├── Requirement ID generation

└── Traceability data structure



Phase 10

├── File storage location

├── Attachment extension configuration

└── Issue severity



Phase 11

├── Gemini model

├── Timeout

├── Retry policy

└── AI quality threshold



Phase 12

├── PDF library

└── Report export details



Phase 13+

└── UI/performance tuning decisions

```



\---



\# 7. Assumptions Status



Requirements already fixed by the specification have been separated from implementation choices.



Open implementation decisions have been documented instead of silently guessed.



These decisions will be resolved only when their relevant phase begins.



