\# Planora — Domain Model



\## 1. Purpose



This document defines the main business concepts of Planora before database or application implementation begins.



Planora is a project-management system supporting:



\* Scrum / Agile

\* V-Model

\* Project and team management

\* Developer → QA workflow

\* Requirements management

\* Requirements traceability

\* AI-assisted requirement generation

\* SRS generation

\* Issues

\* Comments

\* Attachments

\* Notifications

\* Audit logging

\* Reports



The Domain Model describes the important entities and their relationships without depending on ASP.NET MVC, Entity Framework Core, SQL Server, Gemini, or UI implementation.



\---



\# 2. Main Domain Entities



\## 2.1 ApplicationUser



Represents a system user.



A user can have one of the main system roles:



\* Admin

\* Project Manager

\* Scrum Master

\* Developer

\* QA Tester



A user may belong to multiple projects through `ProjectMember`.



Important relationships:



```text

ApplicationUser

&#x20;   │

&#x20;   ├── ProjectMember

&#x20;   ├── TaskItem (Assigned Developer)

&#x20;   ├── QaReview

&#x20;   ├── TaskComment

&#x20;   ├── TaskAttachment

&#x20;   ├── Issue

&#x20;   ├── Notification

&#x20;   └── ActivityLog

```



ASP.NET Core Identity implementation details will be handled in a later phase.



\---



\## 2.2 Project



Represents a Planora project.



A project uses one methodology:



```text

Scrum

or

V-Model

```



Main responsibilities:



\* Store project information

\* Define methodology

\* Maintain project members

\* Maintain project scope and objectives

\* Track project status and dates

\* Control access to project resources

\* Provide project progress information



Main relationships:



```text

Project

&#x20;   │

&#x20;   ├── ProjectMembers

&#x20;   ├── Sprints

&#x20;   ├── BacklogItems

&#x20;   ├── Tasks

&#x20;   ├── Requirements

&#x20;   ├── Issues

&#x20;   ├── Notifications

&#x20;   ├── ActivityLogs

&#x20;   └── ProgressReports

```



Users must only access projects they are authorized to access, except Admin where global access is explicitly allowed.



\---



\## 2.3 ProjectMember



Represents the relationship between a user and a project.



It exists because having a system role alone does not automatically give access to every project.



Example:



```text

ApplicationUser

&#x20;     │

&#x20;     ▼

ProjectMember

&#x20;     │

&#x20;     ▼

Project

```



Main responsibilities:



\* Connect a user to a project

\* Prevent duplicate project membership

\* Support project-level authorization

\* Restrict task assignment to eligible project members

\* Support safe member removal



Important rule:



```text

A user must not be assigned to a task

if that user is not an eligible member

of that project.

```



\---



\# 3. Scrum Domain



\## 3.1 Sprint



Represents a Scrum sprint.



Main responsibilities:



\* Sprint planning

\* Start sprint

\* Complete sprint

\* Track sprint dates

\* Track sprint status

\* Contain sprint tasks

\* Provide sprint progress



Important rule:



```text

Completed Sprint

&#x20;     ↓

Read Only

&#x20;     ↓

No New Tasks

```



A completed sprint must preserve its historical data.



\---



\## 3.2 BacklogItem



Represents an item in the Product Backlog.



Main responsibilities:



\* Store backlog item information

\* Priority

\* Status

\* Link related tasks

\* Track completion



Relationship:



```text

Project

&#x20;  │

&#x20;  ▼

BacklogItem

&#x20;  │

&#x20;  ▼

TaskItem

```



Automatic completion rule:



```text

If every TaskItem linked to a BacklogItem is Done

&#x20;                   ↓

&#x20;         BacklogItem = Completed

```



Otherwise the backlog item remains active / In Sprint.



\---



\## 3.3 TaskItem



Represents a development task.



A task may belong to:



\* Project

\* Sprint

\* BacklogItem



A task may be assigned to an eligible Developer.



Main statuses:



```text

ToDo

InProgress

InReview

Done

```



Main relationships:



```text

TaskItem

&#x20;  │

&#x20;  ├── Assigned User

&#x20;  ├── BacklogItem

&#x20;  ├── Sprint

&#x20;  ├── QaReviews

&#x20;  ├── TaskComments

&#x20;  ├── TaskAttachments

&#x20;  └── Issues

```



The server must reject invalid workflow transitions.



\---



\# 4. Developer → QA Domain



