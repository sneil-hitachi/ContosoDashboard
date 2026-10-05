# Research: Document Upload and Management

**Date**: 2026-10-05  
**Scope**: Resolve architecture, local scanning, persistence, authorization, and validation choices for the clarified feature specification.

## Decisions

### Application architecture

**Decision**: Extend the existing single Blazor Server application. Keep validation, authorization, document queries, notifications, and audit writes in `DocumentService`; use EF Core for metadata and a narrowly scoped authenticated controller for file content. Register infrastructure through the existing dependency-injection setup.

**Rationale**: The repository already separates `Pages`, `Services`, `Models`, and `Data`, targets .NET 10, and uses service-level checks in project/task services. The feature can fit without a second application or external API.

**Alternatives considered**: A separate document API/service would duplicate authentication and make local training setup more complex. Putting authorization and file operations directly in Razor components would duplicate business rules and leave the content boundary less controlled.

**Repository evidence**: `ContosoDashboard/Program.cs`, `ContosoDashboard/Data/ApplicationDbContext.cs`, `ContosoDashboard/Services/ProjectService.cs`, `ContosoDashboard/Services/NotificationService.cs`, and `ContosoDashboard/Pages/ProjectDetails.razor`.

### Local malware screening

**Decision**: Define `IMalwareScanner` and implement the training adapter with the official Windows ClamAV command-line scanner (`clamscan.exe`) against a private staged file. Keep the scanner executable and signature database local; do not call a cloud scanning API or update signatures on application startup. Treat a threat, scanner absence, stale/missing database, timeout, skipped/incomplete scan, process failure, or unrecognized result as not cleared and reject the file. Do not persist an available document or expose the file until a complete clean scan succeeds.

**Rationale**: The Windows ClamAV distribution is an official local installation option, and `clamscan` scans a named file without requiring the separately running daemon. It is usable without elevating the web application process. A local signature database permits offline runtime after setup. An interface allows deterministic fake scanners in tests and a future approved scanner replacement.

**Setup limitation**: ClamAV and a usable signature database must be installed/provisioned before upload is enabled. Signature acquisition/update may require network access during environment preparation; it is not a runtime or cloud-service dependency. In an air-gapped environment, provision the approved database through the local training setup process. If no usable database is present, uploads fail closed.

**Alternatives considered**: Microsoft Defender `MpCmdRun.exe` is installed on the inspected development machine, but Microsoft's command-line guidance requires an elevated command prompt and documents a return code that can cover both no detection and a successfully remediated detection; this complicates safe integration with an ordinary web process. Cloud scanners violate offline/local-only constraints. Writing a malware detector in application code is not credible.

**Validation requirement**: Prove process invocation with paths containing spaces, timeout/cancellation, clean EICAR test-file detection, scanner-not-installed behavior, unusable signatures, and incomplete scan output. Use `ProcessStartInfo.ArgumentList`, never shell-concatenate user-controlled values. Verify configured scan limits cannot silently treat unscanned content as clean.

**References**: [ClamAV installation](https://docs.clamav.net/manual/Installing.html), [ClamAV scanning](https://docs.clamav.net/manual/Usage/Scanning.html), [Microsoft Defender command-line arguments](https://learn.microsoft.com/en-us/defender-endpoint/command-line-arguments-microsoft-defender-antivirus).

### File storage and serving

**Decision**: Implement the required `IFileStorageService` with local filesystem operations rooted in a configurable application-data directory outside `wwwroot`. Store only relative, server-generated paths. Stage files in a private non-web-accessible area, scan them, then move a clean file into its final GUID-based path. Use an authenticated controller and a fresh service authorization check for every preview/download; never expose the filesystem path or rely on UI visibility as authorization.

**Rationale**: This matches the stakeholder storage pattern, limits path traversal/overwrite risks, works without cloud services, and preserves a substitution point for a separately approved Azure implementation. A staged file is not an available document until scan, final storage, and metadata persistence succeed.

**Consistency rule**: If metadata persistence fails after final file placement, delete the new file as compensation. If replacement fails at any stage, retain the prior accepted file. If deletion is confirmed, remove the file and metadata while retaining the audit event. Reconciliation of a process crash between filesystem and database operations is an implementation test/documentation item; do not expose untracked files.

**Alternatives considered**: `wwwroot` storage is public/static and bypasses the document authorization boundary. Storing file bytes in SQL Server couples large payloads to metadata and is outside the stated local-filesystem requirement. Azure Blob Storage is explicitly not a training prerequisite.

### Data and access model

**Decision**: Use integer `DocumentId`, text category values, and relational entities for documents, tags, task attachments, shares, and activity. Store the MIME type in a 255-character field. Use existing `User.Department` as the team scope; add a Department claim at mock login from the persisted user record. A task attachment inherits the task's project. Personal Files remain private even where a team/project relationship would otherwise allow access, unless the owner explicitly shares them.

**Rationale**: These decisions preserve the stakeholder's key constraints and map to existing `User`, `Project`, `ProjectMember`, `TaskItem`, and `Notification` entities. The login currently issues NameIdentifier, Name, Email, and Role claims but omits Department. Existing project membership and manager foreign keys support project scope.

**Alternatives considered**: GUID primary keys or enum-backed categories conflict with explicit database constraints. A delimiter-encoded tag/share field makes uniqueness, filtering, revocation, and relationship authorization difficult. A new team table is unnecessary for the first release because the existing user Department is the available team identifier.

### Database evolution

**Decision**: Move schema management from startup `EnsureCreated` to EF Core migrations. Add a baseline for the current schema and an additive document migration. For an existing database created by `EnsureCreated`, provide a schema-verification and baseline-adoption procedure before applying the additive migration; never drop the database automatically. Document an explicit, backup-first reset as a fallback only when a local database does not match the baseline.

**Rationale**: `EnsureCreated` initializes schema only when there are no tables and does not evolve an existing database. Microsoft warns that it does not work well with migrations and that transitioning is not seamless. The application currently calls it in `Program.cs` and contains no migration directory, so adding only entity classes would leave existing databases missing the new tables.

**Alternatives considered**: Keeping `EnsureCreated` would not upgrade current LocalDB databases. An unreviewed destructive reset would violate the explicit opt-in requirement and risk training data. A hand-maintained parallel schema initializer duplicates EF's schema definition.

**References**: [EF Core Create and Drop APIs](https://learn.microsoft.com/en-us/ef/core/managing-schemas/ensure-created), [Applying EF Core migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

### Tests and UI integration

**Decision**: Add a focused xUnit test project. Use a fake scanner and isolated temporary storage in deterministic unit/service tests; use isolated SQL Server LocalDB databases for migration, foreign-key, and authorization integration tests. Keep current Bootstrap/Bootstrap Icons conventions and integrate into the existing dashboard, project, and task views.

**Rationale**: No test project or existing test files were found. The constitution requires automated checks for business rules and integration-level checks for persistence and authorization. The app already has service-backed dashboard summaries and in-app notifications that can be extended.

**Alternatives considered**: Manual-only testing does not satisfy the constitution. EF's non-relational InMemory provider alone would not verify SQL Server relationships, migrations, or constraints. A new frontend framework or separate web host is unnecessary.

## Resolved Technical Context

- No unresolved technical-context questions remain for the plan. Malware signature provisioning is documented as an environment prerequisite, not an unknown runtime service.
- The scanner adapter's process/result behavior and the baseline-adoption procedure are explicit implementation validation gates in this plan.
- No cloud SDK, cloud account, or external scanning endpoint is selected or required.