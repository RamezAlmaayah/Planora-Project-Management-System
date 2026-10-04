\# Planora — Phase 1 Acceptance Criteria



\## 1. Purpose



This document defines the conditions that must be satisfied before Phase 1 — Requirements \& Domain Analysis can be considered complete.



Phase 1 must not be marked complete unless the core business model, roles, permissions, workflows, assumptions, and acceptance rules are coherent.



\---



\# 2. Domain Model Acceptance



\## AC-DOM-01



The main Planora domain areas are identified.



Required:



\* Users

\* Projects

\* Project Membership

\* Scrum

\* Sprints

\* Backlog Items

\* Tasks

\* Developer / QA Workflow

\* QA Reviews

\* QA Evidence

\* V-Model

\* Requirements

\* Requirement Traceability

\* Issues

\* Comments

\* Task Attachments

\* Notifications

\* Activity Logs

\* Progress Reports



Status:



```text

PASS

```



\---



\## AC-DOM-02



The Domain Model does not depend conceptually on:



\* ASP.NET MVC

\* Entity Framework Core

\* SQL Server

\* Gemini

\* Razor

\* UI-specific logic



Status:



```text

PASS

```



\---



\## AC-DOM-03



The main entity relationships are documented clearly.



Status:



```text

PASS

```



\---



\# 3. Role Acceptance



\## AC-ROLE-01



All five required roles are documented:



```text

Admin

Project Manager

Scrum Master

Developer

QA Tester

```



Status:



```text

PASS

```



\---



\## AC-ROLE-02



Each role has a clearly defined responsibility and scope.



Status:



```text

PASS

```



\---



\# 4. Permission Acceptance



\## AC-PERM-01



The Permission Matrix is documented.



Status:



```text

PASS

```



\---



\## AC-PERM-02



Authorization is not based only on role names.



The documented model includes:



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



Status:



```text

PASS

```



\---



\## AC-PERM-03



Backend authorization is identified as authoritative.



Status:



```text

PASS

```



\---



\## AC-PERM-04



Project access isolation is explicitly documented.



Status:



```text

PASS

```



\---



\# 5. Scrum Acceptance



\## AC-SCRUM-01



Product Backlog behavior is documented.



Status:



```text

PASS

```



\---



\## AC-SCRUM-02



Sprint lifecycle is documented.



Status:



```text

PASS

```



\---



\## AC-SCRUM-03



Task lifecycle is documented.



Required main statuses:



```text

ToDo

InProgress

InReview

Done

```



Status:



```text

PASS

```



\---



\## AC-SCRUM-04



Completed Sprint behavior is documented.



Required:



\* No new task creation

\* UI restriction

\* Server-side restriction

\* Preserve history



Status:



```text

PASS

```



\---



\## AC-SCRUM-05



Task assignee restrictions are documented.



The assignee must be an eligible member of the project.



Status:



```text

PASS

```



\---



\# 6. Developer / QA Acceptance



\## AC-QA-01



Developer workflow is documented.



Required transition:



```text

ToDo

&#x20;↓

InProgress

&#x20;↓

InReview

```



Status:



```text

PASS

```



\---



\## AC-QA-02



QA Pass is documented as:



```text

InReview → Done

```



Status:



```text

PASS

```



\---



\## AC-QA-03



QA Fail is documented as:



```text

InReview → InProgress

```



Status:



```text

PASS

```



\---



\## AC-QA-04



QA Fail requires notes / report back to the Developer.



Status:



```text

PASS

```



\---



\## AC-QA-05



Multiple QA Review cycles are supported conceptually.



Status:



```text

PASS

```



\---



\## AC-QA-06



QA Evidence is separated from normal Task Attachments.



Status:



```text

PASS

```



\---



\# 7. Automatic Backlog Completion Acceptance



\## AC-BACKLOG-01



The system rule is documented:



```text

If all tasks linked to a Backlog Item are Done

&#x20;               ↓

Backlog Item becomes Completed

```



Status:



```text

PASS

```



\---



\## AC-BACKLOG-02



The rule is identified as requiring transactional consistency.



Status:



```text

PASS

```



\---



\# 8. V-Model Acceptance



\## AC-VM-01



The main V-Model phases are documented.



Status:



```text

PASS

```



\---



\## AC-VM-02



V-Model phase configuration is required to be centralized.



Status:



```text

PASS

```



\---



\## AC-VM-03



Traceability concept is documented.



Required flow:



```text

Requirement

&#x20;↓

Design

&#x20;↓

Implementation

&#x20;↓

Test / Verification

&#x20;↓

Validation

```



Status:



```text

PASS

```



\---



\# 9. Requirements Acceptance



\## AC-REQ-01



Functional Requirements are supported conceptually.



Status:



```text

PASS

```



\---



\## AC-REQ-02



Non-Functional Requirements are supported conceptually.



Status:



```text

PASS

```



\---



\## AC-REQ-03



Requirements belong to a specific project.



Status:



```text

PASS

```



\---



\## AC-REQ-04



Requirement traceability is defined as part of the system scope.



Status:



```text

PASS

```



\---



\# 10. AI Acceptance



\## AC-AI-01



Gemini is identified as a server-side external integration.



Status:



```text

PASS

```



\---



\## AC-AI-02



Gemini secrets must not be exposed to the browser or Git.



Status:



```text

PASS

```



\---



\## AC-AI-03



AI Requirement Generation access rules are documented.



Allowed:



\* Admin

\* Project Manager within authorized project