\## 4.1 QaReview



Represents one QA review attempt for a TaskItem.



A task can have multiple QA reviews because a task may fail QA, return to development, and later be reviewed again.



Relationship:



```text

TaskItem

&#x20;  │

&#x20;  ├── QaReview #1 → Fail

&#x20;  │

&#x20;  ├── QaReview #2 → Fail

&#x20;  │

&#x20;  └── QaReview #3 → Pass

```



QaReview information should support:



\* QA Tester

\* Task

\* Pass / Fail result

\* Notes

\* Test date

\* Evidence

\* History



\---



\## 4.2 QA Evidence



Represents evidence attached to a specific QA Review.



Examples:



\* Screenshot

\* Short recording

\* Log file

\* PDF

\* Text file



Important relationship:



```text

TaskItem

&#x20;  │

&#x20;  ▼

QaReview

&#x20;  │

&#x20;  ▼

QA Evidence

```



QA Evidence belongs to the specific review, not directly to the task.



This preserves evidence separately when a task goes through several Pass / Fail cycles.



\---



\# 5. Developer → QA Workflow



Mandatory workflow:



```text

ToDo

&#x20; │

&#x20; ▼

InProgress

&#x20; │

&#x20; ▼

InReview

&#x20; │

&#x20; ▼

QA Review

&#x20;/       \\

Fail     Pass

&#x20;│         │

&#x20;▼         ▼

InProgress Done

```



Developer completion:



```text

InProgress

&#x20;   ↓

InReview

```



QA Pass:



```text

InReview

&#x20;   ↓

Done

```



QA Fail:



```text

InReview

&#x20;   ↓

InProgress

```



QA Fail must return the task to development with QA notes / report.



Invalid transitions must be rejected server-side.



\---



\# 6. V-Model Domain



V-Model projects support phases such as:



```text

Requirements

&#x20;    ↓

System Design

&#x20;    ↓

Architecture / Detailed Design

&#x20;    ↓

Implementation

&#x20;    ↓

Unit Testing

&#x20;    ↓

Integration Testing

&#x20;    ↓

System Testing

&#x20;    ↓

Acceptance / Validation

```



The exact phase configuration must be centralized rather than duplicated or hard-coded throughout the UI.



\---



\# 7. Requirement Domain



\## 7.1 Requirement



Represents a project requirement.



Supported requirement types:



```text

Functional Requirement (FR)

Non-Functional Requirement (NFR)

```



Example identifiers:



```text

FR-001

FR-002

NFR-001

NFR-002

```



Requirement information should support:



\* Unique identifier

\* Name / title

\* Description

\* Type

\* Priority

\* Rationale

\* Dependencies

\* Status

\* Project

\* Creation date

\* Last update

\* Traceability



\---



\## 7.2 RequirementTrace



Represents traceability between requirements and later development / verification stages.



Conceptual flow:



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



The system should support a traceability view or matrix to identify:



\* Covered requirements

\* Missing links

\* Missing verification

\* Missing validation



\---



\# 8. Issue Domain



\## 8.1 Issue



Represents a reported issue or bug.



An Issue may be related to:



\* Project

\* Task

\* Reporter

\* Assignee



Main information:



\* Title

\* Description

\* Severity

\* Priority

\* Status

\* Reporter

\* Assignee

\* CreatedAt

\* UpdatedAt

\* Resolution notes



Suggested lifecycle:



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



An issue may also be:



```text

Reopened

```



Workflow permissions must be validated server-side.



\---



\# 9. Collaboration Domain



\## 9.1 TaskComment



Represents a comment on a TaskItem.



Information:



\* Author

\* Content

\* Created timestamp

\* Edited timestamp if editing exists



Deletion rule:



```text

Admin

&#x20; → may delete permitted comments



Non-Admin

&#x20; → may delete only own comments

```



Unauthorized deletion must be rejected server-side.



\---



\## 9.2 TaskAttachment



Represents reference material attached to a TaskItem.



Examples:



\* Specifications

\* Documents

\* Screenshots

\* Design files



Information should include:



\* Original file name

\* Safe stored name / storage key

\* File size

\* File type / extension

\* Uploaded by

\* Uploaded at

\* Related task



Task Attachments are different from QA Evidence.



```text

TaskAttachment

&#x20;   → Reference material for TaskItem



QA Evidence

&#x20;   → Evidence for a specific QaReview

```



\---



\# 10. Notification Domain



