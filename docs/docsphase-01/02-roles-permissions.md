\# Planora — Roles and Permissions



\## 1. Purpose



This document defines the main Planora roles and the authorization model used throughout the system.



Planora uses:



\* ASP.NET Core Identity Roles

\* Policy-Based Authorization

\* Resource-Based Authorization

\* Project Membership Validation

\* Ownership / Assignment Validation

\* Workflow-State Validation



Authorization must never depend only on role names.



\---



\# 2. Core Roles



Planora contains five main roles:



1\. Admin

2\. Project Manager

3\. Scrum Master

4\. Developer

5\. QA Tester



\---



\# 3. Authorization Model



Every protected action should be evaluated using the following model:



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



This means a user having a role does not automatically mean the user can access every resource.



Example:



```text

User Role = Developer

```



is NOT enough.



The Developer must also:



```text

Be a member of the project

\+

Be assigned to the task

\+

Be allowed to perform the action

\+

The current task status must allow it

```



\---



\# 4. Global Authorization Rules



\## 4.1 Authenticated User



The user must be logged in before accessing protected features.



Anonymous users must not access protected routes.



\---



\## 4.2 Role Permission



The user's role determines the general type of operations they may perform.



Example:



```text

Developer

→ Development Tasks



QA Tester

→ QA Reviews



Project Manager

→ Project Management



Admin

→ Global Administration

```



\---



\## 4.3 Project Membership



Except for deliberately unrestricted Admin access, a user must be an authorized member of the project.



A valid system role alone does not automatically grant access to a project.



\---



\## 4.4 Resource Ownership / Assignment



Some operations require ownership or assignment.



Example:



```text

Developer

→ May update only tasks assigned to that Developer

```



Example:



```text

Scrum Master

→ May delete only Task Attachments uploaded by that Scrum Master

```



\---



\## 4.5 Workflow-State Permission



Actions must also respect the current workflow state.



Example:



```text

Developer cannot send a ToDo task directly to Done.

```



Valid workflow:



```text

ToDo

&#x20;↓

InProgress

&#x20;↓

InReview

&#x20;↓

QA Review

&#x20;↓

Done

```



\---



\# 5. Permission Matrix



| Feature                          | Admin | Project Manager    | Scrum Master           | Developer           | QA Tester               |

| -------------------------------- | ----- | ------------------ | ---------------------- | ------------------- | ----------------------- |

| Manage Users                     | Yes   | No                 | No                     | No                  | No                      |

| Manage Roles                     | Yes   | No                 | No                     | No                  | No                      |

| Create Project                   | Yes   | Yes                | No                     | No                  | No                      |

| Update Authorized Project        | Yes   | Yes                | Limited                | No                  | No                      |

| Manage Project Members           | Yes   | Yes                | Authorized Scrum Scope | No                  | No                      |

| Manage Scrum Backlog             | Yes   | Yes                | Yes                    | View                | View                    |

| Manage Sprint                    | Yes   | Yes                | Yes                    | View                | View                    |

| Create Scrum Task                | Yes   | Yes                | Yes                    | No                  | No                      |

| Update Assigned Development Task | Yes   | Limited            | Limited                | Yes                 | No                      |

| QA Review                        | Yes   | No                 | No                     | No                  | Yes                     |

| Create Issue                     | Yes   | Yes                | Yes                    | Yes                 | Yes                     |

| Upload Task Attachment           | Yes   | Yes                | Yes                    | No                  | No                      |

| Download Task Attachment         | Yes   | Yes                | Yes                    | Assigned Tasks Only | No General Upload Scope |

| Delete Task Attachment           | Any   | Authorized Project | Own Uploads Only       | No                  | No                      |

| Manage Requirements              | Yes   | Yes                | Authorized Scope       | View                | View                    |

| AI Requirement Generation        | Yes   | Yes                | Authorized Scrum Scope | No                  | No                      |

| SRS Generation                   | Yes   | Yes                | Authorized Scrum Scope | No                  | No                      |

| Reports                          | Yes   | Yes                | Authorized Scrum Scope | Limited             | Limited                 |



Every permission must still respect project membership and resource-level checks.



\---



\# 6. Admin



\## Main Scope



Admin has global system-level privileges.



Main permissions:



\* Manage users

\* Manage roles

\* Create projects

