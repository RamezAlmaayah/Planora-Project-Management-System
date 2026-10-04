\# Planora — Architecture Decisions



\## 1. Purpose



This document records the major architectural decisions for Planora.



The objective is to keep the project structure clear, maintainable, testable, secure, and easy to extend throughout development.



\---



\# 2. ADR-001 — .NET Version



\## Decision



Planora will use:



```text

.NET 10

Target Framework: net10.0

SDK: 10.0.400

```



\## Reason



.NET 10.0.400 is installed and working on the development machine.



The solution has already been created and successfully built using this SDK.



A `global.json` file is used to keep the project pinned to the selected SDK.



\## Status



```text

ACCEPTED

```



\---



\# 3. ADR-002 — Application Architecture



\## Decision



Planora will use:



```text

Onion Architecture

\+

Clean Architecture Principles

\+

SOLID

\+

Dependency Inversion

\+

Separation of Concerns

```



\## Reason



Planora contains several business areas:



\* Projects

\* Scrum

\* Developer / QA Workflow

\* V-Model

\* Requirements

\* AI

\* SRS

\* Issues

\* Reports

\* Notifications

\* Security



Separating these concerns makes the system easier to test, maintain, and extend.



\## Status



```text

ACCEPTED

```



\---



\# 4. ADR-003 — Modular Monolith



\## Decision



Planora will be implemented as a:



```text

Modular Monolith

```



\## Not Selected



The project will not initially use:



```text

Microservices

Message Broker

Kubernetes

Event Sourcing

CQRS Framework

```



\## Reason



Planora does not currently require distributed-system complexity.



A modular monolith provides clean separation while keeping local development and deployment simple.



\## Status



```text

ACCEPTED

```



\---



\# 5. ADR-004 — Solution Structure



\## Decision



The solution uses:



```text

Planora.sln



src/

├── Planora.Domain/

├── Planora.Application/

├── Planora.Infrastructure/

└── Planora.Web/



tests/

├── Planora.UnitTests/

├── Planora.IntegrationTests/

└── Planora.SecurityTests/

```



\## Status



```text

IMPLEMENTED

```



\---



\# 6. ADR-005 — Domain Layer



\## Decision



`Planora.Domain` contains only core business concepts.



Examples:



\* Entities

\* Enums

\* Value Objects

\* Domain Rules

\* Domain Exceptions

\* Business Invariants



\## Domain Must Not Depend On



```text

ASP.NET Core MVC

Entity Framework Core

SQL Server

Gemini

Email Providers

Razor Views

JavaScript

Infrastructure

Web

```



\## Reason



The Domain is the center of the application.



Business rules must remain independent of technical implementation details.



\## Status



```text

ACCEPTED

```



\---



\# 7. ADR-006 — Application Layer



\## Decision



`Planora.Application` contains:



\* Use cases

\* Application services

\* Contracts / abstractions

\* DTOs

\* Validators

\* Feature logic

\* Application-level authorization decisions where appropriate



\## Dependency



```text

Planora.Application

&#x20;       ↓

Planora.Domain

```



Application must not depend on:



```text

Planora.Infrastructure

Planora.Web

```



\## Status



```text

ACCEPTED

```



\---



\# 8. ADR-007 — Infrastructure Layer



\## Decision



`Planora.Infrastructure` contains technical and external implementations.



Responsibilities include:



\* Entity Framework Core

\* SQL Server

\* ASP.NET Core Identity persistence

\* File storage

\* Gemini integration

\* Email implementation

\* External services

\* Infrastructure security services



\## Dependencies



```text

Planora.Infrastructure

&#x20;       ↓

Planora.Application

&#x20;       ↓

Planora.Domain

```



Infrastructure may implement abstractions defined by Application.



\## Status



```text

ACCEPTED

```



\---



\# 9. ADR-008 — Web Layer



\## Decision



`Planora.Web` is the Presentation Layer and application Composition Root.



Technology:



```text

ASP.NET Core MVC

Razor Views

HTML5

Modern CSS

JavaScript ES Modules

```



Web responsibilities include:



\* Controllers

