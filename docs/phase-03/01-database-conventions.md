\# Planora — Database Conventions



\## 1. Purpose



This document defines the database conventions that Planora will follow before the first domain entities and EF Core configurations are implemented.



The objective is to keep the database simple, relational, consistent, maintainable, and safe.



\---



\# 2. Database Engine



Planora local development will use:



```text

SQL Server LocalDB



Instance:

(localdb)\\MSSQLLocalDB

```



Status:



```text

ACCEPTED

```



\---



\# 3. Primary Key Strategy



\## Domain / Business Entities



Planora business entities will use:



```csharp

int Id

```



Examples:



```text

Project.Id

ProjectMember.Id

Sprint.Id

BacklogItem.Id

TaskItem.Id

QaReview.Id

QaEvidence.Id

Requirement.Id

Issue.Id

TaskComment.Id

TaskAttachment.Id

Notification.Id

ActivityLog.Id

ProgressReport.Id

```



SQL Server will generate these IDs using identity columns.



Example:



```text

1

2

3

4

...

```



\## Reason



Planora is currently a local modular monolith backed by a single SQL Server database.



Integer keys provide:



```text

Simple implementation

Small indexes

Efficient joins

Easy debugging

Easy foreign-key relationships

No unnecessary identifier complexity

```



GUIDs are not required simply to hide record numbers.



Authorization and IDOR protection must be handled by server-side authorization, not by making IDs difficult to guess.



Status:



```text

ACCEPTED

```



\---



\# 4. ASP.NET Identity User Key



ASP.NET Core Identity will initially keep its standard user key strategy:



```text

string UserId

```



Therefore references to users from Planora business data will use fields such as:



```csharp

string UserId

string AssignedUserId

string CreatedByUserId

string UploadedByUserId

string QaUserId

```



where appropriate.



We will not customize ASP.NET Identity to use Guid keys unless a real requirement later justifies that complexity.



Status:



```text

ACCEPTED

```



\---



\# 5. Important Security Note About Integer IDs



Using integer IDs does NOT mean authorization can trust an ID received from the browser.



Example:



```text

Developer requests:



/Tasks/25

```



The server must still check:



```text

Does Task 25 exist?

&#x20;       ↓

Does it belong to an authorized project?

&#x20;       ↓

Is the Developer a member?

&#x20;       ↓

Is Task 25 assigned to this Developer?

&#x20;       ↓

Does the workflow allow this action?

```



Changing:



```text

TaskId = 25

```



to:



```text

TaskId = 26

```



must never provide unauthorized access.



Primary-key type is not an authorization mechanism.



\---



\# 6. Entity Naming



C# entity classes use singular names.



Examples:



```text

Project

Sprint

TaskItem

Requirement

Issue

QaReview

```



Collections use plural names.



Examples:



```text

Projects

Sprints

Tasks

Requirements

Issues

QaReviews

```



Entity and relationship naming should remain explicit and easy to understand.



\---



\# 7. Foreign Key Naming



Foreign keys use:



```text

<RelatedEntity>NameId

```



Examples:



```text

ProjectId

SprintId

BacklogItemId

TaskItemId

QaReviewId

RequirementId

```



User relationships use descriptive names where necessary.



Examples:



```text

UserId

AssignedUserId

UploadedByUserId

QaUserId

CreatedByUserId

UpdatedByUserId

```



Ambiguous names such as:



```text

Owner

User

Parent

Reference

```



should not be used as foreign-key column names without clear context.



\---



\# 8. Date and Time Convention



Planora stores application timestamps in UTC.



Examples:



```text

CreatedAt

UpdatedAt

JoinedAt

UploadedAt

TestedAt

LastSeenAt

```



Application code will obtain the current UTC time through the existing application abstraction:



```text

IClock.UtcNow

```



rather than scattering direct time access throughout business logic.



Database/application timestamps will later be converted to an appropriate display timezone in the presentation layer where necessary.



\---



\# 9. CreatedAt



Important persistent entities should contain:



```csharp

DateTime CreatedAt

```



where the concept is useful.



The value represents when the record was created.



\---



\# 10. UpdatedAt



Mutable entities should contain:



```csharp

DateTime? UpdatedAt

```



or an equivalent design where appropriate.



Not every append-only entity requires normal modification tracking.



Example:



```text

ActivityLog

```



is intended to remain append-oriented/read-only rather than being routinely edited.



\---



\# 11. CreatedBy / UpdatedBy



User audit relationships may be included where useful.



Examples:



```text

CreatedByUserId

UpdatedByUserId

```



They will not be added blindly to every table.



Use them where they provide meaningful audit or business value.



\---



\# 12. Required vs Optional Relationships



Relationships must be explicitly classified as:



```text

Required

or

Optional

```



Examples:



```text

ProjectMember → Project

Required



TaskItem → Project

Required



TaskItem → Sprint

Depends on finalized Scrum model



Issue → Related Task

Optional



QaEvidence → QaReview

Required

```



The exact nullability will be finalized during entity-by-entity design.



\---



\# 13. Delete Behavior Principle



Planora will not use broad cascade deletion blindly.



Default business-data principle:



```text

Restrict deletion

when deleting the parent could destroy important project history

or create an invalid state.

```



Cascade deletion may be used only for clear dependent data where deleting the parent safely implies deleting the child.



