\# Planora — Business Rules



\## 1. Purpose



This document defines the core business rules that Planora must enforce.



These rules are not only UI behavior.



Where applicable, they must be enforced server-side through:



\* Domain rules

\* Application validation

\* Authorization policies

\* Resource-based authorization

\* Database constraints

\* Transactions



The server is authoritative.



\---



\# 2. Authorization Rules



\## BR-AUTH-01 — Authentication



Protected functionality requires an authenticated user.



Anonymous users must not access protected project-management features.



\---



\## BR-AUTH-02 — Authorization Is Not Role-Only



Authorization must consider:



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



Having the correct role alone is not enough.



\---



\## BR-AUTH-03 — Backend Is Authoritative



Hiding or disabling a button in the UI is not sufficient security.



Every sensitive operation must be authorized again by the server.



\---



\## BR-AUTH-04 — Forbidden Access



An authenticated user who does not have permission to access a protected resource should receive:



```text

403 Forbidden

```



where appropriate.



\---



\# 3. Project Rules



\## BR-PROJ-01 — Authorized Project Access



Except for Admin where global access is explicitly permitted, users may only see and access projects they are authorized to access.



\---



\## BR-PROJ-02 — Project Manager Scope



A Project Manager must not automatically gain access to every project.



The Project Manager must operate only within authorized projects.



\---



\## BR-PROJ-03 — Scrum Master Scope



A Scrum Master may use Scrum-specific functionality only within authorized Scrum projects.



A Scrum Master must not automatically receive access to V-Model-only functionality.



\---



\## BR-PROJ-04 — Project Methodology



A project must use an explicitly supported methodology:



```text

Scrum

or

V-Model

```



Methodology-specific features must respect the selected project methodology.



\---



\# 4. Project Membership Rules



\## BR-MEM-01 — Dedicated Membership



Project access must use a dedicated `ProjectMember` relationship.



\---



\## BR-MEM-02 — No Duplicate Membership



The same user must not be added to the same project more than once.



Conceptually:



```text

(ProjectId + UserId)

must be unique

```



The database constraint will be finalized in Phase 3.



\---



\## BR-MEM-03 — Validate User



Before adding a project member:



```text

User must exist.

```



\---



\## BR-MEM-04 — Validate Project



Before adding a project member:



```text

Project must exist.

```



\---



\## BR-MEM-05 — Eligible Assignee



A task may only be assigned to an eligible member of the same project.



Invalid example:



```text

Task belongs to Project A



Selected Developer belongs only to Project B



→ Reject

```



\---



\## BR-MEM-06 — Server-Side Assignment Validation



The assignee dropdown is not sufficient protection.



Even if a user manually changes the submitted UserId, the server must verify membership.



\---



\## BR-MEM-07 — Safe Member Removal



A project member must not be removed if doing so leaves an invalid active responsibility unless that responsibility is reassigned or explicitly handled.



Example:



```text

Developer owns active assigned tasks

&#x20;       ↓

Attempt Member Removal

&#x20;       ↓

Must be handled safely

```



\---



\# 5. Sprint Rules



\## BR-SPR-01 — Sprint Lifecycle



The system must support:



```text

Create

→ Plan

→ Start

→ Active

→ Complete

```



\---



\## BR-SPR-02 — Completed Sprint Is Read-Only for Task Creation



When:



```text

Sprint = Completed

```



new tasks must not be created inside that sprint.



\---



\## BR-SPR-03 — Completed Sprint UI



When a Sprint is completed:



```text

Create Task

```



must be hidden or disabled in the UI.



\---



\## BR-SPR-04 — Completed Sprint Server Protection



A manually crafted HTTP request must not bypass the Completed Sprint rule.



The server must reject new task creation for a completed sprint.



\---



\## BR-SPR-05 — Preserve Sprint History



Completed sprint history must be preserved.



The system must not silently modify historical sprint data.



\---