\* Views

\* ViewModels

\* Middleware

\* MVC Filters

\* UI authorization behavior

\* Dependency Injection composition

\* HTTP concerns



\## Important Rule



Controllers must remain thin.



Controllers must not contain:



\* Complex business logic

\* Direct database queries

\* Large workflow implementations



\## Status



```text

ACCEPTED

```



\---



\# 10. ADR-009 — Dependency Direction



\## Decision



Dependencies point inward.



Primary business dependency flow:



```text

Planora.Web

&#x20;     ↓

Planora.Application

&#x20;     ↓

Planora.Domain

```



Infrastructure implements Application abstractions:



```text

Planora.Web

&#x20;     ↓

Planora.Infrastructure

&#x20;     ↓

Planora.Application

&#x20;     ↓

Planora.Domain

```



Current project references:



```text

Application

→ Domain



Infrastructure

→ Application

→ Domain



Web

→ Application

→ Infrastructure

```



\## Forbidden Dependencies



```text

Domain → Infrastructure     NOT ALLOWED



Domain → Web                NOT ALLOWED



Application → Infrastructure NOT ALLOWED



Application → Web           NOT ALLOWED



Infrastructure → Web        NOT ALLOWED

```



\## Status



```text

IMPLEMENTED

```



\---



\# 11. ADR-010 — Web Technology



\## Decision



Planora uses:



```text

ASP.NET Core MVC

\+

Razor Views

```



\## Not Selected



```text

Angular

React

Vue

Other SPA Framework

```



\## Reason



The project specification explicitly requires MVC and Razor Views.



Interactive operations can still use:



\* Fetch

\* AJAX

\* Partial Views

\* JavaScript ES Modules

\* HTMX selectively if useful



without converting Planora into a SPA.



\## Status



```text

ACCEPTED

```



\---



\# 12. ADR-011 — Frontend Interaction Strategy



\## Decision



Planora will avoid unnecessary full-page reloads.



Interactive features may use:



```text

Fetch / AJAX

Partial Views

JavaScript ES Modules

```



HTMX remains optional.



Examples:



\* QA Pass

\* QA Fail

\* Kanban updates

\* Task status changes

\* Comments

\* Search

\* Filtering

\* Pagination

\* Member assignment

\* Notifications



\## Status



```text

ACCEPTED

```



\---



\# 13. ADR-012 — Database Technology



\## Decision



Planora will use:



```text

Entity Framework Core

\+

SQL Server

```



\## Not Yet Decided



Local development will use either:



```text

SQL Server LocalDB

or

SQL Server Express

```



The exact choice will be finalized in Phase 3.



\## Status



```text

PARTIALLY DECIDED

```



\---



\# 14. ADR-013 — Authentication



\## Decision



Planora will use:



```text

ASP.NET Core Identity

\+

Secure Cookie Authentication

```



Planora will not use JWT as the primary browser authentication mechanism.



Authentication information must not be stored in browser localStorage.



\## Status



```text

ACCEPTED

```



\---



\# 15. ADR-014 — Authorization Strategy



\## Decision



Authorization will use multiple levels:



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



Technology will include:



```text

ASP.NET Core Identity Roles

Authorization Policies

Resource-Based Authorization

Application Business Rules

```



\## Important Rule



UI visibility is not authorization.



The backend remains authoritative.



\## Status



```text

ACCEPTED

```



\---



\# 16. ADR-015 — External Integrations



\## Decision



External services should be accessed through abstractions where practical.



Example:



```text

Application

&#x20;    ↓

IGeminiService

&#x20;    ↑

Infrastructure implementation

```



This principle also applies where useful to:



\* Email

\* File storage

\* External APIs

\* Export services



\## Reason



Changing an external provider should not require rewriting Planora business logic.



\## Status



```text

ACCEPTED

```



\---



\# 17. ADR-016 — Gemini Integration



\## Decision



Google Gemini is the initial AI provider.



Communication flow:



```text

Razor UI

&#x20;  ↓

MVC Controller

&#x20;  ↓

Application Use Case

&#x20;  ↓

AI Abstraction

&#x20;  ↓

Infrastructure Gemini Client

&#x20;  ↓

Google Gemini API

```



