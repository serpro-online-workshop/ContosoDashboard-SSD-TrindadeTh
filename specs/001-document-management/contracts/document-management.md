# Document Management Contracts

This feature adds authenticated UI and server-side file operations to the existing Blazor Server application. It does not introduce a public API or a separate service.

## User Interface Routes

| Route | Contract |
|---|---|
| `/documents` | Authenticated My Documents page. Lists only documents the current user may access; supports upload, metadata, sort, filters, search, preview/download, and permitted owner/manager actions. |
| `/documents/shared` | Authenticated Shared with Me page. Lists documents with an active direct or department share for the current user, subject to current authorization. |
| `/projects/{projectId}/documents` | Project document view. Requires current project membership, manager access, administrator access, or another explicit grant. |
| Existing task detail surface | Lists documents associated with the task and offers attach/upload. Any task document must use the task's ProjectId. The current `Tasks.razor` has no task-detail route, so implementation must extend its existing detail-modal/navigation flow or add a dedicated task route without changing task identity semantics. |

The upload UI accepts one or more selected files, processes files sequentially, shows per-file progress/result, requires title/category, and permits optional description, project, and tags. It must not expose `FilePath` or a physical host path.

## Protected File Routes

| Method and route | Authentication / authorization | Result |
|---|---|---|
| `GET /documents/{documentId:int}/download` | Authenticated; service must authorize the current user for this specific document. | Streams the original file as attachment with server-validated content type and safe display filename. |
| `GET /documents/{documentId:int}/preview` | Authenticated; same per-document authorization check. | Streams only supported PDF/image content with inline disposition and `X-Content-Type-Options: nosniff`. |

A guessed or unauthorized ID returns a non-disclosing not-found/forbidden response; physical paths and storage keys are never accepted from the caller. The endpoints recheck authorization on every request and never rely on whether the UI rendered a link.

## Service Boundaries

### `IFileStorageService`

- `UploadAsync(Stream fileStream, string fileName, string contentType) -> Task<string>`: persist accepted bytes to the configured local root using a server-generated relative path and return that key.
- `DeleteAsync(string filePath) -> Task`: delete only a validated key beneath the configured root; idempotent for already-missing files.
- `DownloadAsync(string filePath) -> Task<Stream>`: open the validated storage key after business authorization.
- `GetUrlAsync(string filePath, TimeSpan expiration) -> Task<string>`: return an application-controlled authorized URL; local storage must not return a direct filesystem or static-file URL. Expiration is enforced by the endpoint if a temporary route token is used; do not use an unprotected URL as an authorization substitute.

### `IDocumentService`

All methods receive the authenticated requester identity (or a trusted claims principal resolved to the existing integer UserId) and enforce the permission model before returning content or mutating state. The interface covers:

- Upload one file with metadata and optional ProjectId/TaskId, returning a per-file result.
- List/search/filter/sort documents visible to the requester.
- Get document metadata, preview/download stream, and dashboard recent/count data.
- Edit metadata, replace file, and permanently delete after authorization.
- Create and revoke direct-user or department shares; list active Shared with Me results.
- Write audit entries and issue existing in-app notifications after successful database commits.

## Authorization Rules

- Employees: own documents and documents for projects they currently belong to; uploader status by itself does not survive project removal as an access grant.
- Team Leads: own documents and their team members' documents, according to existing role/department data.
- Project Managers: documents associated with projects they manage.
- Administrators: all documents.
- A direct share grants only its recipient; a department share targets users currently assigned to the selected department. Share revocation removes only that share grant.
- Every query and action applies authorization in `DocumentService`; the presentation layer and route attributes are additional boundaries, not substitutes.

## Failure Semantics

- Invalid metadata/type/size: reject before storage or database mutation with a user-safe validation result.
- Infected, unavailable, timed-out, or indeterminate scan: fail closed; discard staging bytes; create no document metadata.
- Storage failure: create no metadata; remove partial files where possible.
- Database failure after file promotion: delete the promoted file; do not send success notifications.
- Permanent deletion: require confirmation in UI; delete file and live record, retain the audit history.