\## BR-SPR-06 — Attachment Restriction on Completed Sprint



New Task Attachments must not be uploaded to tasks belonging to a completed sprint.



This must be enforced server-side.



\---



\# 6. Task Assignment Rules



\## BR-TASK-01 — Project Relationship



A task must belong to a valid project context.



\---



\## BR-TASK-02 — Sprint Relationship



A Scrum task assigned to a Sprint must belong to the appropriate project context.



Detailed foreign-key constraints will be finalized in Phase 3.



\---



\## BR-TASK-03 — Backlog Relationship



Tasks associated with a Backlog Item must remain within the same valid project / Scrum context.



\---



\## BR-TASK-04 — Developer Assignment



A Developer may work only on development tasks assigned to that Developer where the workflow permits the requested operation.



\---



\## BR-TASK-05 — No Cross-User Modification



A Developer must not modify another Developer's assigned task simply by changing a TaskId in a request.



\---



\# 7. Task Status Rules



The main Scrum task statuses are:



```text

ToDo

InProgress

InReview

Done

```



\---



\## BR-STATUS-01 — Valid Development Progression



The expected development progression is:



```text

ToDo

&#x20;↓

InProgress

&#x20;↓

InReview

```



\---



\## BR-STATUS-02 — Developer Submission to QA



When development work is completed:



```text

InProgress → InReview

```



The task then becomes eligible for the QA queue.



\---



\## BR-STATUS-03 — Developer Cannot Directly Complete QA Work



A Developer must not directly perform:



```text

InReview → Done

```



Final completion occurs through QA Pass.



\---



\## BR-STATUS-04 — Invalid Transitions



Invalid task status transitions must be blocked server-side.



\---



\# 8. QA Rules



\## BR-QA-01 — Authorized QA Queue



A QA Tester may review only eligible tasks within authorized projects.



\---



\## BR-QA-02 — QA Status Requirement



QA Review is allowed only when:



```text

Task Status = InReview

```



Attempting to QA-review a task in:



```text

ToDo

InProgress

Done

```



must be rejected.



\---



\## BR-QA-03 — QA Pass



QA Pass performs:



```text

InReview → Done

```



\---



\## BR-QA-04 — QA Fail



QA Fail performs:



```text

InReview → InProgress

```



\---



\## BR-QA-05 — QA Fail Information



When QA fails a task, the task must return to the Developer with QA notes / report.



\---



\## BR-QA-06 — QA History



A task may pass through multiple QA Review cycles.



Each review must remain historically distinguishable.



Example:



```text

Review 1 → Fail

Review 2 → Fail

Review 3 → Pass

```



\---



\# 9. QA Evidence Rules



\## BR-QAE-01 — Review-Specific Evidence



QA Evidence belongs to a specific `QaReview`.



It must not be stored as general Task Attachment data.



\---



\## BR-QAE-02 — Multiple Evidence Files



A single QA Review may contain multiple evidence files.



\---



\## BR-QAE-03 — Evidence Uploader



Only the QA Tester performing that review may upload evidence to that review.



\---



\## BR-QAE-04 — Evidence Deletion



After QA Evidence is submitted:



```text

Only Admin may delete it.

```



This preserves QA history integrity.



\---



\## BR-QAE-05 — Evidence Maximum Size



Maximum size per QA evidence file:



```text

10 MB

```



\---



\## BR-QAE-06 — Evidence Allowed Types



The specification gives the example set:



```text

png

jpg

jpeg

mp4

pdf

txt

log

```



These types should be centralized/configurable.



\---



\## BR-QAE-07 — Server-Side File Validation



Evidence file:



\* Extension

\* Size

\* Storage name



must be validated server-side.



\---



\## BR-QAE-08 — Safe Storage Name



Never trust the original uploaded file name.



Generate a safe internal storage name to help prevent path traversal.



\---



\# 10. Automatic Backlog Completion Rules



