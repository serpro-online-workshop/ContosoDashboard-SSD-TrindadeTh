# Tasks: Document Upload and Management

**Input**: Design documents from `specs/001-document-management/`  
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/document-management.md`, `quickstart.md`

**Tests**: Included because the implementation plan and research explicitly require an xUnit test project and service-level verification. Story test tasks precede implementation tasks.

**Organization**: Tasks are grouped by user story. `[P]` marks tasks that can be performed in parallel on separate files with no incomplete-task dependency. Every task includes its target file path.

## Phase 1: Setup

**Purpose**: Create the test harness required by the plan.

- [X] T001 Create the .NET 8 xUnit test project with a reference to the application and test SDK packages in `ContosoDashboard.Tests/ContosoDashboard.Tests.csproj`

---

## Phase 2: Foundational

**Purpose**: Establish shared storage, scanning, and authentication prerequisites. All story work depends on this phase.

- [X] T002 [P] Define the storage abstraction and options for upload, delete, download, and authorized URL operations in `ContosoDashboard/Services/IFileStorageService.cs`
- [X] T003 [P] Define the scanner abstraction, verdict type, and fail-closed result contract in `ContosoDashboard/Services/IFileScanner.cs`
- [X] T004 [P] Add the required Department claim from the persisted user profile to the mock login principal in `ContosoDashboard/Pages/Login.cshtml.cs`
- [X] T005 Implement root-contained relative-path validation and local filesystem upload, delete, and download operations in `ContosoDashboard/Services/LocalFileStorageService.cs` (depends on T002)
- [X] T006 Implement the local ClamAV scanner adapter with timeout and unavailable/indeterminate outcomes treated as failures in `ContosoDashboard/Services/ClamAvFileScanner.cs` (depends on T003)
- [X] T007 Register storage/scanner services and configure the upload root, scanner socket, and scan timeout without cloud dependencies in `ContosoDashboard/Program.cs` and `ContosoDashboard/appsettings.json` (depends on T005, T006)

**Checkpoint**: Application can resolve local storage and malware-scanning services; login includes the Department claim.

---

## Phase 3: User Story 1 - Upload a document with metadata (Priority: P1) 🎯 MVP

**Goal**: Accept supported files and required metadata, validate and scan each file, then securely store bytes and metadata.

**Independent Test**: Sign in, upload a supported file under 25 MB with title/category, and verify success plus persisted metadata and a GUID-named file outside `wwwroot`. Verify oversized, unsupported, infected, and scanner-unavailable cases leave no document record or accepted file.

### Tests for User Story 1

- [X] T008 [US1] Add upload service tests for required metadata, supported extensions, 25 MB limit, scan verdicts, unique paths, project authorization, and storage/database compensation in `ContosoDashboard.Tests/Services/DocumentUploadTests.cs`

### Implementation for User Story 1

- [X] T009 [US1] Add the integer-keyed Document and DocumentTag entities with required lengths, string Category, MIME capacity, and optional project/task references in `ContosoDashboard/Models/Document.cs` and `ContosoDashboard/Models/DocumentTag.cs`
- [X] T010 [US1] Configure document/tag relationships and indexes, create `ContosoDashboard/Migrations/20261020191215_AddDocuments.cs` without dropping existing SQLite data, and replace `EnsureCreated` with migration application in `ContosoDashboard/Data/ApplicationDbContext.cs` and `ContosoDashboard/Program.cs`
- [X] T011 [US1] Implement authorized upload orchestration, metadata/type/size validation, GUID path generation, scan-before-promotion ordering, failure cleanup, and post-commit project-member notifications in `ContosoDashboard/Services/DocumentService.cs`
- [X] T012 [US1] Add the `/documents` upload form with required title/category, optional description/project/tags, sequential multi-file progress/results, keyed InputFile reset, bounded MemoryStream copying, and cleared IBrowserFile references in `ContosoDashboard/Pages/Documents.razor`; add its navigation link in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: Upload works independently and all rejected/failing uploads leave no document record or accepted file.

---

## Phase 4: User Story 2 - Browse, organize, and search my documents (Priority: P2)

**Goal**: Let users list, sort, filter, and search only documents they are authorized to access.

**Independent Test**: Seed or upload matching and non-matching documents, then verify displayed metadata, all supported sort/filter/search dimensions, access filtering, and the 2-second target at 500 documents.

### Tests for User Story 2

- [ ] T013 [US2] Add query tests for title/description/tag/uploader/project search, category/project/date filters, sorting, and exclusion of inaccessible rows in `ContosoDashboard.Tests/Services/DocumentQueryTests.cs`

### Implementation for User Story 2

- [ ] T014 [US2] Implement server-side authorized list/search queries, whitelisted sort fields, category/project/date filters, and recent/count query methods in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T015 [US2] Extend `/documents` with a responsive metadata table, sorting, filters, search, empty/loading states, and pagination or bounded result display in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: Each list/search operation applies authorization in the service before returning results.

---

## Phase 5: User Story 3 - Download and preview documents (Priority: P2)

**Goal**: Let authorized users retrieve intact files and preview PDFs/images through protected routes.

**Independent Test**: Download an authorized file and compare bytes; preview an authorized PDF/image; verify unauthorized or guessed IDs cannot retrieve content and unsupported preview types offer download instead.

### Tests for User Story 3

- [ ] T016 [US3] Add endpoint/service tests for authorized and denied download/preview, content disposition/type, intact bytes, and unsupported preview types in `ContosoDashboard.Tests/Endpoints/DocumentEndpointTests.cs`

### Implementation for User Story 3

- [ ] T017 [US3] Add authenticated download and preview endpoints that resolve document IDs through `IDocumentService`, set safe headers, and never accept filesystem paths from callers in `ContosoDashboard/Endpoints/DocumentEndpoints.cs`; register the endpoints in `ContosoDashboard/Program.cs`
- [ ] T018 [US3] Add download and PDF/image preview actions to the authorized document rows, with download fallback for other supported types, in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: Every file response repeats the service authorization check and reveals no physical path.

---

## Phase 6: User Story 4 - Project documents and team access (Priority: P2)

**Goal**: Show project files to current project members and managers, while revoking project-derived access after removal.

**Independent Test**: Associate a document to a project; confirm members/managers can access it, outsiders cannot, and after member removal the file remains with the project but the former member is denied absent another permission path.

### Tests for User Story 4

- [ ] T019 [US4] Add authorization tests for employee membership, team-lead team access, project manager ownership, administrator access, non-member denial, and former-member access revocation in `ContosoDashboard.Tests/Services/DocumentAccessPolicyTests.cs`

### Implementation for User Story 4

- [ ] T020 [US4] Implement current-database employee membership, team-lead team access, manager/admin, and explicit-grant checks in a reusable document access policy and apply it to project list/upload/download queries in `ContosoDashboard/Services/DocumentAccessPolicy.cs` and `ContosoDashboard/Services/DocumentService.cs`
- [ ] T021 [US4] Add the project document list and manager upload entry point to the project details view, preserving project association and permission checks in `ContosoDashboard/Pages/ProjectDetails.razor`

**Checkpoint**: Project documents remain attached to projects; former membership or uploader status alone never grants access.

---

## Phase 7: User Story 5 - Manage document metadata, replacement, and deletion (Priority: P3)

**Goal**: Allow permitted owners/managers to edit metadata, replace file bytes, and permanently delete documents safely.

**Independent Test**: Edit metadata, replace a file, and confirm permanent deletion; verify unauthorized users are denied and file/metadata state is consistent after injected failures.

### Tests for User Story 5

- [ ] T022 [US5] Add lifecycle tests for metadata edits, replacement validation/rollback, uploader and manager permissions, confirmed permanent deletion, and storage cleanup in `ContosoDashboard.Tests/Services/DocumentLifecycleTests.cs`

### Implementation for User Story 5

- [ ] T023 [US5] Implement authorized metadata updates, scanned file replacement with atomic promotion/cleanup, and permanent deletion of file, tags, and active shares in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T024 [US5] Add metadata edit, file replace, and confirmation-before-delete controls with role-aware visibility and server-result feedback in `ContosoDashboard/Pages/Documents.razor`

**Checkpoint**: Owner/manager mutations work; unauthorized uploader-only access after project removal remains denied.

---

## Phase 8: User Story 6 - Share documents with people and teams (Priority: P3)

**Goal**: Grant and revoke direct-user or department-based access, notify recipients, and provide Shared with Me.

**Independent Test**: Share with a user and department, verify eligible recipients and in-app notifications, then revoke and confirm only the revoked grant disappears while independent access remains.

### Tests for User Story 6

- [ ] T025 [US6] Add tests for direct/department grants, current department membership, duplicate grants, notifications, owner-only revocation, and independent permission preservation in `ContosoDashboard.Tests/Services/DocumentSharingTests.cs`

### Implementation for User Story 6

- [ ] T026 [US6] Add DocumentShare with direct-user or department recipient constraints, retained revocation state, relationships, indexes, and `ContosoDashboard/Migrations/20261020_AddDocumentShares.cs` in `ContosoDashboard/Models/DocumentShare.cs` and `ContosoDashboard/Data/ApplicationDbContext.cs`
- [ ] T027 [US6] Implement current department-based recipient resolution, active-share authorization, share/revoke operations, and in-app notifications after successful commits in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Models/Notification.cs`
- [ ] T028 [US6] Add the Shared with Me route and document-owner share/revoke controls for user or department recipients in `ContosoDashboard/Pages/SharedDocuments.razor` and `ContosoDashboard/Pages/Documents.razor`; add navigation in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: Share revocation removes only the revoked grant; department eligibility uses current persisted department data.