\* Scrum Master within authorized Scrum project



Not allowed:



\* Developer

\* QA Tester



Status:



```text

PASS

```



\---



\## AC-AI-04



AI Input Quality Validation is documented before final generation.



Status:



```text

PASS

```



\---



\## AC-AI-05



Insufficient input must not trigger final AI generation.



Status:



```text

PASS

```



\---



\## AC-AI-06



AI-generated requirements require human review.



Status:



```text

PASS

```



\---



\# 11. SRS Acceptance



\## AC-SRS-01



SRS generation scope is documented.



Required features:



\* Generate

\* Preview

\* Review

\* Save

\* TXT Export

\* PDF Export



Status:



```text

PASS

```



\---



\## AC-SRS-02



Exporting an already generated SRS must not trigger another Gemini generation call.



Status:



```text

PASS

```



\---



\# 12. Issue Acceptance



\## AC-ISSUE-01



Issue / Bug management is documented.



Status:



```text

PASS

```



\---



\## AC-ISSUE-02



Issue lifecycle is identified.



Status:



```text

PASS

```



\---



\# 13. Comment Acceptance



\## AC-COM-01



Comment ownership is documented.



Status:



```text

PASS

```



\---



\## AC-COM-02



Non-admin deletion is restricted to own comments.



Status:



```text

PASS

```



\---



\## AC-COM-03



Unauthorized comment deletion requires server-side rejection.



Status:



```text

PASS

```



\---



\# 14. Attachment Acceptance



\## AC-ATT-01



Task Attachments are documented separately from QA Evidence.



Status:



```text

PASS

```



\---



\## AC-ATT-02



Attachment upload permissions are documented.



Status:



```text

PASS

```



\---



\## AC-ATT-03



Attachment deletion permissions are documented.



Status:



```text

PASS

```



\---



\## AC-ATT-04



Maximum file size is documented as:



```text

10 MB

```



Status:



```text

PASS

```



\---



\## AC-ATT-05



Server-side file validation is required.



Status:



```text

PASS

```



\---



\## AC-ATT-06



Safe internal file naming is required.



Status:



```text

PASS

```



\---



\# 15. Security Acceptance



\## AC-SEC-01



Security is treated as a requirement from the beginning of the project.



Status:



```text

PASS

```



\---



\## AC-SEC-02



The following threats are recognized as relevant:



\* Broken Access Control

\* IDOR

\* SQL Injection

\* XSS

\* CSRF

\* Authentication attacks

\* Privilege escalation

\* Overposting

\* Path traversal

\* Unsafe file upload

\* Sensitive data exposure

\* API abuse

\* Dependency vulnerabilities

\* Secret exposure



Status:



```text

PASS

```



\---



\## AC-SEC-03



The server is documented as authoritative for validation.



Status:



```text

PASS

```



\---



\# 16. Assumptions Acceptance



\## AC-ASM-01



Explicit specification requirements are separated from implementation decisions.



Status:



```text

PASS

```



\---



\## AC-ASM-02



Open decisions are documented rather than silently guessed.



Status:



```text

PASS

```



\---



\## AC-ASM-03



Phase-specific decisions are assigned to the relevant future phase.



Status:



```text

PASS

```



\---



\# 17. Architecture Readiness



Phase 1 must provide enough clarity to begin architecture setup.



Required:



```text

Domain Model             ✓

Roles                    ✓

Permission Matrix        ✓

Main Workflows           ✓

Business Rules           ✓

Assumptions              ✓

Open Decisions           ✓

Acceptance Criteria      ✓

```



Status:



```text

PASS

```



\---



\# 18. Phase 1 Files



Required documentation:



```text

docs/

└── phase-01/

&#x20;   ├── 01-domain-model.md

&#x20;   ├── 02-roles-permissions.md

&#x20;   ├── 03-workflows.md

&#x20;   ├── 04-business-rules.md

&#x20;   ├── 05-assumptions.md

&#x20;   └── 06-acceptance-criteria.md

```



Status:



```text

PASS

```



\---



\# 19. Phase 1 Final Checklist



```text

\[✓] Domain Model documented



\[✓] Main entities identified



\[✓] Entity relationships documented



\[✓] Five roles documented



\[✓] Permission Matrix documented



\[✓] Project-level authorization documented



\[✓] Resource-level authorization documented



\[✓] Scrum workflow documented



\[✓] Sprint lifecycle documented



\[✓] Completed Sprint rule documented



\[✓] Developer workflow documented



\[✓] QA Pass documented



\[✓] QA Fail documented



\[✓] QA history documented



\[✓] Automatic Backlog Completion documented



\[✓] V-Model documented



\[✓] Requirements documented



\[✓] Traceability documented



\[✓] Issues documented



\[✓] Comments documented



\[✓] Task Attachments documented



\[✓] QA Evidence documented



\[✓] Gemini flow documented



\[✓] AI input validation documented



\[✓] SRS flow documented



\[✓] Notifications documented



\[✓] Audit Trail documented



\[✓] Security principles documented



\[✓] Assumptions documented



\[✓] Open implementation decisions documented



\[✓] No major requirement silently replaced

```



\---



\# 20. Phase 1 Result



```text

PHASE 1 — REQUIREMENTS \& DOMAIN ANALYSIS



STATUS: PASS

```



Planora is ready to proceed to:



```text

PHASE 2 — SOLUTION ARCHITECTURE

```



No database implementation or feature implementation has been started during Phase 1.