\## BR-BACKLOG-01 — Check Related Tasks



When a task passes QA and becomes `Done`, the system must check all tasks related to the same Backlog Item.



\---



\## BR-BACKLOG-02 — Complete When All Done



If:



```text

All Related Tasks = Done

```



then:



```text

BacklogItem → Completed

```



\---



\## BR-BACKLOG-03 — Keep In Sprint Otherwise



If at least one related task is not Done:



```text

BacklogItem remains In Sprint

```



\---



\## BR-BACKLOG-04 — Transactional Consistency



QA Pass and automatic Backlog completion must remain transactionally consistent.



The system must not end with a partially updated workflow.



\---



\# 11. Comment Rules



\## BR-COM-01 — Comment Ownership



Comments must record their author.



\---



\## BR-COM-02 — Non-Admin Deletion



A non-admin user may delete only their own comments.



\---



\## BR-COM-03 — Admin Deletion



Admin may delete permitted comments as defined by the specification.



\---



\## BR-COM-04 — Unauthorized Deletion



Unauthorized comment deletion must be rejected server-side.



\---



\# 12. Task Attachment Rules



Task Attachments are general task reference materials.



They are distinct from QA Evidence.



\---



\## BR-ATT-01 — Admin Upload



Admin may upload Task Attachments to tasks in any project.



\---



\## BR-ATT-02 — Project Manager Upload



Project Manager may upload Task Attachments within authorized projects.



\---



\## BR-ATT-03 — Scrum Master Upload



Scrum Master may upload Task Attachments within authorized Scrum projects.



\---



\## BR-ATT-04 — Developer Upload



Developer must not upload general Task Attachments.



The specification explicitly allows Developers to view/download attachments on tasks assigned to them.



\---



\## BR-ATT-05 — QA Tester Upload



QA Tester must not upload general Task Attachments.



QA-specific files belong to QA Evidence instead.



The specification does not explicitly grant QA Tester general Task Attachment download access in Section 26.1, so this must not be assumed unless later defined.



\---



\## BR-ATT-06 — Admin Delete



Admin may delete any Task Attachment in any project.



\---



\## BR-ATT-07 — Project Manager Delete



Project Manager may delete Task Attachments within authorized projects.



\---



\## BR-ATT-08 — Scrum Master Delete



Scrum Master may delete only Task Attachments personally uploaded by that Scrum Master.



\---



\## BR-ATT-09 — Developer Delete



Developer may not delete Task Attachments.



\---



\## BR-ATT-10 — QA Delete



QA Tester has no general Task Attachment deletion permission.



\---



\## BR-ATT-11 — Maximum File Size



Maximum Task Attachment size:



```text

10 MB

```



\---



\## BR-ATT-12 — Allowed File Extensions



The specification provides the example set:



```text

pdf

docx

xlsx

png

jpg

jpeg

zip

```



The allowed list should be centralized/configurable rather than repeated inline.



\---



\## BR-ATT-13 — Reject Invalid Types



Unsupported file extensions must be rejected server-side regardless of client-side validation.



\---



\## BR-ATT-14 — Safe File Naming



The original filename must never be trusted as the internal storage filename.



\---



\## BR-ATT-15 — Protected File Access



Where practical, uploaded files should not be exposed directly as unrestricted public static files.



Downloads should go through an authorized server action.



\---



\## BR-ATT-16 — Attachment Audit



Every Task Attachment upload and deletion must create an Audit Trail record.



\---



\# 13. Requirement Rules



\## BR-REQ-01 — Requirement Type



Planora supports:



```text

Functional Requirement

Non-Functional Requirement

```



\---



\## BR-REQ-02 — Project Binding



Every requirement must belong to a specific project.



\---



\## BR-REQ-03 — Requirement Identifier



Requirements need unique identifiers such as:



```text

FR-001

NFR-001

```



The exact generation mechanism will be decided during implementation.



\---