\* Access all projects where global Admin access is explicitly allowed

\* Manage project members

\* Manage Scrum

\* Create tasks

\* Perform QA actions

\* Manage requirements

\* Use AI requirement generation

\* Generate SRS

\* Access reports

\* Manage attachments

\* Delete permitted comments

\* View system-wide audit logs

\* View user presence



\---



\## Admin Important Rules



Admin may:



```text

Delete any permitted Task Attachment

```



Admin may:



```text

Delete permitted comments

```



Admin may:



```text

View system-wide Activity Logs

```



Admin may access the dedicated:



```text

Activity Log \& User Presence

```



screen.



The Admin screen must remain server-side protected.



\---



\# 7. Project Manager



\## Main Scope



Project Manager manages authorized projects.



Main permissions:



\* Create projects

\* Update authorized projects

\* Manage project members

\* Manage Scrum backlog

\* Manage sprints

\* Create Scrum tasks

\* Manage requirements

\* Use AI requirement generation

\* Generate SRS

\* View and generate project reports

\* Create issues

\* Upload Task Attachments

\* Delete Task Attachments in authorized projects



\---



\## Project Manager Restrictions



A Project Manager must not automatically access every project in the system.



Access must still respect:



```text

Authorized Project

\+

Membership / Ownership Rules

```



\---



\# 8. Scrum Master



\## Main Scope



Scrum Master operates only within authorized Scrum projects.



Main permissions:



\* Manage Scrum backlog

\* Manage sprints

\* Create Scrum tasks

\* Perform limited task management

\* Manage project members within authorized Scrum scope

\* Manage requirements within authorized scope

\* Use AI requirement generation within authorized Scrum scope

\* Generate SRS within authorized scope

\* View authorized Scrum reports

\* Create issues

\* Upload Task Attachments



\---



\## Scrum Master Restrictions



Scrum Master must NOT automatically access:



```text

V-Model-specific project functionality

```



Scrum Master access must be limited to:



```text

Authorized Scrum Projects

```



Task Attachment deletion:



```text

Scrum Master

→ May delete only attachments personally uploaded by that Scrum Master

```



\---



\# 9. Developer



\## Main Scope



Developer works on assigned development tasks.



Main permissions:



\* View authorized projects

\* View Scrum backlog

\* View sprint

\* View assigned tasks

\* Update assigned development tasks

\* Submit completed development work to QA

\* Create issues

\* Add task comments

\* Delete own comments

\* View requirements

\* View permitted reports

\* View / download Task Attachments on assigned tasks



\---



\## Developer Restrictions



Developer must NOT:



\* Create Scrum tasks

\* Perform QA reviews

\* Generate AI requirements

\* Generate SRS

\* Upload Task Attachments

\* Delete other users' comments

\* Modify another Developer's task

\* Access unauthorized projects



\---



\## Developer Task Rule



Developer may update a task only if:



```text

User is Developer

\+

User belongs to project

\+

Task is assigned to user

\+

Current workflow state permits update

```



\---



\# 10. QA Tester



\## Main Scope



QA Tester reviews eligible tasks.



Main permissions:



\* View authorized projects

\* View InReview tasks

\* Perform QA Review

\* Pass task

\* Fail task

\* Add QA notes

\* Attach QA Evidence

\* Create related issue

\* View QA history

\* Create issues

\* View requirements

\* View limited reports



\---



\## QA Restrictions



QA Tester must NOT:



\* Create Scrum tasks

\* Modify development tasks directly

\* Upload normal Task Attachments

\* Generate AI requirements

\* Generate SRS

\* Review tasks outside authorized projects

\* Review tasks not currently in InReview



\---



\## QA Review Authorization



A QA review is valid only if:



```text

User is authorized QA

\+

User has access to project

\+

Task belongs to authorized project

\+

Task Status = InReview

```



Otherwise the server must reject the request.



\---



\# 11. Project Access Rules



Except where Admin global access is explicitly allowed:



```text

User

&#x20;↓

Must be ProjectMember

&#x20;↓

May access project resources

```



A user must not access a project only because the user has the correct system role.



\---



\# 12. Task Authorization Rules



\## Developer



Developer may:



```text

Update assigned task

```



Developer may not:



```text

Update another user's task

```



\---



\## QA



QA may:



