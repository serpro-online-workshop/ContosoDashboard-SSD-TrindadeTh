# Data Model: Document Upload and Management

This model extends the existing `User`, `Project`, and `TaskItem` records. Persistent document IDs are integers, categories are strings, and file content remains on local disk rather than in SQLite.

## Document

Represents one accepted, currently available file and its metadata.

| Field | Type / constraint | Description |
|---|---|---|
| DocumentId | Integer, primary key | Database identity; consistent with existing entities. |
| Title | Required string, max 255 | User-supplied display title. |
| Description | Optional string, max 2000 | Searchable explanation. |
| Category | Required string, max 100 | One of the six specified category labels; store text, not an enum ordinal. |
| OriginalFileName | Required string, max 255 | Sanitized display-only name; never used as a storage path. |
| FilePath | Required relative string, max 512 | Portable storage key using `{userId}/{projectId-or-personal}/{guid}.{extension}`. |
| FileType | Required string, max 255 | MIME type captured at upload; validate independently of browser-supplied value. |
| FileSizeBytes | Required integer | Actual bytes successfully read, bounded to 25 MiB per file. |
| UploadedAtUtc | Required UTC timestamp | Upload time. |
| UploaderUserId | Required integer, FK to User | Original uploader, retained for attribution; does not alone grant access after project removal. |
| ProjectId | Optional integer, FK to Project | Project association; remains after uploader membership removal. |
| TaskId | Optional integer, FK to TaskItem | Optional task association; if present, ProjectId must match the task's project. |

**Validation and invariants**:
- The stored extension comes from a server-side allowlist after validating the supplied file; never trust the original filename for path construction.
- Generate the GUID-based relative FilePath before any database insertion. Store no absolute host path in SQLite.
- Save the final file only after scan success; insert metadata only after successful file promotion. If the database write fails, remove the promoted file.
- Index UploaderUserId, ProjectId, UploadedAtUtc, Category, and searchable title fields where supported.

## DocumentTag

Represents a custom search tag associated with one document.

| Field | Type / constraint | Description |
|---|---|---|
| DocumentTagId | Integer, primary key | Tag row identity. |
| DocumentId | Integer, FK to Document | Parent document. |
| Tag | Required string, max 100 | Normalized user-provided tag value. |

A unique constraint on `(DocumentId, normalized Tag)` prevents duplicate tags on one document. Deleting a document cascades to its tags.

## DocumentShare

Represents one explicit-user grant or one department-team grant. Revocation is retained as state for audit, rather than deleting the grant row.

| Field | Type / constraint | Description |
|---|---|---|
| DocumentShareId | Integer, primary key | Share identity. |
| DocumentId | Integer, FK to Document | Shared document. |
| GrantedByUserId | Integer, FK to User | Owner who created the share. |
| RecipientUserId | Optional integer, FK to User | Set for a direct user share. |
| RecipientDepartment | Optional string, max 100 | Set for a department share; access is resolved against current user department. |
| CreatedAtUtc | Required UTC timestamp | Share creation time. |
| RevokedAtUtc | Optional UTC timestamp | Null while active; set by owner on revocation. |
| RevokedByUserId | Optional integer, FK to User | Actor who revoked the grant. |

Exactly one of RecipientUserId or RecipientDepartment is set. Authorization checks active shares plus independent project/role permissions. A revoked share removes only its own grant. A department share targets users currently assigned to that department; share notifications are sent to eligible users when the grant is created.

## DocumentActivity

Append-only audit entry for upload, download, preview, metadata edit, file replacement, deletion, share, and share revocation.

| Field | Type / constraint | Description |
|---|---|---|
| DocumentActivityId | Integer, primary key | Activity identity. |
| DocumentId | Optional integer | Nullable so permanent deletion does not erase audit history. |
| ActorUserId | Optional integer, FK to User | User who performed the action. |
| Action | Required string, max 50 | Stable action name. |
| OccurredAtUtc | Required UTC timestamp | Event time. |
| FileTypeSnapshot | Optional string, max 255 | Captures MIME type for historical upload reports after document deletion. |
| CategorySnapshot | Optional string, max 100 | Captures category where useful for audit/reporting. |
| TargetUserId | Optional integer, FK to User | Recipient for direct shares or a share-related event. |
| TargetDepartment | Optional string, max 100 | Department recipient for department shares. |

Index `(OccurredAtUtc, Action)`, `ActorUserId`, and `DocumentId`. Activity rows are retained when the associated document is permanently deleted.

## Existing Entities and Relationships

- `User` supplies integer identity, display name, role, and current Department.
- `Project` owns zero or more `Document` rows; project membership is currently represented by `ProjectMember`.
- `TaskItem` may own document associations; a task-associated document inherits the task's ProjectId.
- Deleting a project or user is not part of this feature. Configure restrictive foreign keys for retained attribution/audit relationships and explicitly preserve document rows when project membership changes.

## Main Lifecycle

1. Validate metadata, extension, and bounded byte count; authorize uploader/project membership.
2. Generate the final relative GUID path and copy one selected `IBrowserFile` into a bounded `MemoryStream` as required by the constitution.
3. Write a private staging copy and scan it with the configured local malware scanner.
4. On clean verdict, promote the staged bytes to the final local path, then insert Document and tag metadata in one database save.
5. On infected/unavailable scan, discard staging and create no Document row. On database failure, remove the promoted file and create no success notification.
6. On permanent deletion, remove file and live metadata/tags/shares; retain activity rows, including file type snapshots for reporting. Exact ordering and compensation for filesystem/database failures must be tested.
