\# Planora — Main Workflows



\## 1. Purpose



This document defines the main business workflows in Planora.



The workflows describe how users move through the system and how important business states change.



The server must remain authoritative for all sensitive workflow transitions.



\---



\# 2. Project Creation Workflow



```text

Authenticated User

&#x20;     ↓

Check Create Project Permission

&#x20;     ↓

Enter Project Information

&#x20;     ↓

Choose Methodology

&#x20;     ↓

Scrum OR V-Model

&#x20;     ↓

Validate Input

&#x20;     ↓

Create Project

&#x20;     ↓

Create Initial Membership / Ownership

&#x20;     ↓

Record Activity

&#x20;     ↓

Show Project Dashboard

```



Only users with permission to create projects may perform this workflow.



Expected roles:



\* Admin

\* Project Manager



\---



\# 3. Project Membership Workflow



```text

Authorized User

&#x20;     ↓

Open Project Members

&#x20;     ↓

Select User

&#x20;     ↓

Validate User Exists

&#x20;     ↓

Validate Project Exists

&#x20;     ↓

Check Duplicate Membership

&#x20;     ↓

Assign User to Project

&#x20;     ↓

Record Activity

&#x20;     ↓

Notify Member

```



Important rules:



\* Duplicate membership is not allowed.

\* The user must exist.

\* The project must exist.

\* The same rule must be enforced server-side.

\* Removing a member must not leave invalid active responsibilities.



\---



\# 4. Scrum Project Workflow



A Scrum project may contain:



```text

Project

&#x20;  ↓

Product Backlog

&#x20;  ↓

Backlog Items

&#x20;  ↓

Sprint Planning

&#x20;  ↓

Sprint

&#x20;  ↓

Tasks

&#x20;  ↓

Developer Work

&#x20;  ↓

QA Review

```



The Scrum workflow includes:



\* Product Backlog

\* Backlog Items

\* Sprint creation

\* Sprint planning

\* Sprint start

\* Sprint completion

\* Scrum tasks

\* Scrum Board

\* Task assignment

\* Sprint progress

\* Backlog progress



\---



\# 5. Backlog Item Workflow



```text

Authorized Scrum User

&#x20;     ↓

Create Backlog Item

&#x20;     ↓

Set Priority

&#x20;     ↓

Set Status

&#x20;     ↓

Include in Sprint Planning

&#x20;     ↓

Create Related Tasks

&#x20;     ↓

Track Task Completion

&#x20;     ↓

Complete Backlog Item Automatically

```



Automatic completion:



```text

All Tasks Linked to Backlog Item = Done

&#x20;                 ↓

&#x20;      Backlog Item = Completed

```



Otherwise:



```text

At Least One Task != Done

&#x20;          ↓

Backlog Item remains In Sprint

```



The automatic completion operation must remain transactional and consistent.



\---



\# 6. Sprint Lifecycle



```text

Create Sprint

&#x20;     ↓

Plan Sprint

&#x20;     ↓

Add Backlog / Tasks

&#x20;     ↓

Start Sprint

&#x20;     ↓

Active Sprint

&#x20;     ↓

Work on Tasks

&#x20;     ↓

Complete Sprint

&#x20;     ↓

Completed Sprint

```



\---



\# 7. Completed Sprint Workflow



Once a sprint becomes completed:



```text

Sprint Status = Completed

&#x20;       ↓

Sprint becomes read-only

&#x20;       ↓

No new tasks

&#x20;       ↓

No silent history changes

```



UI behavior:



```text

Hide / Disable Create Task

```



Server behavior:



```text

Reject Manual HTTP Create Task Attempts

```



Completed sprint history must be preserved.



\---



\# 8. Task Creation Workflow



```text

Authorized Scrum User

&#x20;     ↓

Open Sprint

&#x20;     ↓

Check Sprint Status

&#x20;     ↓

If Completed → Reject

&#x20;     ↓

Select Backlog Item

&#x20;     ↓

Enter Task Details

&#x20;     ↓

Select Eligible Project Member

&#x20;     ↓

Validate Assignee Membership

&#x20;     ↓

Create Task

&#x20;     ↓

Initial Status = ToDo

&#x20;     ↓

Record Activity

```



A task must not be assigned to a user outside the project.



\---



\# 9. Task Lifecycle



Recommended statuses:



```text

ToDo

&#x20;↓

InProgress

&#x20;↓

InReview

&#x20;↓

Done

```



The final transition to Done occurs through QA Pass.



Invalid transitions must be blocked server-side.



\---



\# 10. Developer Workflow