```text

Review eligible InReview task

```



QA may not:



```text

Review ToDo

Review InProgress

Review Done

Review unauthorized project task

```



\---



\# 13. Comment Permissions



Comment deletion rules:



```text

Admin

→ May delete permitted comments

```



```text

Non-Admin

→ May delete only own comments

```



Unauthorized deletion must be rejected by the server.



\---



\# 14. Task Attachment Permissions



\## Upload



\### Admin



```text

Any project

```



\### Project Manager



```text

Authorized project

```



\### Scrum Master



```text

Authorized Scrum project

```



\### Developer



```text

No upload

```



\### QA Tester



```text

No normal Task Attachment upload

```



QA uses QA Evidence instead.



\---



\## Delete



\### Admin



```text

May delete any attachment

```



\### Project Manager



```text

May delete attachments inside authorized projects

```



\### Scrum Master



```text

May delete only own uploads

```



\### Developer



```text

No

```



\### QA Tester



```text

No

```



\---



\# 15. AI Authorization



AI Requirement Generation is available to:



```text

Admin

Project Manager

Scrum Master within authorized scope

```



Not available to:



```text

Developer

QA Tester

```



Every AI request must remain tied to an authorized project.



Authorization must be re-checked server-side for:



```text

Analyze

Refine

Generate

```



\---



\# 16. SRS Authorization



SRS Generation is available to:



\* Admin

\* Project Manager

\* Scrum Master within authorized scope



Not available to:



\* Developer

\* QA Tester



Project authorization must still be checked.



\---



\# 17. V-Model Authorization



V-Model project access must follow project authorization rules.



Scrum Master must not receive Scrum-specific permissions inside unauthorized V-Model projects.



Developer and QA may only access V-Model functionality appropriate to their authorized project scope.



\---



\# 18. Forbidden Access Behavior



When an authenticated user attempts to access a resource without sufficient authorization:



```text

HTTP 403 Forbidden

```



should be returned where appropriate.



The UI may hide unauthorized buttons, but:



```text

Hidden Button ≠ Security

```



Backend authorization remains authoritative.



\---



\# 19. Security Principle



Never rely only on:



\* Hidden buttons

\* Disabled fields

\* JavaScript

\* Route visibility

\* User-provided IDs

\* Hidden form inputs



Every sensitive action must be validated by the server.



\---



\# 20. Authorization Examples



\## Example 1 — Developer Access



```text

Developer

\+

Member of Project A

\+

Assigned Task #25

\+

Task #25 belongs to Project A

\+

Valid workflow status

=

Allowed

```



\---



\## Example 2 — Developer IDOR Attempt



```text

Developer

\+

Member of Project A

\+

Changes URL TaskId to task from Project B

=

Denied

```



\---



\## Example 3 — QA Invalid Review



```text

QA Tester

\+

Authorized Project

\+

Task Status = InProgress

=

Denied

```



because QA may review only eligible `InReview` tasks.



\---



\## Example 4 — Scrum Master V-Model Access



```text

Scrum Master

\+

Attempts Scrum management action

\+

Project Methodology = V-Model

=

Denied

```



\---



\## Example 5 — Project Manager Cross-Project Access



```text

Project Manager

\+

Authorized for Project A

\+

Attempts Project B management

=

Denied

```



unless separately authorized for Project B.



\---



\# 21. Authorization Design Rule



Authorization logic should be centralized.



Avoid scattered code such as:



```text

if role == "Admin"

if role == "Developer"

if role == "QA"

```



across Controllers and Views.



Later phases should use:



\* Central role constants

\* Authorization policies

\* Resource authorization handlers

\* Central workflow rules

\* Project membership services



\---



\# 22. Phase 1 Roles \& Permissions Status



Defined:



\* Admin

\* Project Manager

\* Scrum Master

\* Developer

\* QA Tester

\* Permission Matrix

\* Project Membership Authorization

\* Resource Assignment Authorization

\* Workflow-State Authorization

\* Comment Permissions

\* Task Attachment Permissions

\* QA Permissions

\* AI Permissions

\* SRS Permissions

\* V-Model Restrictions

\* Server-side Enforcement Rules



Detailed ASP.NET Core Identity roles, policies, handlers, and authorization code will be implemented in Phase 4 — Authentication \& Security Foundation.