\## BR-REQ-04 — NFR Related FR Validation



When an NFR references Functional Requirement IDs, the backend must validate those FR IDs.



Invalid references must be:



\* Rejected

\* Corrected

\* Or safely removed



according to the implementation flow.



\---



\# 14. V-Model Rules



\## BR-VM-01 — Supported V-Model Phases



The specification defines phases such as:



```text

Requirements

System Design

Architecture / Detailed Design

Implementation

Unit Testing

Integration Testing

System Testing

Acceptance / Validation

```



\---



\## BR-VM-02 — Centralized Phase Definition



V-Model phases must not be repeatedly hard-coded throughout the UI.



Their configuration should be centralized.



\---



\## BR-VM-03 — Traceability



The system should support traceability across:



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



\---



\# 15. AI Rules



\## BR-AI-01 — Server-Only Gemini Communication



All Gemini API communication must happen server-side.



\---



\## BR-AI-02 — Never Expose Gemini Secrets



The Gemini API key must never appear in:



\* HTML

\* JavaScript

\* Browser storage

\* Git



\---



\## BR-AI-03 — Authorized Roles



AI Requirement Generation is available to:



\* Admin

\* Project Manager within authorized projects

\* Scrum Master within authorized Scrum projects



It is not available to:



\* Developer

\* QA Tester



\---



\## BR-AI-04 — Project-Bound AI Session



Every AI-assisted requirement session must belong to a specific authorized project.



There must not be a project-less global AI requirement session.



\---



\## BR-AI-05 — Authorization Recheck



Project authorization must be re-checked server-side on every AI request:



```text

Analyze

Refine

Generate

```



\---



\## BR-AI-06 — Input Quality Before Generation



Input quality must be analyzed before final requirement generation.



\---



\## BR-AI-07 — Insufficient Input



If input quality is insufficient:



```text

Do NOT perform final generation.

```



Instead, return actionable improvement information.



\---



\## BR-AI-08 — Actionable Improvement Options



Missing information should map to concrete questions, choices, or fill-in information rather than only vague advice.



\---



\## BR-AI-09 — Multiple Refinement Rounds



Users may repeat AI input refinement multiple times before final generation.



\---



\## BR-AI-10 — Human Review



AI-generated FRs and NFRs are suggestions.



They are not automatically considered approved final project requirements.



An authorized human user must review them.



\---



\## BR-AI-11 — Structured Response Validation



Gemini structured output must be validated before being trusted or persisted.



\---



\## BR-AI-12 — Timeout and Cancellation



AI calls must support:



\* Timeout

\* CancellationToken

\* Controlled transient retry where safe

\* Graceful fallback behavior



\---



\## BR-AI-13 — Duplicate Request Protection



The system must prevent accidental duplicate AI generation requests.



\---



\# 16. SRS Rules



\## BR-SRS-01 — Authorized Access



SRS generation follows the defined authorization scope.



\---



\## BR-SRS-02 — Generate and Save



The system supports:



\* Generate

\* Preview

\* Review

\* Edit where appropriate

\* Save

\* TXT export

\* PDF export



\---



\## BR-SRS-03 — Export Must Reuse Generated Content



If an SRS has already been generated:



```text

Export TXT/PDF

```



must reuse the existing generated content.



Export must not trigger another Gemini generation request.



\---



\# 17. Issue Rules



\## BR-ISSUE-01 — Project Scope



An Issue belongs to a project.



It may also be related to a task.



\---



\## BR-ISSUE-02 — Suggested Statuses



```text

Open

Assigned

InProgress

Resolved

Closed

Reopened

```



\---



\## BR-ISSUE-03 — Server-Side Workflow Validation



Issue workflow permissions must be validated server-side.



\---



\# 18. Input Validation Rules



\## BR-VAL-01 — Server Is Authoritative



Client-side validation improves UX.



Server-side validation determines whether the operation is accepted.