\## 10.1 Notification



Represents a useful notification sent to a user.



Examples:



\* Task assigned

\* Task sent to QA

\* QA passed

\* QA failed

\* Issue assigned

\* Sprint started

\* Sprint completed

\* Member added

\* Requirement updated



Notifications must not make the entire system dependent on real-time communication.



\---



\# 11. Audit Domain



\## 11.1 ActivityLog



Represents an important action performed inside the system.



It should track:



\* Who performed the action

\* What action occurred

\* Target resource

\* Timestamp

\* Important previous / new state where useful and safe



Examples:



```text

User logged in

Member added to project

Task moved to InReview

QA failed task

QA passed task

Attachment uploaded

Role changed

```



Sensitive information such as passwords, API keys, authentication cookies, and reset tokens must never be stored in the audit trail.



\---



\# 12. Reporting Domain



\## 12.1 ProgressReport



Represents project progress information that may be used by dashboards and reports.



Reports may contain information about:



\* Project progress

\* Sprint progress

\* Team workload

\* QA results

\* Issues

\* Requirement coverage

\* V-Model traceability

\* Progress history



Reports must respect authorization rules.



\---



\# 13. Main Domain Relationship Diagram



```text

ApplicationUser

&#x20;     │

&#x20;     ▼

ProjectMember

&#x20;     │

&#x20;     ▼

Project

&#x20;┌────┼─────────────────────────────────┐

&#x20;│    │          │         │            │

&#x20;▼    ▼          ▼         ▼            ▼

Sprint BacklogItem Requirement Issue ProgressReport

&#x20;│       │          │

&#x20;│       │          ▼

&#x20;│       │    RequirementTrace

&#x20;│       │

&#x20;│       ▼

&#x20;└────► TaskItem

&#x20;        │

&#x20;        ├── QaReview

&#x20;        │     └── QA Evidence

&#x20;        │

&#x20;        ├── TaskComment

&#x20;        │

&#x20;        ├── TaskAttachment

&#x20;        │

&#x20;        └── Issue



Project / User

&#x20;     │

&#x20;     ├── Notification

&#x20;     └── ActivityLog

```



\---



\# 14. Core Domain Rules



The following rules are fundamental to the Planora domain:



1\. Project access is not determined only by system role.



2\. Project membership must be checked for project-level resources.



3\. Duplicate ProjectMember relationships are not allowed.



4\. A task assignee must be an eligible member of the same project.



5\. A completed Sprint cannot receive new tasks.



6\. Completed Sprint history must not be silently modified.



7\. Developer task transitions must follow the defined workflow.



8\. `InProgress → InReview` submits development work to QA.



9\. QA Pass changes `InReview → Done`.



10\. QA Fail changes `InReview → InProgress`.



11\. QA Fail should contain notes / report for the Developer.



12\. Invalid workflow transitions must be rejected by the server.



13\. When every task connected to a BacklogItem becomes Done, the BacklogItem becomes Completed.



14\. Backlog automatic completion must be transactional.



15\. QA Evidence belongs to the specific QaReview.



16\. TaskAttachment and QA Evidence are separate concepts.



17\. Non-admin users may only delete their own comments.



18\. Authorization decisions must be enforced server-side.



19\. V-Model phase definitions must be centralized.



20\. Requirements belong to a specific project.



21\. AI-generated requirements remain suggestions until reviewed by an authorized human user.



\---



\# 15. Domain Boundary



The Domain Layer must not depend on:



\* ASP.NET MVC

\* Razor Views

\* Entity Framework Core

\* SQL Server

\* Gemini API

\* Email providers

\* JavaScript

\* UI-specific implementation



Expected dependency direction later:



```text

Web

&#x20;↓

Application

&#x20;↓

Domain



Infrastructure

&#x20;↓

Application / Domain abstractions

```



The Domain remains the core business layer of Planora.



\---



\# 16. Phase 1 Domain Model Status



The main Planora domain areas have now been identified:



\* Users

\* Projects

\* Membership

\* Scrum

\* Sprints

\* Backlog

\* Tasks

\* Developer / QA workflow

\* QA Reviews

\* QA Evidence

\* V-Model

\* Requirements

\* Traceability

\* Issues

\* Comments

\* Attachments

\* Notifications

\* Audit logging

\* Reports



Detailed database fields, foreign keys, indexes, delete behaviors, and EF Core configurations will be finalized in Phase 3 — Database Design.