Developer works only on authorized assigned tasks.



```text

Developer

&#x20;  ↓

Open Assigned Task

&#x20;  ↓

Check Project Access

&#x20;  ↓

Check Task Assignment

&#x20;  ↓

Start Work

&#x20;  ↓

ToDo → InProgress

&#x20;  ↓

Complete Development

&#x20;  ↓

InProgress → InReview

&#x20;  ↓

Task enters QA Queue

```



The Developer must not directly move an `InReview` task to `Done`.



\---



\# 11. QA Queue Workflow



```text

Task Status = InReview

&#x20;       ↓

Task appears in authorized QA Queue

&#x20;       ↓

QA Tester opens task

&#x20;       ↓

Validate Project Access

&#x20;       ↓

Validate Task Status = InReview

&#x20;       ↓

Perform QA Review

```



QA Queue should display information such as:



\* Task title

\* Description

\* Priority

\* Assigned developer

\* Deadline

\* Current status

\* Project

\* Sprint

\* Test action



\---



\# 12. QA Pass Workflow



```text

Task = InReview

&#x20;     ↓

Authorized QA Tester

&#x20;     ↓

Perform QA Review

&#x20;     ↓

Result = Pass

&#x20;     ↓

Save QA Notes / Evidence

&#x20;     ↓

InReview → Done

&#x20;     ↓

Check Related Backlog Item

&#x20;     ↓

Update Backlog if all tasks are Done

&#x20;     ↓

Record Activity

&#x20;     ↓

Notify Relevant Users

```



\---



\# 13. QA Fail Workflow



```text

Task = InReview

&#x20;     ↓

Authorized QA Tester

&#x20;     ↓

Perform QA Review

&#x20;     ↓

Result = Fail

&#x20;     ↓

Enter QA Notes / Report

&#x20;     ↓

Attach QA Evidence if needed

&#x20;     ↓

InReview → InProgress

&#x20;     ↓

Return Task to Developer

&#x20;     ↓

Record Activity

&#x20;     ↓

Notify Developer

```



The task may later return to QA:



```text

InProgress

&#x20;   ↓

Developer Rework

&#x20;   ↓

InReview

&#x20;   ↓

QA Review Again

```



\---



\# 14. QA Review History Workflow



A task may have multiple QA Review cycles:



```text

Task

&#x20;│

&#x20;├── QA Review #1 → Fail

&#x20;│

&#x20;├── QA Review #2 → Fail

&#x20;│

&#x20;└── QA Review #3 → Pass

```



Each QA Review should preserve:



\* Result

\* Notes

\* QA Tester

\* Test date

\* Related evidence

\* History



\---



\# 15. QA Evidence Workflow



QA Evidence belongs to a specific QA Review.



```text

QA Tester

&#x20;  ↓

Perform QA Review

&#x20;  ↓

Attach Evidence

&#x20;  ↓

Validate File

&#x20;  ↓

Store Evidence

&#x20;  ↓

Link Evidence to QaReview

```



Examples:



\* Screenshot

\* Short recording

\* Log file

\* PDF

\* Text file



The evidence must not be mixed with general Task Attachments.



\---



\# 16. V-Model Workflow



V-Model projects support phases such as:



```text

Requirements

&#x20;     ↓

System Design

&#x20;     ↓

Architecture / Detailed Design

&#x20;     ↓

Implementation

&#x20;     ↓

Unit Testing

&#x20;     ↓

Integration Testing

&#x20;     ↓

System Testing

&#x20;     ↓

Acceptance / Validation

```



The exact phase configuration must be centralized rather than duplicated throughout the UI.



\---



\# 17. Requirement Management Workflow



```text

Authorized User

&#x20;     ↓

Open Project Requirements

&#x20;     ↓

Create Requirement

&#x20;     ↓

Choose Type

&#x20;     ↓

FR or NFR

&#x20;     ↓

Set Priority

&#x20;     ↓

Add Description / Rationale

&#x20;     ↓

Add Dependencies

&#x20;     ↓

Validate

&#x20;     ↓

Save Requirement

&#x20;     ↓

Record Activity

```



Requirements belong to a specific project.



\---



\# 18. Requirement Traceability Workflow



```text

Requirement

&#x20;    ↓

Design

&#x20;    ↓

Implementation

&#x20;    ↓

Test / Verification

&#x20;    ↓

Validation

```



The Traceability Matrix should help identify:



\* Existing links

\* Missing links

\* Coverage

\* Missing verification

\* Missing validation



\---



\# 19. AI Requirement Generation Workflow