\---



\## BR-VAL-02 — Validate Important Relationships



The server must validate relevant:



\* IDs

\* Project relationships

\* Membership

\* Ownership

\* Assignment

\* Workflow state

\* File type

\* File size



\---



\## BR-VAL-03 — Never Trust Hidden Inputs



Hidden fields must not be treated as trusted values.



\---



\# 19. Security Rules



\## BR-SEC-01 — No Destructive GET



Sensitive state-changing actions must not use unsafe GET requests.



\---



\## BR-SEC-02 — CSRF Protection



Cookie-authenticated state-changing operations must use antiforgery protection.



\---



\## BR-SEC-03 — Overposting Protection



Persistence entities must not be blindly bound from untrusted forms.



Use explicit ViewModels / request DTOs.



\---



\## BR-SEC-04 — XSS Protection



Use Razor output encoding by default.



Do not use untrusted input through `Html.Raw()`.



\---



\## BR-SEC-05 — SQL Injection Protection



Use EF Core parameterized queries.



Never concatenate untrusted input into SQL.



\---



\## BR-SEC-06 — Secret Protection



Never commit or expose:



\* API keys

\* Connection strings

\* Email credentials

\* Production secrets



\---



\# 20. Audit Rules



\## BR-AUD-01 — Important Actions



Important actions should record:



\* Who

\* What

\* Target

\* Timestamp

\* Important old/new state where safe



\---



\## BR-AUD-02 — Never Audit Secrets



Never store:



\* Passwords

\* Reset tokens

\* Authentication cookies

\* API keys

\* Sensitive secrets



inside application logs or audit records.



\---



\## BR-AUD-03 — Activity Log Is Read-Only



The Admin Activity Log is append-oriented/read-only from the UI.



Entries are not manually edited by Admin.



\---



\# 21. Notification Rules



Useful events may create notifications such as:



\* Task assigned

\* Task sent to QA

\* QA passed

\* QA failed

\* Issue assigned

\* Sprint started

\* Sprint completed

\* Member added

\* Requirement updated



SignalR may be used where useful, but core Planora functionality must not depend entirely on it.



\---



\# 22. Maintainability Rules



\## BR-MAIN-01 — Centralized Workflow Rules



Workflow logic should not be copied between Controllers, Views, and JavaScript.



\---



\## BR-MAIN-02 — Centralized Authorization



Avoid scattered logic such as:



```text

if role == ...

```



throughout the project.



Use centralized policies and authorization rules.



\---



\## BR-MAIN-03 — Centralized Constants



Roles, statuses, policies, and similar constants should be centralized.



\---



\## BR-MAIN-04 — External Integrations Behind Abstractions



Gemini and other external services should be accessed through abstractions so external provider code does not leak into business logic.



\---



\# 23. Critical Rules Requiring Automated Tests



The following must later receive explicit tests:



```text

Anonymous protected access

Developer unauthorized project access

Developer editing another user's task

QA reviewing unauthorized project task

QA reviewing non-InReview task

Scrum Master unauthorized project access

Scrum Master V-Model-only access attempt

Changed ProjectId request

Assignment of non-project member

Task creation in completed Sprint

Unauthorized comment deletion

Normal user accessing Admin endpoint

Invalid workflow transition

CSRF attempt

Overposting

XSS input

SQL injection-like input

AI duplicate request

```



\---



\# 24. Business Rules Status



The core Planora rules are now documented for:



\* Authorization

\* Projects

\* Membership

\* Sprints

\* Tasks

\* Developer workflow

\* QA workflow

\* QA Evidence

\* Backlog completion

\* Comments

\* Attachments

\* Requirements

\* V-Model

\* AI

\* SRS

\* Issues

\* Validation

\* Security

\* Audit

\* Notifications

\* Maintainability



These rules will be converted into concrete domain logic, application validation, authorization handlers, EF Core constraints, and automated tests in later phases.



