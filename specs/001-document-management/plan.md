# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-10-20 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `specs/001-document-management/spec.md`

## Summary

Add secure document upload, browsing, search, project/task association, controlled sharing, and audit
to the existing ContosoDashboard. Keep file bytes on local disk outside `wwwroot`, metadata in the
existing SQLite database, and route all file operations through `IFileStorageService`. A
`DocumentService` will own validation and authorization, use a local ClamAV scanner that fails
closed, and only persist metadata after a clean scan and successful file promotion. Add EF Core
migrations for the new schema, preserve existing app conventions, and test the service/UI contracts
in a new .NET test project.

## Technical Context

**Language/Version**: C# / .NET 8 (`net8.0`)  
**Primary Dependencies**: ASP.NET Core Blazor Server; EF Core SQLite 8.0.0; local ClamAV engine and signature database as an offline host prerequisite  
**Storage**: SQLite for metadata and local filesystem for document bytes, rooted outside `wwwroot`  
**Testing**: No test project exists; add an xUnit test project using EF Core SQLite test databases and service-level tests  
**Target Platform**: .NET 8 web server in the offline training environment; local scanner executable/service and signatures must be provisioned on the host  
**Project Type**: Single-project Blazor Server web application  
**Performance Goals**: Upload up to 25 MB in ≤30 seconds; lists and search ≤2 seconds for up to 500 documents; PDF/image preview ≤3 seconds  
**Constraints**: No cloud services or Azure SDK; files outside web root; authorization on every document operation; integer document keys; text categories; offline malware scanning; support mock cookie authentication  
**Scale/Scope**: Up to 500 documents per list scenario; multiple selected files processed sequentially, each limited to 25 MB; 8 user stories, 34 functional requirements

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle / Gate | Plan assessment |
|---|---|
| I. Security-First by Default | **PASS**: deny-by-default service authorization; GUID storage keys; extension allowlist; malware scan before permanent storage; authenticated download/preview endpoints; scanner outages fail closed. |
| II. Storage Abstraction for Cloud Portability | **PASS**: `DocumentService` uses `IFileStorageService`; only `LocalFileStorageService` is registered; no cloud SDK or storage-specific paths in UI/business logic. |
| III. Offline-First, Local-Storage Operation | **PASS**: all core flows use SQLite, local disk, and a locally provisioned scanner/signature database; no runtime network dependency. |
| IV. Layered Architecture & Separation of Concerns | **PASS**: UI → `IDocumentService` → EF/storage/scanner/notification abstractions; authorization stays in the service. Keep the required per-file bounded `MemoryStream` copy, then clear the `IBrowserFile` reference. |
| V. Data Integrity & Convention Consistency | **PASS**: integer `DocumentId`, string `Category`, MIME length ≥255, portable relative file paths, GUID names generated before any DB insert. |
| Security & Data Integrity Standards | **PASS WITH REMEDIATION**: add the missing Department claim in login; resolve team membership from current persisted department data so stale claims cannot preserve access. Audit uploads, downloads, deletes, shares, and revocations. |
| Development Workflow & Quality Gates | **PASS**: stay in the existing project architecture; use non-destructive EF migrations; test against a clean SQLite database without dropping user data. |

The constitution mandates a `MemoryStream` copy, while current Microsoft Blazor guidance recommends
streaming larger files directly to disk to reduce server memory pressure. To comply with the
ratified rule, process one file at a time, enforce the 25 MB limit while opening/reading the stream,
and release the buffer before the next selected file. Revisit this only through a constitution
amendment if measured load makes the bounded approach unacceptable.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/document-management.md
└── tasks.md                 # Generated in the tasks phase
```

### Source Code (repository root)

```text
ContosoDashboard/
├── Data/
│   └── ApplicationDbContext.cs
├── Models/
│   ├── Document.cs
│   ├── DocumentShare.cs
│   ├── DocumentTag.cs
│   └── DocumentActivity.cs
├── Pages/
│   ├── Documents.razor
│   ├── SharedDocuments.razor
│   ├── ProjectDetails.razor
│   └── Tasks.razor
├── Services/
│   ├── DocumentService.cs
│   ├── FileStorageService.cs
│   ├── FileScannerService.cs
│   └── DocumentAccessPolicy.cs
├── Endpoints/
│   └── DocumentEndpoints.cs
├── Properties/launchSettings.json
└── Migrations/

ContosoDashboard.Tests/
├── Services/
└── Data/
```

**Structure Decision**: Extend the existing single Blazor Server project and add one test project.
Keep document entities in the current `Models` namespace, services in `Services`, EF schema
configuration in `ApplicationDbContext`, and authenticated stream endpoints in a small endpoint
registration module. Existing task/project pages are integration surfaces; no second frontend or
API project is needed.

## Design Decisions

- `DocumentService` is the sole business entry point for upload, metadata, query, sharing, and
  deletion; it evaluates access using the requesting user ID and current database state.
- Uploads are validated, authorized, copied into a bounded `MemoryStream`, scanned via a local
  ClamAV daemon using a quarantined staging file, promoted to a GUID-based relative path, then
  committed to SQLite. On any failure, remove staging/final files and do not leave metadata.
- Use a configured local `AppData/uploads` root outside `wwwroot`; deny execution where supported,
  use least-privilege filesystem permissions, and never expose physical paths to the UI.
- Add EF Core migrations rather than relying on `EnsureCreated`, which does not evolve an existing
  schema when document entities are added.
- Keep search database-side and authorization-filtered; the stated scale of 500 documents does not
  justify a separate search engine. Index uploader, project, upload time, category, and share lookup
  fields, then verify the 2-second acceptance target.
- Use user shares and department shares as distinct grant forms. A department share resolves against
  current `User.Department` values; revocation removes only that share grant, not other permission
  paths.
- Record audit events separately from live document rows so permanent document deletion does not
  erase activity history or reporting evidence.

## Post-Design Constitution Check

| Principle / Gate | Post-design assessment |
|---|---|
| I. Security-First by Default | **PASS**: the contracts require per-request authorization, GUID paths, private storage, allowlist validation, and fail-closed scanning. |
| II. Storage Abstraction for Cloud Portability | **PASS**: file persistence is behind `IFileStorageService`; no business or UI contract relies on the local implementation. |
| III. Offline-First, Local-Storage Operation | **PASS**: local SQLite, disk, and pre-provisioned ClamAV signatures support offline runtime. |
| IV. Layered Architecture & Separation of Concerns | **PASS**: UI, document business service, persistence, storage, and scanner responsibilities are separated; bounded `MemoryStream` copy is retained per constitution. |
| V. Data Integrity & Convention Consistency | **PASS**: integer keys, string categories, 255-character MIME capacity, relative file paths, and pre-insert GUID generation are specified. |
| Security & Data Integrity Standards | **PASS WITH REMEDIATION**: implementation must add the Department claim, use current database department membership for authorization, and audit document operations. |
| Development Workflow & Quality Gates | **PASS**: existing schema is evolved through migrations; failure cleanup and isolated tests are in scope; no excluded feature was added. |

No gate violation requires a complexity exception. Implementation must complete the Department-claim
remediation before team-sharing authorization is considered complete.

## Complexity Tracking

No constitution violations or additional project boundaries are proposed. The test project and
service abstractions are required for independently verifying authorization, storage, and scanner
failure behavior.
