<!--
Sync Impact Report
Version change: (unversioned template) → 1.0.0
Rationale: Initial ratification of the ContosoDashboard constitution. All placeholder
tokens replaced with concrete governance derived from the Document Upload and Management
Feature requirements.

Modified principles:
- [PRINCIPLE_1_NAME] → I. Security-First by Default
- [PRINCIPLE_2_NAME] → II. Storage Abstraction for Cloud Portability
- [PRINCIPLE_3_NAME] → III. Offline-First, Local-Storage Operation
- [PRINCIPLE_4_NAME] → IV. Layered Architecture & Separation of Concerns
- [PRINCIPLE_5_NAME] → V. Data Integrity & Convention Consistency

Added sections:
- Security & Data Integrity Standards (Section 2)
- Development Workflow & Quality Gates (Section 3)

Removed sections: None

Deferred items / TODOs: None

Follow-up: Templates under .specify consume this constitution at runtime; no template
source files were modified. This Sync Impact Report comment is scratch review material and
should be removed before committing the amended constitution.
-->

# ContosoDashboard Constitution
<!-- Governance for the ContosoDashboard application and the Document Upload and Management feature -->

## Core Principles

### I. Security-First by Default

Every document-handling path MUST enforce security before convenience. Non-negotiable rules:

- Uploaded files MUST be stored outside `wwwroot` and served only through authenticated,
  authorization-checked endpoints.
- File names on disk MUST be GUID-based; user-supplied file names MUST NEVER be used directly
  in file paths (path-traversal prevention).
- File extensions MUST be validated against an explicit whitelist, and files MUST be scanned for
  viruses and malware before storage.
- Every read, download, edit, delete, and share operation MUST perform an authorization check in
  the service layer to prevent Insecure Direct Object Reference (IDOR) access.
- Files exceeding the 25 MB limit or of unsupported types MUST be rejected with clear error
  messages.

Rationale: The feature centralizes sensitive corporate documents; a single missing authorization
or validation check can expose data or enable attacks. Security cannot be retrofitted.

### II. Storage Abstraction for Cloud Portability

All file persistence MUST go through the `IFileStorageService` abstraction. Non-negotiable rules:

- Business logic, controllers, pages, and the database schema MUST depend on `IFileStorageService`
  (`UploadAsync`, `DeleteAsync`, `DownloadAsync`, `GetUrlAsync`), never on a concrete storage
  implementation.
- The training implementation MUST be `LocalFileStorageService` using `System.IO`; a future
  `AzureBlobStorageService` MUST be swappable via dependency injection and configuration ONLY.
- The storage path pattern `{userId}/{projectId or "personal"}/{guid}.{extension}` MUST work
  unchanged for both local paths and Azure blob names.
- Migrating storage backends MUST require NO changes to business logic, UI, or database schema.

Rationale: Designing to an interface keeps the training build offline while guaranteeing a
zero-rewrite path to Azure Blob Storage in production.

### III. Offline-First, Local-Storage Operation

The feature MUST function fully without cloud services or internet connectivity. Non-negotiable
rules:

- The training implementation MUST use local filesystem storage and MUST NOT introduce Azure SDK
  or other cloud dependencies.
- File paths persisted in the database MUST be relative and portable.
- Core document workflows (upload, browse, search, download, preview) MUST work offline.

Rationale: The application targets a training environment that may run without network access;
offline capability is a hard constraint, not a preference.

### IV. Layered Architecture & Separation of Concerns

Work MUST respect the layered architecture: Data, Storage, Business Logic, and Presentation.
Non-negotiable rules:

- The `DocumentService` business layer MUST orchestrate the upload workflow and enforce all
  validation and authorization rules before any data access.
- The mandatory upload sequence is: generate unique GUID path → save file to disk → write
  database metadata. This order MUST be preserved to prevent orphaned records and duplicate-key
  errors.
- Presentation (Blazor Server) MUST NOT bypass the service layer to reach storage or data
  directly.
- Blazor file uploads MUST copy `IBrowserFile` streams into a `MemoryStream` and clear the file
  reference after copying to avoid disposal/reuse errors.

Rationale: Clear layer boundaries keep security enforcement centralized, the system maintainable,
and the design cloud-ready without major rewrites.

### V. Data Integrity & Convention Consistency

Data models MUST stay consistent with existing application conventions. Non-negotiable rules:

- `DocumentId` MUST be an integer key, consistent with existing User and Project keys (NOT a GUID).
- `Category` MUST be stored as text values (e.g., "Project Documents"), NOT as an integer enum.
- The `FileType` (MIME) field MUST accommodate at least 255 characters.
- `FilePath` MUST accommodate GUID-based filenames.
- Unique file paths MUST be generated before database insertion; empty or non-unique paths are
  prohibited.

Rationale: Matching established key and data conventions avoids integration defects, duplicate-key
violations, and orphaned records, keeping the schema stable across the local-to-Azure migration.

## Security & Data Integrity Standards

- Authentication claims issued at login MUST include NameIdentifier, Name, Email, Role, and
  Department; the Department claim is required for team-based authorization in document sharing.
- Access to documents (view, download, edit, delete, share) MUST be restricted to users with the
  appropriate role or project membership; search results MUST exclude documents the user cannot
  access.
- All document activities (uploads, downloads, deletions, shares) MUST be logged for audit and
  compliance reporting.
- Supported file types are limited to PDF, Microsoft Office documents, text files, and images
  (JPEG, PNG); maximum file size is 25 MB per file.
- Performance targets MUST be honored: upload ≤ 30 s for 25 MB files, list pages ≤ 2 s for up to
  500 documents, search ≤ 2 s, preview ≤ 3 s.

## Development Workflow & Quality Gates

- Features MUST be delivered within the current application architecture; major rewrites are
  prohibited.
- Changes MUST comply with the existing mock authentication system.
- Before first-time upload testing, the database MUST be in a clean state; orphaned records with
  empty `FilePath` values MUST be removed (drop/recreate) to prevent duplicate-key violations.
- Code review MUST verify compliance with every Core Principle, especially security,
  storage-abstraction, and the mandatory upload sequence.
- Out-of-scope items (real-time collaboration, version history, approval workflows, external
  integrations, mobile apps, document templates, storage quotas, soft delete) MUST NOT be
  introduced without an amendment to scope.

## Governance

This constitution supersedes other development practices for the ContosoDashboard document
management feature. All pull requests and reviews MUST verify compliance with its principles, and
any deviation MUST be justified and documented.

Amendments require: a written proposal describing the change and rationale, approval by the
project maintainers, and a version bump following semantic versioning:

- MAJOR: backward-incompatible governance or principle removals/redefinitions.
- MINOR: a new principle or section, or materially expanded guidance.
- PATCH: clarifications, wording, or non-semantic refinements.

Complexity that violates the Simplicity and offline-first intent MUST be justified in review or
rejected. This document is the authoritative source of project governance; feature specifications
and plans MUST align with it.

**Version**: 1.0.0 | **Ratified**: 2026-10-20 | **Last Amended**: 2026-10-20
