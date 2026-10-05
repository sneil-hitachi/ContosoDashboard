# Implementation Plan: Document Upload and Management

**Branch**: `001-document-management` | **Date**: 2026-10-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/001-document-management/spec.md`

## Summary

Add secure document upload, browsing, sharing, project/task association, dashboard visibility, notifications, and audit reporting to the existing training application. Retain the current Blazor Server, EF Core, SQL Server LocalDB, claims-based mock authentication, and service-layer architecture. For the required offline training implementation, store files outside `wwwroot` behind `IFileStorageService`; stage each file privately, validate and scan it with a local ClamAV command-line installation, and publish it only after a clean scan. Use an `IMalwareScanner` seam so scan failures fail closed and tests do not depend on a host antivirus installation. Persist document metadata and access/activity records in LocalDB, and authorize every query and content request in the service layer. An optional production-migration design below describes asynchronous scanning with Azure Functions and Queue Storage; it is not part of this feature's implementation scope or a training runtime dependency.

## Optional Azure Asynchronous Scan Design (Not in Training Scope)

This section records a future cloud-hosted migration path only. Implementing it requires a separately approved scope change to the local-first constitution/specification and must not add Azure SDKs, credentials, cloud resources, or network requirements to the training implementation.

1. The upload API validates identity, metadata, extension, declared/actual size, and project/task authorization, then streams the file to a private quarantine container using a generated blob name. It persists the document as `PendingScan` with the blob identifier, content length, and SHA-256 digest. The quarantine blob is never downloadable or previewable.
2. After the blob and pending metadata are durable, the application enqueues a small `DocumentScanRequested` message in Azure Queue Storage. The message carries only the document ID, opaque blob identifier, expected content hash, and schema/message version; it contains no file bytes, user-controlled path, or authorization decision. A dispatch failure leaves the document pending and is retried/reconciled; it must not make the file available.
3. An Azure Function with a Queue Storage trigger loads the current document record and private blob, confirms the record is still `PendingScan` and the blob hash matches, then invokes the scanner. The Function must be idempotent: duplicate deliveries, visibility-timeout redelivery, or retries must not scan/promote the wrong content or repeat terminal side effects.
4. The scan engine runs inside the Function's supported deployment environment (for example, a Linux custom-container Function package containing ClamAV and locally provisioned signatures). Scanner execution is isolated behind the same malware-scanning contract. Availability of a compatible hosting plan, process permissions, signature provisioning/update policy, memory/CPU limits, and the 25 MB payload limit must be proven in a separate cloud design before implementation; do not assume an ordinary managed Function can launch an arbitrary native scanner binary.
5. A complete clean result causes the Function to promote/copy the exact hash-verified object from quarantine to a private accepted container, atomically set the document to `Available`, and record scan completion. Only `Available` documents may be listed as ready or served by authorized content routes. The UI reports `PendingScan` as processing and refreshes/polls or receives a later in-app notification for the terminal result.
6. A threat or content-integrity mismatch sets `Rejected`, keeps the object inaccessible, records an audit event, and applies the approved quarantine-retention policy. A transient infrastructure failure leaves the record pending for bounded retries. Exhausted messages land in the Queue Storage poison queue; an operator-visible diagnostic/alert and a reconciliation path are required. An indeterminate result never becomes `Available`.
7. Queue poison retention, duplicate-message handling, blob/SQL consistency, cleanup of abandoned quarantine blobs, notification idempotency, and deletion/replacement races require integration tests and operational runbooks. Queue messages are signals, not the source of truth; the database state and verified blob hash control publication.

The training implementation uses the same terminal-state rule but invokes local ClamAV inline: do not mark a document accepted until its local scan succeeds. Do not implement an Azure queue, Function, or blob adapter in the current feature branch without the separate approval described above.

## Technical Context

**Language/Version**: C# / .NET 10, nullable reference types enabled.

**Primary Dependencies**: Training implementation: ASP.NET Core Blazor Server, EF Core SQL Server provider 10.0.12, SQL Server LocalDB, Bootstrap 5.3 conventions; official ClamAV Windows command-line installation as a local runtime prerequisite. Add an xUnit test project; use the existing SQL Server provider for relational integration tests. Optional future migration only: Azure Functions Queue Storage trigger and private Azure Blob Storage, subject to separate scope approval and hosting/scanner feasibility validation.

**Storage**: Existing SQL Server LocalDB for metadata, shares, and activity; local filesystem for staged and accepted files. Store relative paths under a configurable application-data root outside `wwwroot`.

**Testing**: `dotnet build` and a new focused xUnit test project. Use deterministic fake scanner and temporary filesystem for service tests; use isolated LocalDB databases for migration, relationship, and authorization integration tests. Exercise a real ClamAV EICAR test fixture as an environment-gated validation.

**Target Platform**: Windows training/development host with .NET 10 SDK, SQL Server LocalDB, and ClamAV installed. Runtime upload, browse, and download flows must not call the network; scanner signatures must already be available locally.

**Project Type**: Existing single-project ASP.NET Core / Blazor Server web application with a separate test project.

**Performance Goals**: Accept one file up to 25 MB; complete file transfer within 30 seconds on a typical network, excluding malware scan time; list/search up to 500 documents within 2 seconds; PDF/image preview within 3 seconds.

**Constraints**: The approved training implementation has no cloud accounts, cloud SDKs, external service APIs, or required runtime internet access. All file content stays outside the web root and is served only through authenticated, authorized endpoints. File IDs remain integer keys, category values remain text, and MIME type storage supports 255 characters. Retain mock authentication as training-only. Never silently drop or recreate an existing database. The optional Azure architecture is descriptive only and requires a separate approved scope change.

**Scale/Scope**: Five prioritized user journeys; six fixed categories; multiple selected uploads with a 25 MB per-file limit; document lists of up to 500 rows; personal, department/team, project, task, and explicit-share access scopes.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Gate

| Principle / gate | Status | Evidence and plan response |
|---|---|---|
| I. Specification-led brownfield changes | PASS | Clarified spec and acceptance scenarios exist. The plan identifies current layers, affected surfaces, migration behavior, and focused tests. |
| II. Training-only, local-first operation | PASS WITH DOCUMENTATION GATE | LocalDB and local filesystem remain the runtime dependencies. ClamAV runs as a local process using locally installed signatures; its installation and signature preparation must be documented, with no startup update or cloud call. |
| III. Authorization at every data boundary | PASS WITH REQUIRED CHANGE | Existing pages use `[Authorize]` and services perform resource checks. The new document service and file endpoint must authorize from the authenticated principal on every read/mutation. Mock login currently omits Department; add it from the persisted user record for team-scope rules. |
| IV. Layered design and proportionate abstractions | PASS | Keep UI in Blazor, business rules in services, persistence in EF Core, and file/scanner infrastructure behind interfaces with concrete local implementations. |
| V. Verifiable behavior and explicit limitations | PASS WITH TEST-PROJECT ADDITION | No test project exists. Add focused service and LocalDB integration tests for upload validation, role/resource authorization, persistence, and denied content access; document UI/performance checks and environmental limitations. |
| Technology and scope constraints | PASS | Preserve .NET 10, Blazor Server, EF Core/LocalDB, Bootstrap, mock roles, local-only operation, integer document IDs, text categories, and explicit non-destructive database handling. |

No constitutional violations are identified. The malware scanner and test project are documented setup dependencies, not runtime network dependencies.

## Project Structure

### Documentation (this feature)

```text
specs/001-document-management/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── document-access.md
└── tasks.md              # Created by /speckit.tasks
```

### Source Code (repository root)
```text
ContosoDashboard/
├── Controllers/
│   └── DocumentsController.cs       # Authenticated preview/download only
├── Data/
│   ├── ApplicationDbContext.cs     # Document sets, relationships, indexes
│   └── Migrations/                 # Baseline adoption and document schema
├── Models/
│   ├── Document.cs
│   ├── DocumentShare.cs
│   ├── DocumentActivity.cs
│   ├── DocumentTag.cs
│   └── TaskDocument.cs
├── Pages/
│   ├── Documents.razor             # My Documents and Shared with Me
│   ├── ProjectDetails.razor        # Project documents
│   ├── Tasks.razor                 # Task attachments/upload entry point
│   └── Index.razor                 # Recent Documents and document count
├── Services/
│   ├── DocumentService.cs          # Business rules and resource authorization
│   ├── IFileStorageService.cs
│   ├── LocalFileStorageService.cs
│   ├── IMalwareScanner.cs
│   └── ClamAvMalwareScanner.cs
└── Program.cs                      # DI, routes, migrations, local configuration
tests/
└── ContosoDashboard.Tests/          # Unit and SQL Server LocalDB integration tests
```

**Structure Decision**: Extend the existing `ContosoDashboard` web project and add one test project under `tests/`. Reuse its `Models`, `Data`, `Services`, `Pages`, and DI registration conventions. Add one authenticated content controller because stored files are outside `wwwroot`; it delegates authorization and retrieval to the service layer. Do not add a separate API host or cloud project.

## Complexity Tracking

None. The scanner interface isolates a required local infrastructure dependency, and the storage interface is explicitly required for the cloud migration path; neither adds an unused abstraction.

## Constitution Check (Post-Design)

| Principle / gate | Status | Design evidence |
|---|---|---|
| I. Specification-led brownfield changes | PASS | Data model and protected content contract trace to the clarified spec; implementation tasks can map to FR-001 through FR-022. |
| II. Training-only, local-first operation | PASS WITH DOCUMENTATION GATE | The selected implementation uses ClamAV as a local executable and scan database. App runtime performs no signature download; setup documents how to provision/update definitions before offline use and how unavailable scanning blocks upload. Azure Functions/Queue Storage appear only as an optional future architecture, with an explicit separate-approval gate; no Azure SDK or cloud service is introduced. |
| III. Authorization at every data boundary | PASS | Service derives actor identity/role from the principal, rechecks owner/team/project/share scope for each operation, and content routes do not serve by path alone. Tests cover allowed and denied roles and stale grants/membership. |
| IV. Layered design and proportionate abstractions | PASS | Blazor UI calls DocumentService; EF entities/context own persistence; IFileStorageService and IMalwareScanner have concrete local adapters; controller only streams authorized content. |
| V. Verifiable behavior and explicit limitations | PASS | xUnit tests cover validation, scanner failures, compensation, permissions, and persistence; LocalDB tests cover relationships/migrations; quickstart covers EICAR and narrow/wide UI plus response-time checks. |
| Data compatibility and resets | PASS WITH REQUIRED MIGRATION WORK | Replace `EnsureCreated` with EF migrations. Existing `EnsureCreated` databases have no migration history: add a verified baseline-adoption path that preserves matching databases. Never drop automatically; document a backup-first, explicit opt-in reset only when an existing schema cannot be adopted. |

No design deviation from the constitution is required because the Azure Functions/Queue Storage architecture is not selected for this training feature. A future decision to implement it requires separate approval and corresponding spec, constitution, dependency, operational, and verification updates. The local AV executable, signature provisioning, Windows/LocalDB, and package restore requirements must be explicit in setup documentation.
