# Quickstart: Document Upload and Management

This guide defines the end-to-end validation path for the planned implementation. It assumes the app remains a .NET 8 Blazor Server project and uses local SQLite, local file storage, and a local malware scanner.

## Prerequisites

- .NET 8 SDK.
- A local ClamAV installation with `clamd` running and a signature database already provisioned. The scanner socket must be accessible only to the application host; the app must fail closed if the scanner is unavailable.
- No cloud account, internet connection, or Azure SDK is required at runtime.
- A writable application data directory outside `ContosoDashboard/wwwroot` for staging and accepted uploads.

## Setup

From the repository root:

```sh
dotnet restore ContosoDashboard/ContosoDashboard.csproj
dotnet ef database update --project ContosoDashboard/ContosoDashboard.csproj
```

If the EF command is unavailable, install/use the repository-approved `dotnet-ef` tool version matching EF Core 8 before continuing. Do not drop the existing training database for routine setup; migrations must preserve existing users, projects, tasks, and notifications.

## Automated Validation

Run the new test project after implementation:

```sh
dotnet test ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
```

Expected coverage includes size/type validation, scanner clean/infected/unavailable cases, GUID path generation, cleanup when persistence fails, authorization matrices, project removal behavior, direct and department shares/revocation, audit events, and document queries against 500 seeded records.

## Run the Application

```sh
dotnet run --project ContosoDashboard/ContosoDashboard.csproj
```

Open the HTTPS URL printed by the app. Sign in using the existing mock login users, then validate the following flows:

1. Upload a supported file below 25 MB with title/category and optional project/tags. Confirm progress and success, and confirm a GUID-named file exists under the configured data root but not under `wwwroot`.
2. Try a file above 25 MB and an unsupported extension. Confirm clear rejection and no document row or accepted file.
3. Upload a known harmless ClamAV test signature in an isolated test environment. Confirm the scanner rejects it, staging is removed, and no metadata is stored. Also stop the scanner and confirm the app fails closed.
4. Search, sort, and filter documents as users with different roles. Confirm inaccessible documents never appear in list or search results.
5. Download an authorized file and preview an authorized PDF/image. Confirm the bytes match the upload and unauthorized IDs cannot retrieve content.
6. Remove a project member after they uploaded a project document. Confirm the file remains attached to the project and the removed user loses membership/uploader-derived access, while independent role/share access still works.
7. Share with one user and with a department. Confirm recipients receive in-app notifications; revoke one share and confirm only that grant is removed.
8. Attach/upload a document from a task. Confirm its ProjectId matches the task's project.
9. Perform upload, download, preview, edit, delete, share, and revoke actions. Confirm activity reports retain relevant events after permanent file deletion.
10. Seed 500 documents and measure list/search, a 25 MB upload, and preview against the spec targets.

Expected results: all checks enforce server-side authorization, files remain private on disk, scanner failure never accepts a file, database/file state is compensated on errors, and every stated performance target passes in the documented training environment.

## References

- [Feature specification](spec.md)
- [Data model](data-model.md)
- [Service and UI contracts](contracts/document-management.md)
- [Implementation research](research.md)