Gemini credentials remain server-side only.



\## Status



```text

ACCEPTED

```



\---



\# 18. ADR-017 — Testing Strategy



\## Decision



Planora uses three test projects.



\### Unit Tests



```text

Planora.UnitTests

```



For:



\* Domain rules

\* Application rules

\* Workflow transitions

\* Validation

\* Permission decision logic



\### Integration Tests



```text

Planora.IntegrationTests

```



For:



\* Database

\* Infrastructure

\* Authentication

\* Authorization

\* MVC integration

\* Critical workflows



\### Security Tests



```text

Planora.SecurityTests

```



For:



\* Access control

\* IDOR

\* CSRF

\* Authentication security

\* Authorization bypass

\* Input abuse



\## Status



```text

IMPLEMENTED

```



\---



\# 19. ADR-018 — Dependency Injection



\## Decision



Planora will use ASP.NET Core built-in Dependency Injection.



Dependencies should be registered centrally through extension methods rather than placing all registrations directly inside a large `Program.cs`.



Expected future pattern:



```text

Application Dependency Injection

Infrastructure Dependency Injection

Web Configuration

```



\## Status



```text

ACCEPTED

```



\---



\# 20. ADR-019 — Configuration



\## Decision



Configuration must remain centralized and environment-aware.



Examples:



\* Database connection

\* Gemini settings

\* File upload settings

\* Authentication settings

\* Rate limiting settings



Secrets must not be committed to source control.



Local secrets will use:



```text

dotnet user-secrets

```



where appropriate.



\## Status



```text

ACCEPTED

```



\---



\# 21. ADR-020 — Security From the Beginning



\## Decision



Security is not postponed until the final Security Audit.



Security controls will be added throughout implementation.



Phase 15 will perform a dedicated final audit rather than introduce security for the first time.



\## Status



```text

ACCEPTED

```



\---



\# 22. ADR-021 — Maintainability



\## Decision



Planora must prioritize maintainable implementation.



Rules include:



\* Thin Controllers

\* No business logic in Views

\* Centralized configuration

\* Centralized roles and statuses

\* Focused interfaces

\* Small focused services

\* Avoid duplicated business logic

\* Avoid giant classes

\* Meaningful naming

\* Automated tests for critical rules



\## Status



```text

ACCEPTED

```



\---



\# 23. Architecture Snapshot



Current architecture:



```text

&#x20;                      ┌─────────────────┐

&#x20;                      │   Planora.Web   │

&#x20;                      │ MVC / Razor UI  │

&#x20;                      └───────┬─────────┘

&#x20;                              │

&#x20;                 ┌────────────┴────────────┐

&#x20;                 │                         │

&#x20;                 ▼                         ▼

&#x20;      ┌────────────────────┐    ┌───────────────────────┐

&#x20;      │Planora.Application │    │Planora.Infrastructure │

&#x20;      │Use Cases/Contracts │◄───│EF/Identity/Gemini/etc │

&#x20;      └──────────┬─────────┘    └───────────┬───────────┘

&#x20;                 │                          │

&#x20;                 └────────────┬─────────────┘

&#x20;                              ▼

&#x20;                   ┌──────────────────┐

&#x20;                   │  Planora.Domain  │

&#x20;                   │ Business Core    │

&#x20;                   └──────────────────┘

```



The Domain remains the innermost layer.



\---



\# 24. Current Architecture Status



Completed:



```text

\[✓] .NET 10 selected

\[✓] global.json configured

\[✓] Planora.sln created

\[✓] Domain project created

\[✓] Application project created

\[✓] Infrastructure project created

\[✓] Web MVC project created

\[✓] Unit Test project created

\[✓] Integration Test project created

\[✓] Security Test project created

\[✓] Projects added to solution

\[✓] Dependency direction configured

\[✓] Folder structure created

\[✓] Initial full solution build successful

```



Architecture decisions are now documented.



Phase 2 is not considered fully complete until the remaining common architecture setup is verified.



