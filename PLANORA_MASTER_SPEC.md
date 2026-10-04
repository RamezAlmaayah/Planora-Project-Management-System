# Planora — Antigravity Master Specification

> **Purpose:** Build Planora as a complete, secure, high-performance, visually impressive graduation-project web application using **ASP.NET Core MVC + Onion Architecture**.  
> The system must be clean, maintainable, responsive, secure-by-design, and highly polished.  
> **Do not use React, Angular, or another SPA framework.**

---

# 1. Product Vision

Build **Planora**, an intelligent project-management platform that supports both:

- **Scrum / Agile**
- **V-Model**

The system must combine:

- Project management
- Team and role management
- Scrum backlog and sprint management
- Interactive task boards
- Developer → QA workflow
- V-Model phases and traceability
- Requirements management
- AI-assisted requirement generation
- IEEE-style SRS generation
- Issue / bug reporting
- Reports and dashboards
- Notifications
- Audit logging
- Strong security controls
- Professional responsive UI

The final result must feel like a polished commercial SaaS product, not a basic university CRUD project.

---

# 2. Mandatory Technology Stack

Use the following stack unless a change is technically necessary and clearly justified.

## Backend / Web

- ASP.NET Core MVC
- C#
- Razor Views
- ASP.NET Core Identity
- Cookie-based Authentication
- Entity Framework Core
- SQL Server
- FluentValidation where useful
- Serilog for structured logging
- Built-in ASP.NET Core Rate Limiting
- ASP.NET Core authorization policies
- SignalR only where real-time behavior adds real value

## Frontend

- Razor Views
- HTML5
- Modern CSS
- JavaScript ES Modules
- Bootstrap utilities/components only where useful, or a lightweight custom design system
- Fetch / AJAX for partial page interactions
- HTMX may be used selectively if it reduces unnecessary JavaScript complexity
- SortableJS may be used for Kanban drag-and-drop
- Chart.js may be used for charts
- Do not introduce React, Angular, Vue, or another SPA framework

## AI

- Google Gemini API
- The Gemini API key must exist **server-side only**
- Never expose AI secrets in HTML, JavaScript, browser storage, or Git

## Tooling

- Git
- GitHub
- EF Core Migrations
- Automated tests
- Dependency vulnerability checks

---

# 3. Architecture

Use:

> **Onion Architecture with Clean Architecture principles, SOLID, Dependency Inversion, Separation of Concerns, and modular feature organization.**

The solution should contain:

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

---

# 4. Onion Architecture Rules

## 4.1 Domain Layer

`Planora.Domain`

Contains only core business concepts.

Suggested structure:

```text
Domain/
├── Entities/
├── Enums/
├── ValueObjects/
├── Events/
├── Exceptions/
├── Specifications/
└── Rules/
```

Example entities:

- ApplicationUser abstraction if needed without framework coupling
- Project
- ProjectMember
- Sprint
- BacklogItem
- TaskItem
- QaReview
- Issue
- TaskComment
- Requirement
- RequirementTrace
- Notification
- ActivityLog
- ProgressReport

### Domain Rules

The Domain layer must:

- Contain business invariants
- Avoid references to MVC
- Avoid references to Entity Framework Core
- Avoid references to SQL Server
- Avoid references to Gemini
- Avoid references to Email providers
- Avoid UI-specific logic

The Domain must remain the core of the system.

---

## 4.2 Application Layer

`Planora.Application`

Contains use cases and application contracts.

Suggested structure:

```text
Application/
├── Abstractions/
├── DTOs/
├── Features/
│   ├── Authentication/
│   ├── Users/
│   ├── Projects/
│   ├── Members/
│   ├── Backlog/
│   ├── Sprints/
│   ├── Tasks/
│   ├── QA/
│   ├── Issues/
│   ├── Comments/
│   ├── Requirements/
│   ├── VModel/
│   ├── Reports/
│   ├── Srs/
│   └── Notifications/
├── Validators/
├── Mapping/
└── Common/
```

Application must contain use cases such as:

- CreateProject
- UpdateProject
- ArchiveProject
- AssignProjectMember
- RemoveProjectMember
- CreateBacklogItem
- CreateSprint
- StartSprint
- CompleteSprint
- CreateTask
- AssignTask
- UpdateTaskStatus
- SubmitTaskForQa
- PassQaReview
- FailQaReview
- CreateIssue
- AddTaskComment
- DeleteTaskComment
- CreateRequirement
- UpdateRequirement
- GenerateRequirementsWithAi
- ValidateAiInput
- GenerateSrs
- ExportSrs
- GenerateProjectReport

Application logic must be independent of UI and database implementation.

---

## 4.3 Infrastructure Layer

`Planora.Infrastructure`

Contains external implementations.

Suggested structure:

```text
Infrastructure/
├── Persistence/
│   ├── PlanoraDbContext.cs
│   ├── Configurations/
│   ├── Migrations/
│   ├── Repositories/
│   └── Seed/
├── Identity/
├── Authorization/
├── Security/
├── Gemini/
├── Email/
├── Logging/
├── Files/
└── Services/
```

Responsibilities:

- Entity Framework Core
- SQL Server persistence
- ASP.NET Core Identity persistence
- Gemini integration
- Email service
- Security token services
- File/export services
- Logging implementation
- External integrations

---

## 4.4 Web / Presentation Layer

`Planora.Web`

Use ASP.NET Core MVC.

Suggested structure:

```text
Web/
├── Controllers/
├── Areas/
│   └── Admin/
├── Views/
├── ViewModels/
├── Filters/
├── Middleware/
├── Authorization/
├── Extensions/
├── TagHelpers/
├── Components/
├── wwwroot/
│   ├── css/
│   ├── js/
│   ├── images/
│   └── icons/
└── Program.cs
```

Rules:

- Controllers must remain thin
- Do not put database queries directly in Controllers
- Do not put complex business logic in Views
- Do not put authorization only in the UI
- ViewModels must be separate from persistence entities
- Forms must use proper model binding and validation
- Sensitive operations must be POST/PUT/DELETE style actions, never unsafe GET actions

---

# 5. Dependency Direction

Dependencies must point inward.

```text
Planora.Web
     │
     ▼
Planora.Application
     │
     ▼
Planora.Domain

Planora.Infrastructure
     │
     ├── implements Application abstractions
     └── depends inward, never the opposite
```

The Domain must never depend on Infrastructure or Web.

---

# 6. Core Roles

Implement the following roles:

1. Admin
2. Project Manager
3. Scrum Master
4. Developer
5. QA Tester

Use ASP.NET Core Identity roles plus policy/resource-based authorization.

---

# 7. Authorization Model

Authorization must never depend only on role names.

Use all relevant layers:

```text
Authenticated User
      ↓
Role Permission
      ↓
Project Membership
      ↓
Resource Ownership / Assignment
      ↓
Workflow-State Permission
```

Examples:

- A Developer cannot access a project simply because the user has the Developer role.
- A user must be an authorized project member.
- A Developer may update only tasks assigned to that Developer where the workflow allows it.
- QA may review only eligible tasks in authorized projects.
- Scrum Master must access only authorized Scrum projects in Scrum-specific screens.
- A Project Manager must not automatically gain access to every project in the database.
- Backend authorization must remain authoritative even if a button is hidden in the UI.

Return `403 Forbidden` for authenticated users who lack authorization.

---

# 8. Permission Matrix

Implement a centralized, documented permission model.