---

## Phase 9: User Story 7 - Task and dashboard integration (Priority: P3)

**Goal**: Attach and upload documents from tasks, and surface recent documents and counts on the dashboard.

**Independent Test**: Upload/attach through a task and confirm the document inherits its project; confirm the home dashboard shows the current user's five most recent documents and count.

### Tests for User Story 7

- [ ] T029 [US7] Add tests for task/project association, task access enforcement, recent-five ordering, and per-user document counts in `ContosoDashboard.Tests/Services/DocumentIntegrationTests.cs`

### Implementation for User Story 7

- [ ] T030 [P] [US7] Add task document listing, attach, and direct-upload controls to the existing task detail flow, deriving ProjectId only from the task in `ContosoDashboard/Pages/Tasks.razor`
- [ ] T031 [P] [US7] Add the Recent Documents widget and document count summary to the dashboard using requester-scoped `IDocumentService` queries in `ContosoDashboard/Pages/Index.razor`

**Checkpoint**: Task association and dashboard content are requester-scoped and do not bypass document authorization.

---

## Phase 10: User Story 8 - Activity audit and reporting (Priority: P4)

**Goal**: Preserve document activity history and provide administrator reports for file types, uploaders, and access patterns.

**Independent Test**: Perform upload, download, preview, edit, replacement, delete, share, and revoke actions; verify each is logged and an administrator can generate all three reports, including after document deletion.