Every relationship will receive an explicit delete behavior in its EF Core configuration.



Examples requiring careful protection:



```text

Project

Sprint

Task

QA Review

Requirement

Activity Log

```



Historical project data should not disappear accidentally.



\---



\# 14. Unique Constraints



Unique constraints must protect important business invariants.



Confirmed example:



```text

ProjectMember



(ProjectId, UserId)

must be unique

```



This prevents duplicate project membership even if application validation is bypassed.



Other unique constraints will be defined when their entities are designed.



\---



\# 15. Indexes



Indexes will be added for frequently searched, joined, or filtered fields.



Likely examples include:



```text

ProjectMember.ProjectId

ProjectMember.UserId



TaskItem.ProjectId

TaskItem.SprintId

TaskItem.AssignedUserId

TaskItem.Status



Issue.ProjectId

Issue.Status

Issue.AssigneeId



Requirement.ProjectId

Requirement.Type



Notification.UserId



ActivityLog.UserId

ActivityLog.ProjectId

ActivityLog.CreatedAt

```



Final indexes will be confirmed after entity and query design.



Indexes must serve real query patterns rather than being added to every column.



\---



\# 16. No Orphan Records



Database design must prevent orphaned relationships.



Examples:



```text

Task must not reference a Project that does not exist.



QaEvidence must not reference a QaReview that does not exist.



ProjectMember must not reference a Project that does not exist.



TaskComment must not reference a Task that does not exist.

```



Foreign keys and application validation will work together to preserve integrity.



\---



\# 17. Cross-Project Integrity



Planora must protect against logically invalid relationships even when individual foreign keys exist.



Example:



```text

Task.ProjectId = 1

Sprint.ProjectId = 2



Task.SprintId = that Sprint

```



This relationship is logically invalid even though both records exist.



Application validation and database design must prevent broken cross-project relationships where practical.



\---



\# 18. Transactions



Transactions are required for important multi-step operations.



Important example:



```text

QA Pass

&#x20;  ↓

Task → Done

&#x20;  ↓

Check sibling tasks

&#x20;  ↓

Possibly BacklogItem → Completed

```



These changes must succeed consistently as one business operation.



Other workflows will use transactions when partial completion would leave invalid state.



\---



\# 19. Concurrency Strategy



Planora will use optimistic concurrency where simultaneous edits could cause meaningful data loss or invalid workflow transitions.



Important candidates include:



```text

Project

Sprint

TaskItem

```



Examples:



```text

Two Project Managers edit the same Project.



Two QA users attempt to review the same Task.



Sprint is completed while another user changes Sprint tasks.

```



The exact EF Core concurrency token implementation will be defined when these entities are configured.



The user should receive a friendly conflict message rather than silently overwriting newer data.



\---



\# 20. EF Core Configuration Convention



EF Core entity mapping will use separate Fluent API configuration classes.



Expected structure:



```text

Planora.Infrastructure/

└── Persistence/

&#x20;   └── Configurations/

&#x20;       ├── ProjectConfiguration.cs

&#x20;       ├── ProjectMemberConfiguration.cs

&#x20;       ├── SprintConfiguration.cs

&#x20;       ├── TaskItemConfiguration.cs

&#x20;       └── ...

```



Avoid putting all entity configuration inside one huge:



```text

OnModelCreating()

```



\---



\# 21. Navigation Loading



Planora will not depend on automatic lazy loading.



Queries should deliberately load/project only the data required by the use case.



Preferred patterns later include:



```text

Select projection

AsNoTracking for read-only queries

Controlled Include usage

Pagination

```



This helps prevent:



```text

N+1 queries

Massive object graphs

Unnecessary database reads

```



\---



\# 22. Database Constraints vs Application Validation



Critical rules should be protected at multiple appropriate levels.



Example:



```text

Duplicate Project Membership

```



Protection:



```text

Application validation

&#x20;       +

Composite unique database constraint

```



Another example:



```text

Non-project member assigned to Task

```



Protection:



```text

Application authorization / validation

\+

Relationship integrity

```



The database is not a replacement for business authorization, and application validation is not a replacement for relational integrity.



\---



\# 23. Migration Convention



All schema changes will use EF Core Migrations.



Migrations must be:



```text

Named meaningfully

Reviewed before application

Kept in source control

Applied deliberately

```



Example initial migration:



```text

InitialCreate

```



Migration creation begins only after the first database model is coherent.



\---



\# 24. Seed Convention



Role seeding is required.



Demo data will later be seeded only when:



```text

Environment = Development

```



The system must never seed demo accounts/data into a non-development environment accidentally.



Passwords must go through normal ASP.NET Core Identity password hashing.



\---



\# 25. Current Database Decisions



```text

Database Engine

→ SQL Server LocalDB



Instance

→ (localdb)\\MSSQLLocalDB



Business Entity Primary Keys

→ int identity



ASP.NET Identity User Key

→ string



Application Time Storage

→ UTC



EF Mapping

→ Separate Fluent API configurations



Delete Behavior

→ Explicit per relationship



Concurrency

→ Optimistic where required



Migration Strategy

→ EF Core Migrations

```



\---



\# 26. Status



```text

PHASE 3 DATABASE CONVENTIONS



STATUS: ACCEPTED

```



The project is ready to begin detailed entity design.