| Feature | Admin | Project Manager | Scrum Master | Developer | QA Tester |
|---|---:|---:|---:|---:|---:|
| Manage Users | Yes | No | No | No | No |
| Manage Roles | Yes | No | No | No | No |
| Create Project | Yes | Yes | No | No | No |
| Update Authorized Project | Yes | Yes | Limited | No | No |
| Manage Project Members | Yes | Yes | Authorized Scrum scope | No | No |
| Manage Scrum Backlog | Yes | Yes | Yes | View | View |
| Manage Sprint | Yes | Yes | Yes | View | View |
| Create Scrum Task | Yes | Yes | Yes | No | No |
| Update Assigned Development Task | Yes | Limited | Limited | Yes | No |
| QA Review | Yes | No | No | No | Yes |
| Create Issue | Yes | Yes | Yes | Yes | Yes |
| Upload Task Attachment | Yes | Yes | Yes | No (view/download only) | No |
| Delete Task Attachment | Yes (any) | Yes (own project) | Own uploads only | No | No |
| Manage Requirements | Yes | Yes | Authorized scope | View | View |
| AI Requirement Generation | Yes | Yes | Authorized scope | No | No |
| SRS Generation | Yes | Yes | Authorized scope | No | No |
| Reports | Yes | Yes | Authorized Scrum scope | Limited | Limited |

All rows must still enforce project membership and resource-level checks.

---

# 9. Authentication

Use:

> **ASP.NET Core Identity + Secure Cookie Authentication**

Required features:

- Register where allowed
- Login
- Logout
- Forgot Password
- Reset Password
- Change Password
- Email verification if configured
- Account lockout
- Failed login protection
- Secure password hashing through ASP.NET Core Identity
- Secure reset tokens
- Session expiration
- Re-authentication for highly sensitive actions where appropriate

Cookie requirements:

- `HttpOnly = true`
- `Secure = true` in HTTPS environments
- Appropriate `SameSite`
- Short/controlled lifetime
- Sliding expiration only if deliberately configured
- No authentication data stored in localStorage

---

# 10. Project Management Requirements

The system must support:

- Create project
- Edit project
- Archive project
- Project status
- Project methodology
- Scrum methodology
- V-Model methodology
- Project dates
- Project objectives
- Project scope
- Project members
- Project member roles
- Project dashboard
- Progress indicators
- Activity timeline
- Reports
- Authorized-only access

A user must only see projects the user is authorized to access, except Admin where global access is deliberately permitted.

---

# 11. Project Membership

Implement a dedicated ProjectMember relationship.

Requirements:

- Prevent duplicate membership
- Validate member existence
- Validate project existence
- Restrict task assignee choices to eligible members
- Prevent assignment to users outside the project
- Enforce the same rule server-side
- Support member removal safely
- Prevent removal where active responsibility creates an invalid state unless reassigned or explicitly handled

---

# 12. Scrum Requirements

Support:

- Product Backlog
- Backlog Item CRUD
- Backlog priority
- Backlog status
- Sprint creation
- Sprint planning
- Start sprint
- Complete sprint
- Sprint backlog
- Scrum tasks
- Scrum Board
- Drag-and-drop interactions
- Project members as task assignees
- Sprint progress
- Backlog progress

Recommended task board statuses:

```text
ToDo
InProgress
InReview
Done
```

---

# 13. Completed Sprint Rule

A completed sprint must become read-only for task creation.

Requirements:

- Hide or disable Create Task in the UI
- Reject manual HTTP attempts server-side
- Preserve sprint history
- Avoid silently modifying completed sprint data

---

# 14. Developer → QA Workflow

Mandatory workflow:

```text
ToDo
  ↓
InProgress
  ↓
InReview
  ↓
QA Review
 ↙       ↘
Fail     Pass
 ↓        ↓
InProgress
          ↓
         Done
```

Rules:

### Developer

When development is completed:

```text
InProgress → InReview
```

The task becomes visible in the QA queue.

### QA Pass

```text
InReview → Done
```

### QA Fail

```text
InReview → InProgress
```

Return the task to the Developer with QA notes / report.

Invalid workflow transitions must be blocked on the server.

---

# 15. Automatic Backlog Completion

When a task passes QA and becomes Done:

- Check all tasks linked to the same backlog item
- If all related tasks are Done:
  - backlog item → Completed
- Otherwise:
  - backlog item remains In Sprint

This must be transactional and consistent.

---

# 16. QA Module

QA Tester must have an authorized QA queue.

Display:

- Task title
- Description
- Priority
- Assigned developer
- Deadline
- Current status
- Project
- Sprint
- Test action

QA review data should support:

- Pass / Fail
- Notes
- Test evidence if supported
- Test date
- QA user
- Related issue creation
- History

## QA Evidence Attachments

QA Tester may attach evidence files to a specific QA Review record (not to the task in general — this is distinct from the Task Attachments defined in Section 26.1, which are uploaded by Project Manager / Scrum Master for reference material, not by QA).

- Evidence (screenshots, short recordings, log files, PDFs) can be attached at the moment a QA Review is submitted, whether the outcome is Pass or Fail.
- Multiple files may be attached to a single review.
- Only the QA Tester performing that review may upload evidence to it; only Admin may delete QA evidence after submission (to preserve QA history integrity).
- Constraints follow the same pattern as Section 26.1: maximum file size 10 MB, allowed extensions only (example set: `png, jpg, jpeg, mp4, pdf, txt, log`), server-side extension/size enforcement regardless of client-side checks, safe internal file naming to prevent path traversal.
- Evidence must stay attached to its specific QA Review record (not to the task as a whole), so that if a task goes through multiple Pass/Fail cycles, each cycle's evidence remains distinctly traceable in the task's QA History rather than being mixed together.
- Evidence is visible to: Admin, the Project Manager/Scrum Master of that project, and the Developer assigned to the task (so the developer can see exactly what QA found on Fail).

---

# 17. V-Model Requirements

Support V-Model projects with appropriate phases such as:

- Requirements
- System Design
- Architecture / Detailed Design
- Implementation
- Unit Testing
- Integration Testing
- System Testing
- Acceptance / Validation

The exact configured phase model should be centralized and not hard-coded throughout the UI.

---

# 18. Requirement Management

Support Functional Requirements and Non-Functional Requirements.

Example IDs:

```text
FR-001
FR-002
NFR-001
NFR-002
```

Requirements should support:

- Unique identifier
- Name / title
- Description
- Type
- Priority
- Rationale
- Dependencies
- Status
- Project relationship
- Creation date
- Last update
- Traceability where applicable

---

# 19. V-Model Traceability

Where implemented, allow mapping between:

```text
Requirement
   ↓
Design
   ↓
Implementation
   ↓
Test / Verification
   ↓
Validation
```

Provide a traceability view / matrix showing coverage and missing links.

---

# 20. Gemini AI Integration

All Gemini communication must occur from the server.

```text
Razor UI
   ↓
MVC Controller
   ↓
Application Use Case
   ↓
IGeminiService
   ↓
Infrastructure Gemini Client
   ↓
Google Gemini API
```

Never expose:

- Gemini API key
- Provider secrets
- Raw internal prompts containing sensitive information unless explicitly intended

Implement:

- Timeout
- CancellationToken
- Retry only for safe transient failures
- Controlled fallback messages
- Structured parsing
- Schema validation
- Logging without secrets
- Protection against excessively large requests

---

# 21. AI Input Quality Validation

Before final AI generation, analyze the user input.

Return:

- Quality Score
- Quality Level
- Validation Message
- Missing Information
- Issues
- Suggestions

If the input is insufficient:

- Do not send the final generation request
- Show actionable suggestions
- Allow user to improve the input

## 21.1 Interactive Improvement Options

Suggestions must go beyond generic advice text. When the input is insufficient or ambiguous, the AI should return **concrete, selectable improvement options** the user can act on directly, rather than only a vague hint.

Examples of the difference:

- Weak (avoid): "Add more detail about user authentication."
- Actionable (required pattern): Present specific clarifying options such as "Should users log in with email or phone number?" with selectable choices (e.g. Email / Phone / Both), or short fill-in prompts for the exact missing piece (e.g. "What should happen if login fails 3 times?").

Requirements:

- Each Missing Information / Issue item should map to a concrete question or set of options the user can respond to, not just a restated problem.
- Selecting or answering an option updates the input description accordingly (append/merge into the draft text) rather than requiring the user to retype everything.
- The user can iterate through multiple improvement rounds before triggering final generation — there is no limit on how many times the input can be re-analyzed and refined.
- This interactive refinement step itself should remain lightweight (structured Gemini call, small payload) and must still respect the timeout/retry/fallback rules defined in Section 20.