### Tests for User Story 8

- [ ] T032 [US8] Add audit/report tests for all document actions, actor/time capture, post-deletion history, report aggregates, and administrator-only access in `ContosoDashboard.Tests/Services/DocumentAuditTests.cs`

### Implementation for User Story 8

- [ ] T033 [US8] Add append-only DocumentActivity with nullable document reference, action/time/actor and reporting snapshots, indexes, relationships, and `ContosoDashboard/Migrations/20261020_AddDocumentActivity.cs` in `ContosoDashboard/Models/DocumentActivity.cs` and `ContosoDashboard/Data/ApplicationDbContext.cs`
- [ ] T034 [US8] Record upload, download, preview, edit, replacement, deletion, share, and revocation events only at the corresponding successful operation boundary in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Endpoints/DocumentEndpoints.cs`
- [ ] T035 [US8] Implement administrator-authorized report queries for most uploaded file types, active uploaders, and access patterns in `ContosoDashboard/Services/DocumentService.cs`
- [ ] T036 [US8] Add the administrator-only document activity reports page with date filters and the three required report views in `ContosoDashboard/Pages/DocumentReports.razor`; register its navigation in `ContosoDashboard/Shared/NavMenu.razor`

**Checkpoint**: All defined events survive permanent deletion and report access is restricted to administrators.

---

## Final Phase: Polish and Cross-Cutting Concerns

**Purpose**: Complete security, performance, and operational validation across the feature.

- [ ] T037 [P] Add an end-to-end authorization matrix covering every service operation and both protected file routes in `ContosoDashboard.Tests/Services/DocumentAuthorizationMatrixTests.cs`
- [ ] T038 Run the full automated suite and each quickstart acceptance flow, including scanner outage and 500-document performance checks, and record results against `specs/001-document-management/quickstart.md`
- [ ] T039 Verify persisted files stay outside web root, all file keys are relative/GUID-based, migrations preserve existing records, and no cloud package was added in `ContosoDashboard/Program.cs` and `ContosoDashboard/Migrations/20261020191215_AddDocuments.cs`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: T001 creates the test project and is the prerequisite for test tasks.
- **Foundational (Phase 2)**: Depends on T001; establishes storage/scanner contracts, local adapters, DI, and Department claim. Blocks all stories.
- **User Stories (Phases 3-10)**: Each depends on the foundation. Stories are prioritized and should be implemented in order unless the listed dependency graph permits parallel work.
- **Polish (Final Phase)**: Depends on all desired stories being complete.

### User Story Dependencies

- **US1 (P1)**: Starts after Phase 2; creates the document/tag schema and upload flow. MVP.
- **US2 (P2)**: Depends on US1's persistent document schema and upload service.
- **US3 (P2)**: Depends on US1's stored documents and service authorization.
- **US4 (P2)**: Depends on US1's project association and upload service.
- **US5 (P3)**: Depends on US1 and US4 authorization rules for owner/manager actions.
- **US6 (P3)**: Depends on US1 and the foundational Department claim; adds share-specific data and notifications.
- **US7 (P3)**: Depends on US1 and US4 task/project authorization and association.
- **US8 (P4)**: Depends on the operations introduced by US1, US3, US5, and US6 so it can audit their successful outcomes.

### Within Each User Story

- Tests are written before implementation and should fail for the missing behavior.
- Models/schema precede service/query behavior; service authorization precedes UI wiring.
- Storage, scanner, and database failures must be tested before marking upload complete.
- A story is complete only when its independent test criteria pass.

### Parallel Opportunities

- T002, T003, and T004 are independent files and can be prepared in parallel after T001.
- After the shared foundation and US1 schema, US2, US3, and US4 can be developed in parallel by separate workers, but US5 waits for US4.
- US6 can proceed independently after US1 and the foundation; US7 waits for task/project authorization from US4.
- Within US7, T030 and T031 touch separate pages and can be implemented in parallel after T029.

---

## Parallel Example: Foundational Contracts

```text
Task T002: Define IFileStorageService in ContosoDashboard/Services/IFileStorageService.cs
Task T003: Define IFileScanner in ContosoDashboard/Services/IFileScanner.cs
Task T004: Add Department claim in ContosoDashboard/Pages/Login.cshtml.cs
```

## Parallel Example: User Story 7

```text
Task T030: Integrate task documents in ContosoDashboard/Pages/Tasks.razor
Task T031: Add dashboard widget in ContosoDashboard/Pages/Index.razor
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1 and Phase 2.
2. Complete Phase 3 (US1 upload) and verify its independent test criteria.
3. Demo the secure upload, validation, malware rejection, and metadata persistence before expanding scope.

### Incremental Delivery

1. Add US2 browsing/search, then US3 retrieval, then US4 project access; independently validate each story.
2. Add US5 lifecycle management and US6 sharing/revocation.
3. Add US7 task/dashboard integration and US8 audit/reporting.
4. Run the final security and performance checks after all selected stories are complete.

### Parallel Team Strategy

Complete Setup and Foundational work first. After US1 establishes the shared document schema and upload service, assign US2, US3, and US4 to separate contributors. US6 may run independently after US1; US5 and US7 wait for US4; US8 integrates after the audited operations exist.