AI Requirement Generation is project-scoped.



```text

Authorized Project User

&#x20;     ↓

Open AI Requirement Feature

&#x20;     ↓

Server Re-checks Project Authorization

&#x20;     ↓

Enter Requirement Description

&#x20;     ↓

AI Input Quality Validation

&#x20;     ↓

Evaluate Quality

```



The AI quality check returns:



\* Quality Score

\* Quality Level

\* Validation Message

\* Missing Information

\* Issues

\* Suggestions



\---



\# 20. AI Input Improvement Workflow



If input quality is insufficient:



```text

Input

&#x20; ↓

Quality Analysis

&#x20; ↓

Insufficient

&#x20; ↓

Do NOT perform final generation

&#x20; ↓

Show Concrete Improvement Options

&#x20; ↓

User Selects / Adds Missing Information

&#x20; ↓

Update Draft

&#x20; ↓

Analyze Again

```



The user may repeat this process multiple times.



\---



\# 21. AI Final Generation Workflow



When the input is sufficient:



```text

Validated Project

&#x20;     ↓

Validated User Authorization

&#x20;     ↓

Validated Input Quality

&#x20;     ↓

Application Use Case

&#x20;     ↓

AI Service Abstraction

&#x20;     ↓

Gemini Service

&#x20;     ↓

Structured Response

&#x20;     ↓

Schema Validation

&#x20;     ↓

Generate FR / NFR Suggestions

&#x20;     ↓

Human Review

&#x20;     ↓

Save Approved Requirements

```



Generated requirements remain suggestions until reviewed by an authorized human user.



\---



\# 22. Gemini Technical Flow



```text

Razor UI

&#x20;  ↓

MVC Controller

&#x20;  ↓

Application Use Case

&#x20;  ↓

IGeminiService

&#x20;  ↓

Infrastructure Gemini Client

&#x20;  ↓

Google Gemini API

```



Important rules:



\* API key stays server-side.

\* Use timeout.

\* Use CancellationToken.

\* Retry only safe transient failures.

\* Validate structured responses.

\* Do not log secrets.

\* Protect against excessively large requests.



\---



\# 23. SRS Generation Workflow



```text

Authorized User

&#x20;     ↓

Authorized Project

&#x20;     ↓

Open SRS Feature

&#x20;     ↓

Generate SRS

&#x20;     ↓

Preview

&#x20;     ↓

Review

&#x20;     ↓

Edit Where Appropriate

&#x20;     ↓

Save

&#x20;     ↓

Export TXT or PDF

```



Important rule:



```text

Already Generated SRS

&#x20;      ↓

Export

&#x20;      ↓

Reuse Saved Generated Content

```



Exporting must NOT trigger another Gemini generation request.



\---



\# 24. Issue Workflow



Suggested Issue lifecycle:



```text

Open

&#x20; ↓

Assigned

&#x20; ↓

InProgress

&#x20; ↓

Resolved

&#x20; ↓

Closed

```



An Issue may later become:



```text

Reopened

```



Basic flow:



```text

Authorized User

&#x20;     ↓

Create Issue

&#x20;     ↓

Choose Project / Related Task

&#x20;     ↓

Enter Severity / Priority

&#x20;     ↓

Assign if applicable

&#x20;     ↓

Work on Issue

&#x20;     ↓

Resolve

&#x20;     ↓

Close

```



Workflow permissions must be validated server-side.



\---



\# 25. Task Comment Workflow



```text

Authorized User

&#x20;     ↓

Open Task

&#x20;     ↓

Add Comment

&#x20;     ↓

Validate Content

&#x20;     ↓

Save Comment

&#x20;     ↓

Record Timestamp

```



Delete workflow:



```text

Delete Comment Request

&#x20;      ↓

Is Admin?

&#x20; ↙           ↘

Yes           No

&#x20;↓             ↓

Allowed      Is Author?

&#x20;              ↓

&#x20;         Yes → Allowed

&#x20;         No  → Denied

```



\---



\# 26. Task Attachment Upload Workflow



Task Attachments are general task reference materials.



```text

Authorized User

&#x20;     ↓

Open Task

&#x20;     ↓

Check Upload Permission

&#x20;     ↓

Check Sprint Not Completed

&#x20;     ↓

Select File

&#x20;     ↓

Validate Size

&#x20;     ↓

Validate Extension

&#x20;     ↓

Generate Safe Internal Name

&#x20;     ↓

Store Outside Direct Public Access

&#x20;     ↓

Link to Task

&#x20;     ↓

Record Audit Event

```