## 21.2 Access Scope

AI Requirement Generation (input validation, interactive improvement, and final FR/NFR generation) follows the same access rule already defined in the Section 8 Permission Matrix:

- **Project Manager** — full access within their authorized projects
- **Scrum Master** — access within their authorized Scrum projects
- **Admin** — full access, consistent with Admin's unrestricted access across the system (Section 8)
- **Developer / QA Tester** — no access to this feature

## 21.3 Project Binding

Every AI-assisted requirement session is tied to a specific project from the moment it starts:

- The user must first be operating within an authorized project (per Section 7's Project Membership rule) before the AI input/generation screen is reachable at all.
- All quality validation, interactive improvement, and generated FR/NFR output are scoped and persisted against that specific project — never generated in a project-less or global context.
- Server-side authorization re-checks project membership on every request in this flow (analyze, refine, generate), not only when the screen first loads.

---

# 22. AI-Generated Functional Requirements

Each generated FR should use:

- Requirement ID
- Requirement Name
- Priority
- Description
- Business Rationale
- Dependency
- Exception Scenario

Generated requirements are suggestions until reviewed by a human user.

---

# 23. AI-Generated Non-Functional Requirements

Each generated NFR should use:

- NFR ID
- Category
- Description
- Priority
- Rationale
- Related Functional Requirements

Backend must validate related FR IDs.

Invalid references must be rejected, corrected, or removed safely.

---

# 24. SRS Generation

Generate a professional IEEE-style Software Requirements Specification.

Features:

- Generate
- Preview
- Review
- Edit where appropriate
- Save
- Export TXT
- Export PDF

Important:

Exporting an already generated SRS must reuse the generated content and must not trigger another Gemini generation request.

---

# 25. Issue / Bug Management

Issue entity should support:

- ID
- Project
- Related task
- Title
- Description
- Severity
- Priority
- Status
- Reporter
- Assignee
- CreatedAt
- UpdatedAt
- Resolution notes

Suggested statuses:

```text
Open
Assigned
InProgress
Resolved
Closed
Reopened
```

Workflow permissions must be validated server-side.

---

# 26. Task Comments

Support task comments with:

- Author
- Content
- Created timestamp
- Edited timestamp if editing exists

Deletion rules:

- Admin may delete permitted comments
- Non-admin may delete only their own comments
- Unauthorized deletion must be rejected by the server

---

# 26.1 Task Attachments

Support file attachments on tasks so Project Managers and Scrum Masters can attach reference material (specifications, design files, screenshots, documents) directly to a task.

## Entity

`TaskAttachment` related to `TaskItem`:

- File name (original)
- Stored path / storage key
- File size
- File type / extension
- Uploaded by (user)
- Uploaded at (timestamp)

## Who Can Upload

- **Project Manager** — Yes, for any task in an authorized project
- **Scrum Master** — Yes, for any task in an authorized Scrum project
- **Admin** — Yes, for any task in any project (unrestricted, consistent with Section 8 Permission Matrix)
- **Developer** — No upload permission; may view and download attachments on tasks assigned to them
- **QA Tester** — No upload permission here; QA-specific evidence continues to use the separate QA Review "Test evidence" mechanism defined in Section 16, and must not be mixed with general task attachments

## Who Can Delete

- **Admin** — may delete any attachment in any project
- **Project Manager** — may delete any attachment within their authorized projects
- **Scrum Master** — may delete only attachments they personally uploaded
- Deletion attempts outside these rules must be rejected server-side, following the same enforcement pattern already defined for Task Comments (Section 26)

## Constraints

- Maximum file size: **10 MB** per attachment
- Allowed extensions only (example set, to be centralized/configurable rather than hard-coded inline): `pdf, docx, xlsx, png, jpg, jpeg, zip`
- Reject any other extension server-side, regardless of client-side validation
- Sanitize and never trust the original file name; generate a safe internal storage name to prevent path traversal
- Store files outside direct public web access where practical, and serve downloads through an authorized controller action rather than a raw static file link
- Respect the Completed Sprint Rule (Section 13): once a sprint is completed, its tasks become read-only — new attachment uploads on tasks belonging to a completed sprint must be blocked server-side, consistent with the "no silent modification of completed sprint data" rule

## UI Placement

- Show an "Attachments" section on the Task Detail view, alongside Comments
- List existing attachments with uploader name, upload date, file size, and a download action
- Show the upload control only to roles permitted to upload (Section above); backend authorization remains authoritative regardless of what the UI shows

## Audit Trail

- Every upload and deletion of a task attachment must be recorded in the Audit Trail (Section 50), including who performed the action, when, and on which task

---

# 27. Notifications

Provide useful notifications such as:

- Task assigned
- Task sent to QA
- QA passed
- QA failed
- Issue assigned
- Sprint started
- Sprint completed
- Member added
- Requirement updated

Use SignalR only if live updates genuinely improve the experience.

Do not make the whole system dependent on real-time connections.

---

# 28. Dashboard

Create role-aware dashboards.

## Admin Dashboard

Show:

- Users
- Active projects
- Role distribution
- Recent activities
- Security-relevant audit events
- System overview
- Quick access to the full Activity Log & User Presence screen (Section 50.1) for a detailed, filterable view and live Online/Offline status

## Project Manager Dashboard

Show:

- Authorized projects
- Project progress
- Upcoming deadlines
- Team workload
- Sprint / phase summary
- Risks / issues
- Recent activity

## Scrum Master Dashboard

Show only authorized Scrum projects.

Show:

- Active sprint
- Sprint progress
- Backlog progress
- Blocked tasks
- QA returns
- Team workflow

## Developer Dashboard

Show:

- Assigned tasks
- Current work
- Priorities
- Deadlines
- QA-returned tasks
- Personal activity

## QA Dashboard

Show:

- InReview tasks
- Priority
- Deadlines
- Pending QA work
- Recent pass/fail history

---

# 29. UI / UX Design Direction

The interface must look premium, modern, calm, and professional.

## Mandatory Main Colors

Use a carefully balanced palette based on:

- White
- Off-white
- Light gray
- Cool gray
- Sky blue
- Soft blue
- Deep blue only for high-contrast elements

Suggested design tokens:

```css
--bg-main: #F7F9FC;
--bg-surface: #FFFFFF;
--bg-soft: #F1F5F9;

--text-primary: #0F172A;
--text-secondary: #64748B;
--text-muted: #94A3B8;

--border-soft: #E2E8F0;

--sky-50: #F0F9FF;
--sky-100: #E0F2FE;
--sky-300: #7DD3FC;
--sky-500: #0EA5E9;
--sky-600: #0284C7;

--blue-deep: #0F4C81;
```

These are guidelines, not an excuse to make every element blue.

---

# 30. Visual Style

Use:

- Clean spacious layouts
- Soft shadows
- Subtle borders
- Rounded corners
- Strong typography hierarchy
- Clear information density
- Excellent whitespace
- Modern cards
- Smooth hover states
- Elegant empty states
- Consistent iconography
- Professional charts
- Clear status badges
- Progressive disclosure

Avoid:

- Excessive gradients
- Neon colors
- Huge shadows
- Over-animation
- Glassmorphism everywhere
- Tiny text
- Dense tables with poor spacing
- Decorative animation that slows interaction

---

# 31. UI Personality

The experience should feel like:

- Linear
- Notion
- modern Jira
- modern GitHub
- modern SaaS dashboards

But **do not copy any product directly**.

Create a distinctive Planora visual identity.

---

# 32. Unique / Memorable UX Features

Add polished interactions that make Planora feel unusually refined without becoming gimmicky.

## 32.1 Global Command Palette

Keyboard shortcut:

```text
Ctrl + K
```

Allow fast navigation/actions:

- Go to project
- Open current sprint
- Search task
- Search requirement
- Create issue
- Open QA queue
- Open recent item

---

## 32.2 Context-Aware Quick Actions

Show quick actions based on:

- Current role
- Current project
- Current workflow state

Example:

Developer viewing an assigned task:

- Start Work
- Submit to QA
- Add Comment
- Report Issue

QA viewing InReview task:

- Pass
- Fail
- Report Issue

Never show unauthorized actions.

---

## 32.3 Smart Activity Timeline

For project and task pages show a visual timeline such as:

```text
Task Created
   ↓
Assigned to Ahmad
   ↓
Moved to InProgress
   ↓
Submitted to QA
   ↓
QA Failed
   ↓
Returned to Developer
   ↓
Submitted again
   ↓
QA Passed
```

---

## 32.4 Intelligent Empty States

Never show an empty blank box.

Examples:

> No tasks are waiting for QA. Your review queue is clear.

> This sprint has no tasks yet. Create the first task to begin planning.

Provide an authorized call-to-action where appropriate.

---

## 32.5 Progressive Detail Panels

Avoid constantly navigating away from the current screen.

Where appropriate, use:

- Slide-over detail panels
- Quick preview cards
- Expandable rows
- Lightweight modals

Do not turn every operation into a modal.

---

## 32.6 Interactive Kanban

Requirements:

- Smooth drag-and-drop
- Clear drop zones
- Optimistic UI only when safe
- Revert UI if server rejects transition
- Keyboard-accessible alternative
- Loading feedback
- No duplicate requests
- No frozen board

---

## 32.7 Focus Mode

Allow users to focus on:

- Current sprint
- My tasks
- QA queue
- Critical issues

Reduce unnecessary visual noise.

---

## 32.8 Smart Search

Provide fast global/project search for:

- Projects
- Tasks
- Requirements
- Issues
- Members

Add debounce.

Cancel stale requests.

Never send a server request on every keystroke without control.

---

## 32.9 Health / Progress Indicators

Use meaningful project health signals:

- On Track
- At Risk
- Delayed
- Blocked

Do not calculate misleading health metrics.

Explain the underlying reason in a tooltip or detail panel.

---

## 32.10 Skeleton Loading

Use skeletons for:

- Dashboard cards
- Tables
- Kanban
- Reports
- Profile pages

Avoid full-screen blocking spinners except for genuinely blocking operations.

---

## 32.11 Microinteractions

Use subtle transitions for:

- Button state
- Card hover
- Menu opening
- Status changes
- Toasts
- Successful save feedback
- Drag/drop

Typical animation duration:

```text
120ms – 220ms
```

Avoid animations that block user input.

Respect:

```css
prefers-reduced-motion
```

---

# 33. Responsive Design

Support:

- Desktop
- Laptop
- Tablet
- Mobile

Prioritize desktop/laptop because Planora is a project-management application.

Rules:

- Sidebar collapses cleanly
- Tables remain usable
- Kanban becomes horizontally scrollable where necessary
- Forms avoid cramped fields
- Touch targets remain usable
- No accidental horizontal page overflow

---

# 34. Accessibility

Implement:

- Semantic HTML
- Proper labels
- Keyboard navigation
- Visible focus indicators
- Accessible form errors
- ARIA only when necessary
- Sufficient contrast
- Screen-reader-friendly status messages
- `prefers-reduced-motion`
- Non-color-only status indicators

Target WCAG 2.1 AA patterns where practical.

Do not claim formal compliance unless it has actually been tested.

---

# 35. Performance Goals

The website must feel instant during normal use.

Performance principles:

- Async I/O
- Efficient EF Core queries
- Pagination
- Projection to DTO/ViewModel
- Avoid N+1 queries
- Proper indexes
- Use `AsNoTracking()` for read-only queries
- Avoid loading entire tables
- Lazy-load heavy content where appropriate
- Cache only safe and suitable data
- Debounce search
- Cancel stale browser/API requests
- Minify production CSS/JS
- Compress responses
- Enable HTTP caching for static assets
- Use versioned static assets
- Optimize images
- Avoid unnecessary libraries
- Avoid oversized JavaScript bundles

---

# 36. Perceived Performance

A fast-feeling application is as important as raw response time.

Use:

- Immediate button feedback
- Disabled state while submitting
- Skeletons
- Inline progress
- Toast feedback
- Non-blocking background updates
- Optimistic updates only for reversible low-risk operations
- Clear retry actions

The browser must never appear frozen while waiting for long operations.

---

# 37. Long-Running Operations

AI generation and report generation can take longer than normal CRUD.

Requirements:

- Show progress state
- Show elapsed time where useful
- Allow cancellation when technically possible
- Use CancellationToken server-side
- Disable duplicate submission
- Handle timeout gracefully
- Preserve user input after a recoverable failure
- Never create duplicate AI requests because the user clicked twice

---

# 38. Database Design

Use proper relational design.

Requirements:

- Primary Keys
- Foreign Keys
- Unique constraints
- Composite unique constraints where needed
- Indexes
- Correct delete behavior
- Transactions
- Concurrency strategy where needed
- CreatedAt
- UpdatedAt
- CreatedBy where useful
- UpdatedBy where useful

Prevent:

- Duplicate memberships
- Duplicate invalid assignments
- Orphan records
- Broken project relationships
- Invalid sprint/task relations
- Invalid requirement references

---

# 39. EF Core Rules

- Use Fluent API configurations
- Keep entity configuration separate
- Avoid giant `OnModelCreating`
- Use migrations
- Seed roles safely
- Seed demo accounts only in development
- Never seed production passwords in source code
- Use projections
- Use `AsNoTracking` for read-only queries
- Avoid unnecessary `.Include()` chains
- Use transactions for multi-step workflow changes
- Add indexes for frequent filters / joins

---

# 40. Security-by-Design

Security is mandatory from the first phase.

Design against common risks including:

- Broken Access Control
- IDOR
- SQL Injection
- XSS
- CSRF
- Authentication attacks
- Brute force
- Credential stuffing resistance where practical
- Session attacks
- Privilege escalation
- Mass Assignment / Overposting
- Injection
- Path traversal
- Unsafe file upload if file upload exists
- Sensitive data exposure
- Security misconfiguration
- Information disclosure
- API abuse
- Dependency vulnerabilities
- Insecure secret management

---

# 41. Input Validation

Validate on:

1. Client
2. Server

The server is authoritative.

Validate:

- Required values
- Length
- Format
- Email
- Dates
- Ranges
- Enum values
- IDs
- Project relationships
- Membership
- Task ownership
- Workflow states
- File type / size if uploads exist

Never trust hidden inputs.

---

# 42. Output Encoding / XSS

- Use Razor's default output encoding
- Avoid `Html.Raw()` for untrusted data
- Avoid injecting user input directly into scripts
- Sanitize rich HTML only if rich text is intentionally supported
- Prefer plain text for comments unless rich content is a real requirement
- Use CSP-compatible patterns

---

# 43. CSRF Protection

Because MVC uses cookie authentication:

- Enable antiforgery protection
- All state-changing forms must include antiforgery tokens
- Validate antiforgery token server-side
- Never perform destructive operations through GET

Protect actions such as:

- Create
- Edit
- Delete
- Assign
- Role change
- QA Pass
- QA Fail
- Sprint state transitions
- Password change

---

# 44. SQL Injection Protection

- Use Entity Framework Core parameterized queries
- Do not concatenate user input into SQL
- If raw SQL is ever required, parameterize it
- Review dynamic sort/filter behavior carefully

---

# 45. Overposting Protection

Never bind persistence entities directly from untrusted forms.

Use ViewModels / request DTOs with explicit allowed fields.

Example:

```text
EditUserProfileViewModel
```

must not accidentally allow:

```text
Role = "Admin"
IsAdmin = true
ProjectOwnerId = ...
```

---

# 46. Secure Headers

Configure appropriate security headers, such as:

- Content-Security-Policy
- X-Content-Type-Options
- Referrer-Policy
- Permissions-Policy
- Frame protection / `frame-ancestors`
- HSTS in production

Do not blindly add headers without verifying compatibility.

---

# 47. Rate Limiting

Rate-limit sensitive endpoints such as:

- Login
- Forgot password
- Reset flows
- AI generation
- Expensive searches
- Expensive report generation

Return friendly errors.

---

# 48. Secrets Management

Never commit secrets.

Use:

- User Secrets for local development
- Environment variables / secure secret store for production
- Configuration validation at startup

Never expose:

- Connection strings
- API keys
- Email credentials
- Production secrets

---

# 49. Logging

Use structured logging with Serilog.

Log useful events such as:

- Login success/failure
- Unauthorized attempts
- Role changes
- User creation/deletion
- Project creation/deletion
- Membership changes
- QA decisions
- Critical workflow transitions
- AI failures
- Application exceptions

Never log:

- Passwords
- Reset tokens
- Authentication cookies
- API keys
- Sensitive secrets

Use correlation / trace IDs.

---

# 50. Audit Trail

Create a user-friendly audit/activity trail where appropriate.

Track:

- Who performed the action
- What action occurred
- Target resource
- Timestamp
- Important old/new state where safe and useful

Do not expose sensitive security information in the UI.

---

# 50.1 Admin Activity Log & User Presence

Admin must have a dedicated screen — separate from the general Audit Trail (Section 50) — that gives full visibility into user activity and current presence across the system. This screen is Admin-only and is not exposed to any other role.

## Activity Log

- Show every tracked action across the whole system (not scoped to a single project), reusing the same audit data captured in Section 50: who performed the action, what the action was, which resource it affected, and when.
- Support filtering by user, by project, by action type, and by date range.
- Support pagination (per Section 55 rules) — this log will grow continuously and must never be loaded as one unbounded list.
- Each entry should be readable in plain language (e.g. "Ahmad moved Task #124 to InReview — 2026-09-02 14:32"), not a raw technical dump.
- This log is read-only for Admin; entries are never editable and are only ever added, never modified.

## Online / Offline Presence

- Show, in real time, which users are currently **Online** and which are **Offline**.
- A user counts as Online while they have an active authenticated session (e.g. an active, non-expired login) interacting with the system; a user becomes Offline after logout or after their session/cookie expires from inactivity.
- Use SignalR for this specific screen — presence is one of the few places in Planora where real-time behavior adds genuine value (consistent with Section 27's rule: use SignalR only where it adds real value, not as a system-wide dependency).
- If SignalR is temporarily unavailable, the presence view should degrade gracefully to a periodically refreshed snapshot rather than breaking the page — presence is a monitoring convenience, not a critical workflow, so it must never block or affect core project-management functionality (consistent with Section 85, High Availability).
- Show, for each user: name, role, current status (Online/Offline), and last-seen timestamp when Offline.

## Access & Security

- Restrict this entire screen to Admin only, enforced server-side (not just hidden in the UI), consistent with Section 7's authorization rules.
- This screen must not display sensitive data such as passwords, tokens, or raw session identifiers — only the activity/presence information described above.

---

# 51. Global Error Handling

Implement centralized exception handling.

Return/show safe errors.

Example structure for AJAX/API-like MVC responses:

```json
{
  "status": 400,
  "message": "Validation failed",
  "errors": {},
  "traceId": "..."
}
```

Never expose:

- Stack traces
- SQL
- Connection strings
- Server paths
- API keys
- Internal framework details

---

# 52. Reliability

The system must:

- Avoid duplicate submissions
- Use database transactions for critical workflows
- Handle external service failures
- Preserve consistent state
- Recover safely from expired sessions
- Handle stale data / concurrency where relevant
- Avoid partial workflow updates
- Provide retry only where safe

---

# 53. Concurrency

Protect important multi-user workflows.

Examples:

- Two managers editing the same project
- Two QA users attempting to review the same task
- Sprint completion while tasks are changing

Use optimistic concurrency where appropriate.

Present a friendly conflict message rather than silently overwriting changes.

---

# 54. Caching

Use caching selectively.

Good candidates:

- Static reference data
- Non-sensitive read-heavy lookup data

Avoid blindly caching:

- User-specific permissions
- Highly sensitive pages
- Frequently changing workflow state

Do not return one user's cached private data to another user.

---

# 55. Search / Pagination

Large lists must use:

- Server-side pagination
- Filtering
- Sorting
- Search

Examples:

- Users
- Projects
- Tasks
- Issues
- Requirements
- Activity logs

Never load thousands of records into a Razor page just to filter them in JavaScript.

---

# 56. Reports

Reports may include:

- Project summary
- Sprint progress
- Team workload
- QA results
- Issues
- Requirement coverage
- V-Model traceability
- Progress history

Reports must:

- Respect authorization
- Avoid heavy synchronous queries
- Use efficient projections
- Provide loading feedback
- Export safely if export is supported

---

# 57. Navigation

Use a professional responsive shell:

```text
Top Bar
├── Global Search
├── Command Palette
├── Notifications
└── Profile

Sidebar
├── Dashboard
├── Projects
├── Scrum
├── V-Model
├── Tasks
├── QA
├── Issues
├── Requirements
├── Reports
└── Admin (authorized only)
```

Navigation must adapt to role and authorization.

---

# 58. Design System

Create reusable UI primitives:

- Button
- Icon Button
- Card
- Badge
- Status Badge
- Input
- Select
- Textarea
- Modal
- Drawer / Slide-over
- Tooltip
- Toast
- Table
- Pagination
- Empty State
- Skeleton
- Breadcrumb
- Tabs
- Dropdown
- Avatar
- Timeline
- Progress bar
- Confirmation dialog

Use consistent sizes and spacing.

---

# 59. Typography

Use a professional modern font that is legally and easily distributable through normal web delivery.

Prefer clean fonts such as:

- Inter
- Manrope
- system-ui fallback

Typography should emphasize readability over decoration.

---

# 60. Tables

Tables must support when appropriate:

- Search
- Sort
- Filter
- Pagination
- Sticky headers for long views
- Row actions
- Clear hover state
- Responsive behavior
- Empty state
- Loading state

Do not place ten icon-only actions in every row.

Use a clean contextual menu.

---

# 61. Forms

Forms must have:

- Labels
- Helpful placeholders only where useful
- Inline validation
- Error summary for complex forms
- Required field indication
- Disabled/loading state
- Confirmation for destructive actions
- Preserve input after normal validation errors

Do not reset a form after a server error.

---

# 62. Toasts and Feedback

Use toasts for:

- Save success
- Assignment success
- Workflow update
- Minor recoverable error

Use inline/alert feedback for:

- Validation errors
- Permissions
- Critical failures
- Important blocking issues

Avoid showing a toast for every tiny interaction.

---

# 63. Confirmation Patterns

Destructive actions need clear confirmation.

Examples:

- Delete user
- Delete project
- Remove member
- Delete requirement

The dialog should explicitly name the affected item.

For highly destructive actions, consider typed confirmation.

---

# 64. No-Freezing Requirement

The system must not appear to freeze during normal operation.

Antigravity must actively avoid:

- Synchronous blocking I/O
- Massive database reads
- Repeated duplicate AJAX calls
- Infinite frontend loops
- Long operations on the main UI thread
- Huge unoptimized images
- Excessive third-party JS
- Full page reloads for every tiny interaction where partial update is better

Every network interaction needs:

- Loading state
- Error state
- Completion state

---

# 65. Clean Code Requirements

Mandatory:

- SOLID
- DRY where it improves maintainability
- KISS
- Separation of Concerns
- Dependency Injection
- Small focused methods
- Meaningful names
- No magic strings for roles/statuses
- Centralized constants/enums/policies
- No giant Controllers
- No giant Services
- No business logic duplicated across UI and backend
- No dead code
- No commented-out obsolete code
- No `TODO` left in completed production paths
- Nullable reference types enabled
- Async methods end in `Async`
- Pass `CancellationToken` where appropriate

---

# 66. Do Not Over-Engineer

Do not add:

- Microservices
- Message brokers
- Kubernetes
- Event sourcing
- CQRS frameworks
- Redis
- Elasticsearch

unless a real requirement justifies them.

This project should use a clean **modular monolith**.

Complexity must serve the product, not impress through unnecessary infrastructure.

---

# 67. Folder / File Quality

Avoid files that become excessively large.

Split by feature / responsibility.

A controller should coordinate a use case, not implement the entire use case.

A Razor page should render and bind UI, not contain business rules.

JavaScript should be modular and page/feature scoped.

---

# 68. Security Testing

Explicitly test:

- Anonymous user accessing protected route
- Developer accessing another project
- Developer modifying another user's task
- QA reviewing task outside authorized project
- QA reviewing task not in `InReview`
- Scrum Master accessing unauthorized project
- Scrum Master accessing V-Model-only data
- User changing ProjectId manually
- User assigning non-member
- User creating task in completed sprint
- Non-admin deleting another user's comment
- Normal user attempting admin endpoint
- CSRF on state-changing actions
- Overposting role/admin fields
- XSS payload in comments
- SQL injection-like input
- Excessive login attempts
- Expired authentication session
- AI endpoint abuse / duplicate submission

---

# 69. Testing Strategy

Implement:

## Unit Tests

Test:

- Domain rules
- Workflow transitions
- Validation
- Permission decision logic
- Requirement rules

## Integration Tests

Test:

- Database operations
- Authentication
- Authorization
- MVC actions
- Critical workflows
- Gemini abstraction using mocked/fake external service
- Export flows

## System Tests

Test complete scenarios:

```text
Login
→ Project
→ Sprint
→ Task
→ Developer work
→ InReview
→ QA Fail
→ Developer rework
→ InReview
→ QA Pass
→ Done
```

## UAT

Validate role workflows from a normal user's point of view.

## Performance Tests

Check:

- Login
- Dashboard
- Project list
- Task list
- Kanban
- Reports
- AI quality analysis
- AI generation

---

# 70. CI / Quality Gates

Before accepting a phase:

- Restore dependencies
- Build solution
- Run tests
- Check migrations
- Check lint/static analysis where configured
- Check vulnerable dependencies
- Ensure no secrets are committed
- Verify authorization tests
- Verify no broken navigation

Never move to the next major phase while the current build is broken.

---

# 71. Development Workflow for Antigravity

Antigravity must work phase-by-phase.

## Phase 1 — Requirements and Domain Analysis

Deliver:

- Domain model
- User roles
- Permission matrix
- Main workflows
- Assumptions
- Acceptance criteria

Do not start random coding before this is coherent.

---

## Phase 2 — Solution Architecture

Deliver:

- Onion projects
- Dependency setup
- Folder structure
- Common abstractions
- Architecture decision notes

Build the empty solution successfully.

---

## Phase 3 — Database Design

Deliver:

- Entities
- Relationships
- EF configurations
- Constraints
- Indexes
- Initial migration
- Role seed

Verify migration succeeds.

---

## Phase 4 — Authentication and Security Foundation

Deliver:

- ASP.NET Identity
- Login / Logout
- Password reset
- Secure cookies
- Role seed
- Authorization policies
- Project-level authorization foundation
- CSRF
- Rate limiting
- Global exception handling
- Serilog
- Security headers

Test before continuing.

---

## Phase 5 — Project and Membership Management

Deliver:

- Projects
- Members
- Project authorization
- Project dashboard shell

Test access isolation.

---

## Phase 6 — Scrum

Deliver:

- Backlog
- Sprints
- Tasks
- Scrum Board
- Completed sprint rules
- Assignment restrictions

Test all business rules.

---

## Phase 7 — Developer / QA Workflow

Deliver:

- Developer task workflow
- InReview queue
- QA Pass
- QA Fail
- Automatic backlog completion

Test the complete workflow.

---

## Phase 8 — V-Model

Deliver:

- V-Model phases
- V-Model project view
- V-Model tasks
- Progress

---

## Phase 9 — Requirements / Traceability

Deliver:

- Functional requirements
- Non-functional requirements
- Requirements management
- Traceability matrix where supported

---

## Phase 10 — Issues and Comments

Deliver:

- Issues
- Comments
- Permissions
- Activity history

---

## Phase 11 — Gemini / AI

Deliver:

- Server-only Gemini integration
- Input quality check
- FR generation
- NFR generation
- Validation
- Timeout/error handling

---

## Phase 12 — SRS / Reports

Deliver:

- SRS
- Preview
- TXT export
- PDF export
- Reports

---

## Phase 13 — Premium UI / UX Pass

Do a dedicated design pass.

Review:

- Every page
- Spacing
- Hierarchy
- Responsiveness
- Loading states
- Empty states
- Error states
- Hover states
- Keyboard navigation
- Mobile behavior
- Skeletons
- Toasts
- Command palette
- Interactive details
- Microinteractions

No page should look like raw Bootstrap scaffolding.

---

## Phase 14 — Performance Hardening

Review:

- EF queries
- N+1
- Pagination
- Static assets
- JavaScript
- Large images
- Cache rules
- AI calls
- Duplicate requests
- Expensive reports

Profile before guessing.

---

## Phase 15 — Security Audit

Audit:

- Authentication
- Authorization
- Broken access control
- IDOR
- CSRF
- XSS
- SQL injection
- Overposting
- Rate limiting
- Cookie security
- Secrets
- Logging
- Error disclosure
- AI endpoint protection
- Dependency vulnerabilities

Fix findings before completion.

---

## Phase 16 — Final Regression

Run:

- Unit tests
- Integration tests
- Security tests
- Main system workflows
- Role workflows
- Responsive UI review
- Broken-link review
- Empty state review
- Error state review

---

# 72. Definition of Done

A feature is **not Done** merely because the screen exists.

A feature is Done only when:

```text
UI                         ✓
Backend behavior           ✓
Database                   ✓
Validation                 ✓
Authentication             ✓
Authorization              ✓
Business rules             ✓
Error handling             ✓
Loading state              ✓
Empty state                ✓
Security review            ✓
Tests                      ✓
Responsive behavior        ✓
Successful build           ✓
```

---

# 73. Acceptance Standard for Visual Quality

Before calling the project complete:

- No raw default forms
- No unfinished white pages
- No inconsistent buttons
- No broken alignment
- No accidental horizontal overflow
- No ugly loading spinner on every page
- No duplicate headings
- No placeholder icons
- No missing empty states
- No visually inconsistent tables
- No unstyled validation output
- No visible developer/debug information

Every screen must look intentional.

---

# 74. Acceptance Standard for Performance

Normal navigation must feel responsive.

Do not claim absolute zero latency or zero defects.

Instead ensure:

- No known UI freeze
- No unnecessary blocking operation
- No obvious N+1 query
- No unbounded list query
- No duplicate request bug
- No uncontrolled repeated AI call
- No heavy screen that loads unnecessary data
- Graceful handling of slow operations

---

# 75. Acceptance Standard for Security

Do not claim the system is “100% secure.”

The requirement is:

> Build using secure defaults, defend against major common web vulnerabilities, enforce least privilege, test critical authorization paths, and leave no known high-severity security defect unresolved at final acceptance.

---

# 76. Antigravity Coding Rules

When implementing:

1. Inspect existing code before changing it.
2. Preserve architecture boundaries.
3. Do not duplicate an existing service or component.
4. Make the smallest coherent change.
5. Build after meaningful changes.
6. Run relevant tests.
7. Fix the root cause instead of hiding the error.
8. Never disable security merely to make a test pass.
9. Never remove validation because a form fails.
10. Never hard-code role bypasses.
11. Never hard-code production credentials.
12. Never expose stack traces to end users.
13. Never move directly to the next phase while the current one has compile/runtime errors.
14. Keep the application runnable throughout development.
15. Prefer stable, maintained dependencies.
16. Avoid dependency bloat.
17. Document non-obvious architecture/security decisions.

---

# 77. Final Product Goal

The final Planora application should feel:

- Fast
- Stable
- Modern
- Interactive
- Elegant
- Secure
- Organized
- Easy to understand
- Easy to maintain
- Visually distinctive
- Professionally engineered

It must demonstrate not just CRUD functionality, but strong software-engineering practices.

---

# 78. Final Instruction to Antigravity

Build the system as a **secure modular monolith using ASP.NET Core MVC and Onion Architecture**.

Prioritize:

```text
Correctness
    ↓
Security
    ↓
Maintainability
    ↓
Performance
    ↓
Usability
    ↓
Visual polish
```

Do not sacrifice correctness or security for visual effects.

For every major feature:

```text
Analyze
  ↓
Design
  ↓
Implement
  ↓
Build
  ↓
Test
  ↓
Security Check
  ↓
UX Review
  ↓
Continue
```

When a requirement conflicts with another requirement, stop and resolve the conflict based on:

1. Security
2. Data integrity
3. Core business rules
4. Architecture consistency
5. User experience

The finished system must be presentation-ready, demonstrable, maintainable, and suitable for a high-quality graduation project.


---

# 79. Simple and Effortless UX

The UX must be intentionally simple, predictable, and easy to learn.

The user should be able to complete common tasks with minimal steps and without needing technical knowledge.

Mandatory UX principles:

- Keep navigation clear and consistent
- Avoid unnecessary screens and excessive clicks
- Keep forms short and logically grouped
- Show only relevant actions for the current role and context
- Use progressive disclosure for advanced options
- Use clear labels instead of technical terminology
- Preserve user input after recoverable errors
- Provide visible feedback after every important action
- Use confirmations only when genuinely necessary
- Avoid modal overload
- Avoid crowded dashboards
- Use sensible defaults
- Minimize cognitive load
- Keep workflows consistent across the system

Examples:

```text
Create Task
→ Select Project
→ Select Sprint
→ Select Backlog Item
→ Enter Task Details
→ Assign Eligible Member
→ Save
```

Do not introduce unnecessary intermediate pages.

The interface must feel simple even when the business logic is complex.

---

# 80. High Maintainability

Maintainability is a mandatory system quality attribute.

The system must be easy to understand, debug, test, update, and extend without forcing major rewrites.

Requirements:

- Keep clear Onion Architecture boundaries
- Keep business rules outside Controllers and Views
- Use dependency injection
- Use focused interfaces
- Use reusable services
- Use feature-based organization
- Keep configuration centralized
- Keep role names, policies, statuses, and constants centralized
- Avoid duplicated business logic
- Keep methods small and focused
- Avoid giant service classes
- Avoid tightly coupled modules
- Write meaningful names
- Keep code self-explanatory
- Add comments only for non-obvious decisions
- Keep automated tests for critical business rules
- Keep database migrations controlled
- Keep environment-specific configuration separate
- Use structured logging
- Use consistent error handling

A future developer should be able to locate and modify a feature without searching through unrelated code.

---

# 81. Extensibility

The system must support future features without redesigning the entire application.

Examples of future additions that should be possible:

- New project methodologies
- New roles
- New task workflow states
- New report types
- New AI providers
- New export formats
- New notification channels
- New project health rules
- Additional requirement types
- Future mobile/API clients

Design rules:

- Depend on abstractions where external integrations are involved
- Avoid hard-coded provider-specific logic in business code
- Centralize workflow rules
- Centralize authorization policies
- Avoid scattered `if role == ...` logic
- Use strategy/policy patterns where they provide real value
- Keep external integrations replaceable

Example:

```text
Application
    ↓
IAiRequirementService
    ↓
GeminiRequirementService
```

A future AI provider should be replaceable without rewriting project-management logic.

---

# 82. Adaptability

Planora must be adaptable to changing business and UI requirements.

The implementation should support changes such as:

- New workflow statuses
- New validation rules
- New roles and permissions
- New dashboard widgets
- New project methodology rules
- Changes to AI prompts or AI provider
- New reporting requirements
- Different deployment environments
- Changes in branding and theme

Prefer configuration and modular rules over repeated hard-coded decisions.

Do not make the system so generic that it becomes difficult to understand.

Adaptability must come from clean structure, not unnecessary abstraction.

---

# 83. High Performance Requirements

Performance must be treated as a design requirement, not as a final optimization step.

Use:

- Async I/O
- Efficient EF Core queries
- `AsNoTracking()` for read-only queries
- Projection with `Select()`
- Pagination
- Database indexes
- Controlled `Include()` usage
- Elimination of N+1 queries
- Server-side filtering and sorting
- Debounced search
- Cancellation of stale requests
- Minified production assets
- Brotli/Gzip response compression
- Browser caching for static assets
- Optimized SVG/WebP/AVIF assets
- Lightweight JavaScript
- Efficient partial updates
- Performance profiling before release

Performance targets under normal expected project load:

```text
Immediate UI feedback:             < 100–200 ms
Typical server operation:          ideally < 500 ms
Common list/data retrieval:        < 1 second
Dashboard initial useful content:  approximately 1–2 seconds
Search debounce:                   approximately 300 ms
Normal report generation:          < 3 seconds when practical
```

AI operations are exceptions to normal request latency.

For AI operations:

- Never freeze the interface
- Show progress/loading feedback
- Prevent duplicate requests
- Allow cancellation when technically possible
- Use timeout and controlled retry
- Preserve user input after recoverable failure

Do not claim a performance target as validated until it has actually been measured in the target environment.

---

# 84. No Unnecessary Full-Page Reloads

Normal interactive operations should not reload the complete page unless a full reload is technically necessary.

Use Fetch/AJAX, partial views, or lightweight dynamic updates for actions such as:

- QA Pass
- QA Fail
- Task status updates
- Kanban movement
- Add comment
- Delete comment
- Search
- Filtering
- Sorting
- Pagination
- Notifications
- Assign member
- Update assignee
- Quick actions
- Status changes

Expected pattern:

```text
User Action
    ↓
Immediate UI Feedback
    ↓
Small Server Request
    ↓
Authorization + Validation
    ↓
Database Update
    ↓
Update Only Affected UI
    ↓
Toast / Inline Confirmation
```

Avoid:

```text
Click
→ Blank page
→ Full reload
→ Reload entire dashboard
→ User loses context
```

---

# 85. High Availability

The system should be designed to remain available and usable during normal failures and maintenance scenarios.

Requirements:

- Use graceful error handling
- Keep external AI failures isolated from core project-management functionality
- A Gemini outage must not prevent users from managing projects, tasks, sprints, QA, or issues
- Configure health checks for critical dependencies
- Support safe application restart
- Use stateless web behavior where practical
- Avoid storing critical application state only in server memory
- Use production-safe database connection resiliency
- Use controlled retries for transient database/network errors where safe
- Use timeouts for external services
- Avoid single long-running request blocking unrelated requests
- Provide maintenance-friendly deployment configuration
- Keep database migrations backward-conscious where practical
- Support backup and restore procedures for production data

If deployed to infrastructure that supports multiple application instances, the architecture should not prevent horizontal scaling.

High availability must not be falsely claimed unless the deployment infrastructure actually provides redundancy.

---

# 86. Reliability

Planora must behave consistently and preserve valid system state.

Reliability requirements:

- Use database transactions for critical multi-step workflows
- Prevent partial state changes
- Prevent duplicate submissions
- Use idempotent patterns where appropriate
- Validate workflow transitions server-side
- Handle external service failures safely
- Preserve data integrity
- Protect against concurrency conflicts
- Return controlled error messages
- Recover safely from expired sessions
- Avoid silent failures
- Log unexpected failures with trace IDs
- Do not lose user input unnecessarily
- Do not report success before persistence succeeds

Examples of critical transactional workflows:

```text
QA Pass
→ Save QA Review
→ Update Task
→ Check Backlog Completion
→ Update Backlog if required
→ Commit once
```

If one critical step fails, the transaction must not leave the workflow half-completed.

---

# 87. Responsiveness

The interface must be responsive in both meanings:

## A. Device Responsiveness

Support:

- Desktop
- Laptop
- Tablet
- Mobile

Requirements:

- Responsive sidebar
- Responsive tables
- Horizontal Kanban scrolling where appropriate
- Adaptive cards
- Usable forms
- Touch-friendly controls
- No accidental page overflow
- No overlapping controls
- Readable typography at all supported sizes

## B. Interaction Responsiveness

Every user action must immediately produce visible feedback.

Examples:

- Button changes to loading state
- Save button becomes temporarily disabled
- Dragged task shows movement state
- Search indicates loading when necessary
- Long action shows progress
- Error appears near the relevant control
- Successful updates appear without unnecessary page reload

The system should never leave the user wondering whether a click was registered.

---

# 88. Maintainability and Operational Simplicity

Planora must not become difficult to maintain after delivery.

Avoid operational complexity that does not provide clear value.

Prefer:

```text
ASP.NET Core MVC
+
Onion Architecture
+
SQL Server
+
EF Core
+
Modular Monolith
```

over unnecessary infrastructure.

Do not add:

- Microservices
- Distributed messaging
- Kubernetes
- Multiple databases
- Complex distributed cache
- Separate frontend deployment

unless a real requirement later justifies them.

Maintenance requirements:

- One clear solution structure
- Consistent naming
- Central configuration
- Automated migrations
- Health checks
- Structured logs
- Clear environment setup
- Simple local development
- Clear README
- Minimal required manual steps
- Stable dependency selection
- Easy backup strategy
- Easy deployment procedure
- Easy troubleshooting through logs and trace IDs

The project must remain understandable for a developer who did not originally build it.

---

# 89. Performance and Stability Verification

Before final acceptance, Antigravity must perform a dedicated performance and stability review.

Inspect:

- Slow SQL queries
- N+1 queries
- Missing indexes
- Oversized response payloads
- Repeated AJAX requests
- Duplicate submissions
- Excessive JavaScript execution
- Large CSS/JS bundles
- Large images
- Slow dashboard widgets
- Unnecessary full-page reloads
- Slow reports
- Duplicate Gemini calls
- Memory-heavy operations
- Long-running synchronous code
- Missing pagination
- Stale requests
- Unnecessary database round-trips

Measure before and after important optimizations.

Do not “optimize” by removing validation, authorization, logging, or security controls.

---

# 90. Final Quality Attribute Priorities

The final system must prioritize all of the following together:

```text
Security
+
Correctness
+
Simple UX
+
Performance
+
Availability
+
Reliability
+
Responsiveness
+
Maintainability
+
Extensibility
+
Adaptability
+
Accessibility
+
Visual Quality
```

These are not optional polish items.

They are part of the system requirements.

The implementation should balance them without sacrificing security or data integrity.

---

# 91. Updated Final Quality Instruction to Antigravity

Planora must be built as a polished, maintainable, secure, responsive, and extensible system.

The final experience must satisfy these principles:

```text
Simple to use
Fast to interact with
Easy to maintain
Easy to extend
Easy to adapt
Reliable under normal use
Graceful under failures
Responsive across devices
Secure by design
Visually impressive without being heavy
```

Do not create visual effects that reduce usability or performance.

Do not create abstractions that make maintenance harder.

Do not sacrifice backend validation for UI convenience.

Do not sacrifice security for speed.

For every feature, review:

```text
Functionality
↓
Security
↓
Data Integrity
↓
Performance
↓
Reliability
↓
UX Simplicity
↓
Responsiveness
↓
Maintainability
↓
Extensibility
↓
Accessibility
↓
Visual Polish
```

The finished Planora application must feel fast and simple to the user while remaining clean and structured internally.

---

# 92. Local Development & Demo Setup

Planora will initially run **locally only** (developer machine, no cloud deployment, no public hosting). The following requirements exist to make local setup fast, repeatable, and demo-ready — not to prepare for production deployment.

## 92.1 Database

- Use **SQL Server LocalDB** or **SQL Server Express** for local development.
- Connection string must live in `appsettings.Development.json` or **User Secrets**, never committed with real credentials.
- Apply EF Core Migrations automatically on startup in the Development environment only (`Database.Migrate()` behind an environment check), or document the manual `dotnet ef database update` step clearly in the README.

## 92.2 Secrets Management (Local)

- Use `dotnet user-secrets` for:
  - Gemini API key
  - SQL Server connection string (if it contains credentials)
  - Any SMTP/email testing credentials
- `appsettings.json` must never contain real secrets, even for local-only use — if the repository is ever pushed to GitHub, secrets must not be exposed.
- Provide an `appsettings.Example.json` or a documented list of required keys so the project can be set up from a clean clone.

## 92.3 Email (Local Substitute)

Since there is no production email server, choose one approach and apply it consistently:

- **Option A (recommended):** Use a local SMTP capture tool such as **Papercut SMTP** or **Mailtrap sandbox** so Forgot Password / email verification flows work end-to-end without sending real email.
- **Option B (fallback):** Disable email verification for local/demo use, and surface the password-reset token directly in the UI or logs, clearly marked as a development-only shortcut.

Whichever option is used, the Infrastructure layer's email abstraction (`IEmailSender` or equivalent) must remain unchanged — only the local configuration/provider differs, so switching to a real provider later requires no code changes in Domain/Application.

## 92.4 File Uploads (If Implemented)

- Store locally under `wwwroot/uploads/` (or a configurable local folder outside `wwwroot` if files should not be publicly served directly).
- Enforce the same size/extension/type validation rules defined in Section 40–41 even though this is local-only — this is a habit that also protects the demo from broken/oversized uploads.

## 92.5 HTTPS & Cookies Locally

- Trust the .NET development certificate (`dotnet dev-certs https --trust`) so `Secure = true` cookies and HTTPS-only behavior work correctly during local runs.
- Do not weaken cookie security settings "because it's local" — the local environment should behave the same way security-wise as it would in a real deployment, so the graduation defense/demo reflects real system behavior.

## 92.6 Seed Data for Demo

Local/demo seeding is a first-class requirement, not an afterthought — the application must be genuinely demo-ready immediately after first run and migration, without manual data entry.

Seed, in Development environment only:

- All five roles (Admin, Project Manager, Scrum Master, Developer, QA Tester)
- At least one demo user per role, with clearly documented credentials (e.g. in the README)
- At least one Scrum project and one V-Model project, each with realistic members
- For the Scrum project: a populated backlog, at least one sprint (active or completed), and tasks spread across ToDo / InProgress / InReview / Done
- For the V-Model project: requirements across phases with some traceability links populated
- A few sample Issues, Task Comments, and Notifications so those screens are not empty on first view
- Demo passwords must be simple but must still go through normal Identity password hashing — never store or seed plaintext passwords, even locally

Never seed this demo data outside the Development environment check.

## 92.7 README Requirements

The README must allow a developer (including the original author, months later) to go from a clean clone to a running, demo-ready application. It must document:

- Required SDK/runtime version and SQL Server edition (LocalDB vs Express)
- Exact steps to restore dependencies, apply migrations, and run the project
- Where and how to set the Gemini API key and connection string via User Secrets
- Which local email option (92.3) is configured and how to view captured emails
- Seeded demo account credentials for each role
- Any one-time setup step (e.g. trusting the dev HTTPS certificate)

## 92.8 What This Section Does *Not* Cover

Cloud/production deployment (hosting provider, CI/CD pipelines, Key Vault, horizontal scaling, production email providers) is intentionally out of scope while Planora remains local-only. If the project later needs to be deployed or demoed remotely, Sections 85 (High Availability) and 88 (Maintainability) already define the target behavior — only the local substitutes in this section (92.3, 92.5) would need to be swapped for their production equivalents, without changing Domain/Application code.
