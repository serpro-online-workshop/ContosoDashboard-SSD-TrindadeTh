# Research: Document Upload and Management

## 1. Local Malware Scanning for Offline Uploads

**Decision**: Use an `IFileScanner` abstraction backed by a locally installed ClamAV daemon (`clamd`) and a pre-provisioned signature database. Scan each quarantined staging file before promoting it to the permanent upload directory. Treat missing scanner, unavailable daemon, stale/unreadable configuration, timeout, and indeterminate results as scan failures and reject the upload (fail closed). Keep the daemon on a local Unix socket or otherwise inaccessible to untrusted networks.

**Rationale**: ClamAV supports offline scanning when its signature database is present locally. Its daemon supports repeated scans without rebuilding the scanning engine for each file; its command-line client requires a running daemon for daemon-backed scans. The current app has no cloud dependency, and a local scanning engine fits the offline requirement. The security requirement prohibits accepting an upload that could not be scanned.

**Alternatives considered**:
- Cloud malware-scanning API: rejected because the training application must work offline and add no cloud dependency.
- One `clamscan` process per file: rejected as the default because it initializes the engine/database for each run; acceptable only as a diagnostic fallback, not as the normal 25 MB upload path.
- No-op scanner or test-only acceptance: rejected because it would violate the MUST-scan requirement. Tests should inject a deterministic fake scanner; runtime must use the real configured scanner.

**Operational caveat**: Virus definitions must be installed before the training environment goes offline. ClamAV is a malware detection engine, not a complete endpoint protection suite. Record engine/database version and scan result in diagnostic/audit information without exposing host paths to users.

## 2. Blazor Server Upload Handling

**Decision**: Use the existing Blazor Server `InputFile` interaction and process selected files sequentially. Enforce a 25 MB `OpenReadStream` limit, copy one file at a time into the constitution-required `MemoryStream`, clear the `IBrowserFile` reference after the copy, and release the buffer before moving to the next file. Calculate progress during the bounded copy and display a per-file result.

**Rationale**: This matches the existing .NET 8 Blazor Server architecture and the ratified constitution's explicit upload-state requirements. Sequential processing bounds peak application memory to one permitted file buffer rather than multiplying it by the selected batch size.

**Alternatives considered**:
- Read all selected files into memory concurrently: rejected because it multiplies memory use and can cause server resource exhaustion.
- Stream directly from `IBrowserFile` to permanent storage: Microsoft recommends streaming for larger files, but the current constitution specifically mandates a `MemoryStream` copy. The bounded sequential approach obeys governance while preserving the explicit 25 MB cap.
- Send uploads through a separate API project: rejected because the application is a single server-interactive Blazor app and does not need another service boundary.

**Follow-up risk**: Measure memory and upload latency during acceptance testing. If one-file buffering does not meet the 30-second/performance target under expected concurrency, propose a constitution amendment to permit bounded temporary-file streaming.

## 3. SQLite Schema Evolution

**Decision**: Add EF Core migrations for document entities and indexes, and apply them to the existing SQLite database. Replace feature-path dependence on `EnsureCreated` with migration-based initialization in implementation work.

**Rationale**: The app currently calls `EnsureCreated` and has no existing document tables. `EnsureCreated` does not incrementally evolve a database that already contains tables, so adding DbSets alone will leave existing training databases without the new schema. A migration keeps existing users, projects, tasks, and notifications intact.

**Alternatives considered**:
- Drop and recreate the database: rejected as the normal rollout because it destroys existing dashboard records. A clean database remains useful only for isolated tests or explicit local recovery from corrupted prior test data.
- Continue using `EnsureCreated`: rejected because it is not a schema migration mechanism for populated databases.

## 4. Search and Performance at Stated Scale

**Decision**: Query SQLite on the server with authorization predicates applied before results are returned; search title, description, tags, uploader, and project metadata. Add indexes for common filters and ordering, and verify with a seeded 500-document test dataset.

**Rationale**: The specification bounds the list-page target at 500 documents. A separate search platform or full-text service would violate simplicity/offline goals and is not justified at this scale. Authorization must be part of the query, not a UI-only filter.

**Alternatives considered**:
- External search service: rejected because it adds a cloud/runtime dependency and requires data synchronization.
- Search all rows in the UI: rejected because inaccessible results could leak and query time grows with the dataset.

## 5. Testing Strategy

**Decision**: Add an xUnit test project targeting .NET 8. Use isolated SQLite databases for EF behavior, temporary filesystem roots for storage tests, and a fake `IFileScanner` for deterministic clean/infected/unavailable verdict tests. Add focused service tests for role, project membership, department shares, revocation, and permanent deletion; use a 500-row dataset for list/search timing checks.

**Rationale**: Repository inspection found no existing test project. The feature's most consequential risks are security authorization and file/metadata consistency, both of which need repeatable service-level tests independent of browser interaction.

**Alternatives considered**:
- Unit tests only with EF in-memory provider: rejected for database behaviors because SQLite-specific indexes, constraints, and query translation should be exercised.
- Browser-only manual testing: rejected as the only gate because it does not reliably cover unauthorized access, rollback, scanner failure, or boundary cases.

## Sources

- Microsoft Learn, [ASP.NET Core Blazor file uploads](https://learn.microsoft.com/en-us/aspnet/core/blazor/file-uploads?view=aspnetcore-8.0): explicit stream size limits, file-name trust boundaries, guidance to avoid buffering large files, server-side progress examples, and scanning expectations.
- ClamAV Documentation, [Scanning](https://docs.clamav.net/manual/Usage/Scanning.html): daemon scanning, local signature database requirement, `clamdscan` dependency on a running daemon, and local socket/network cautions.
- Repository evidence: `ContosoDashboard/ContosoDashboard.csproj` targets `net8.0` with EF Core SQLite 8.0.0; `Program.cs` registers Blazor Server, SQLite, cookie authentication, and `EnsureCreated`; `Data/ApplicationDbContext.cs` defines existing DbSets and relationships; `Pages/Login.cshtml.cs` emits NameIdentifier, Name, Email, and Role but not Department; no test project was found.