Maximum file size:



```text

10 MB

```



Example allowed extensions:



```text

pdf

docx

xlsx

png

jpg

jpeg

zip

```



These allowed types should be centralized/configurable.



\---



\# 27. Task Attachment Delete Workflow



```text

Delete Attachment Request

&#x20;         ↓

Check User Role

&#x20;         ↓

Check Project Authorization

&#x20;         ↓

Check Ownership Rule

&#x20;         ↓

Delete if Authorized

&#x20;         ↓

Record Audit Event

```



Rules:



```text

Admin

→ any permitted attachment

```



```text

Project Manager

→ authorized project attachments

```



```text

Scrum Master

→ own uploaded attachments only

```



```text

Developer

→ no deletion

```



```text

QA Tester

→ no general Task Attachment deletion

```



\---



\# 28. Notification Workflow



Notifications may be created for events such as:



```text

Task Assigned

Task Sent to QA

QA Passed

QA Failed

Issue Assigned

Sprint Started

Sprint Completed

Member Added

Requirement Updated

```



Basic flow:



```text

Business Event

&#x20;    ↓

Create Notification

&#x20;    ↓

Store Notification

&#x20;    ↓

Display to User

```



SignalR may be used only where real-time behavior adds meaningful value.



Core system functionality must not depend entirely on SignalR.



\---



\# 29. Audit Trail Workflow



Important business actions should create audit events.



```text

User Action

&#x20;   ↓

Business Operation

&#x20;   ↓

Operation Succeeds

&#x20;   ↓

Create ActivityLog

```



Audit information may include:



\* User

\* Action

\* Target resource

\* Timestamp

\* Important old/new state where useful



Never include:



\* Passwords

\* Reset tokens

\* Authentication cookies

\* API keys

\* Sensitive secrets



\---



\# 30. Admin Activity Log Workflow



```text

Admin

&#x20; ↓

Open Activity Log

&#x20; ↓

Filter By

&#x20; ├── User

&#x20; ├── Project

&#x20; ├── Action Type

&#x20; └── Date Range

&#x20; ↓

Paginated Results

```



The Activity Log is read-only.



Entries should be readable in plain language.



\---



\# 31. User Presence Workflow



```text

Authenticated Session Active

&#x20;       ↓

User = Online

```



After logout or session inactivity / expiry:



```text

User = Offline

```



The Admin Presence screen may use SignalR.



If SignalR is unavailable:



```text

Fallback to Periodic Snapshot

```



Presence failure must not break core project-management functionality.



\---



\# 32. Overall Scrum End-to-End Workflow



```text

Login

&#x20; ↓

Open Authorized Project

&#x20; ↓

Create / Review Backlog

&#x20; ↓

Create Sprint

&#x20; ↓

Create Tasks

&#x20; ↓

Assign Developer

&#x20; ↓

Start Sprint

&#x20; ↓

Developer:

ToDo → InProgress

&#x20; ↓

Developer:

InProgress → InReview

&#x20; ↓

QA Queue

&#x20; ↓

QA Review

&#x20;/       \\

Fail      Pass

&#x20;↓          ↓

InProgress Done

&#x20;↓          ↓

Developer   Check Backlog

Rework      Completion

&#x20;↓

InReview

&#x20;↓

QA Again

```



\---



\# 33. Main Server-Side Workflow Rule



Every sensitive state-changing workflow must follow this pattern:



```text

User Request

&#x20;    ↓

Authentication

&#x20;    ↓

Authorization

&#x20;    ↓

Project Membership

&#x20;    ↓

Resource Validation

&#x20;    ↓

Workflow-State Validation

&#x20;    ↓

Input Validation

&#x20;    ↓

Business Operation

&#x20;    ↓

Database Transaction Where Needed

&#x20;    ↓

Audit / Notification

&#x20;    ↓

Response

```



The UI may guide the user, but the server remains authoritative.



\---



\# 34. Phase 1 Workflow Status



The following workflows are now documented:



\* Project creation

\* Project membership

\* Scrum backlog

\* Sprint lifecycle

\* Completed Sprint rule

\* Task creation

\* Developer workflow

\* QA queue

\* QA Pass

\* QA Fail

\* QA history

\* Automatic backlog completion

\* QA Evidence

\* V-Model

\* Requirements

\* Traceability

\* AI validation

\* AI refinement

\* AI generation

\* SRS generation

\* Issues

\* Comments

\* Task Attachments

\* Notifications

\* Audit Trail

\* Admin Activity Log

\* User Presence



Implementation details will be handled in later phases.



