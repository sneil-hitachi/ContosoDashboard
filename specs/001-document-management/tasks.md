---
description: "Dependency-ordered implementation tasks for document upload and management"
---

# Tasks: Document Upload and Management

**Input**: Design documents from `/specs/001-document-management/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/document-access.md`, and `quickstart.md`

**Tests**: Focused automated unit and LocalDB integration tests are included because the project constitution requires verification of changed business rules, persistence relationships, and authorization.

**Organization**: Tasks are grouped by user story to enable incremental implementation and independent acceptance checks. Tests precede implementation in each story.

## Phase 1: Setup

**Purpose**: Establish the test harness and repeatable local scanner/storage configuration without changing runtime cloud dependencies.

- [X] T001 Create the xUnit test project with a reference to the application in `tests/ContosoDashboard.Tests/ContosoDashboard.Tests.csproj`.
- [X] T002 [P] Add reusable fake scanner, temporary file storage, authenticated-principal factory, and isolated LocalDB fixture in `tests/ContosoDashboard.Tests/TestInfrastructure/FakeMalwareScanner.cs`, `tests/ContosoDashboard.Tests/TestInfrastructure/TemporaryFileStorage.cs`, `tests/ContosoDashboard.Tests/TestInfrastructure/TestPrincipalFactory.cs`, and `tests/ContosoDashboard.Tests/TestInfrastructure/SqlServerLocalDbFixture.cs`.
- [X] T003 [P] Document ClamAV executable/signature setup, local storage configuration, and fail-closed behavior in `specs/001-document-management/quickstart.md`.

## Phase 2: Foundational

**Purpose**: Make schema evolution safe and provide the identity data required by all document authorization rules. Complete before user stories.

- [X] T004 [P] Add tests for fresh database creation, existing `EnsureCreated` baseline adoption, and preservation of seeded training data in `tests/ContosoDashboard.Tests/Database/MigrationCompatibilityTests.cs`.
- [X] T005 Replace startup `EnsureCreated` with EF Core migration initialization and add the verified baseline plus safe adoption procedure in `ContosoDashboard/Program.cs` and `ContosoDashboard/Data/Migrations/`.
- [X] T006 [P] Add tests that mock-login principals contain NameIdentifier, Name, Email, Role, and Department claims sourced from the selected persisted user in `tests/ContosoDashboard.Tests/Authentication/MockLoginClaimsTests.cs`.
- [X] T007 Add the Department claim to the mock login identity and preserve the existing training authentication flow in `ContosoDashboard/Pages/Login.cshtml.cs`.

**Checkpoint**: Database changes preserve existing LocalDB data, and all document services can derive identity, role, and team scope from the principal.

## Phase 3: User Story 1 - Upload a Document (Priority: P1)

**Goal**: Upload supported files with required metadata, per-file progress/results, malware screening, and private local storage; expose accepted uploads in the owner's basic document list.

**Independent Test**: As an authenticated employee, upload supported clean files with valid title/category and confirm metadata and files appear only after a clean scan. Verify invalid, oversized, infected, indeterminate, and mixed-batch outcomes independently.

### Tests for User Story 1

- [X] T008 [P] [US1] Add upload service tests for required title/category, all six category values, supported extensions, 25 MB per-file limit, MIME metadata, and independently reported mixed-batch results in `tests/ContosoDashboard.Tests/Services/DocumentUploadValidationTests.cs`.
- [X] T009 [P] [US1] Add malware adapter tests for clean, threat, missing executable/signatures, timeout, nonzero exit, incomplete output, and paths containing spaces in `tests/ContosoDashboard.Tests/Services/ClamAvMalwareScannerTests.cs`.
- [X] T010 [P] [US1] Add local storage tests for unique generated paths, staging/final separation, path traversal resistance, cleanup, and storage outside `wwwroot` in `tests/ContosoDashboard.Tests/Services/LocalFileStorageServiceTests.cs`.
- [X] T011 [P] [US1] Add upload integration tests proving files are not downloadable or persisted as available before a clean scan and that database failure removes the newly stored file in `tests/ContosoDashboard.Tests/Integration/DocumentUploadIntegrationTests.cs`.

### Implementation for User Story 1

- [X] T012 [P] [US1] Create the integer-keyed document metadata entity with required title/category, 255-character file type, generated relative path, file size, uploader, upload time, and optional project in `ContosoDashboard/Models/Document.cs`.
- [X] T013 [US1] Configure the Document entity's User/Project relationships and query/unique indexes in `ContosoDashboard/Data/ApplicationDbContext.cs`.
- [X] T014 [P] [US1] Define fail-closed malware scan results and the scanner abstraction in `ContosoDashboard/Services/IMalwareScanner.cs`.
- [X] T015 [P] [US1] Implement the local ClamAV process adapter using argument-list invocation, bounded execution, and unambiguous clean-result parsing in `ContosoDashboard/Services/ClamAvMalwareScanner.cs`.
- [X] T016 [P] [US1] Define local file upload, delete, download, and relative-key operations in `ContosoDashboard/Services/IFileStorageService.cs`.
- [X] T017 [P] [US1] Implement private staging and final local file operations under a configurable root outside `wwwroot`, using server-generated GUID names in `ContosoDashboard/Services/LocalFileStorageService.cs`.
- [X] T018 [US1] Implement authenticated-principal authorization, metadata/type/size validation, scan-before-publish ordering, per-file result handling, and compensation on persistence failure in `ContosoDashboard/Services/DocumentService.cs`.
- [X] T019 [US1] Register document, local storage, and local scanner services and bind local-only settings in `ContosoDashboard/Program.cs` and `ContosoDashboard/appsettings.Development.json`.
- [X] T020 [US1] Create the keyed Blazor upload form with per-file title/category/optional metadata, stream-copy handling, upload progress, and independent success/error results in `ContosoDashboard/Shared/DocumentUploadForm.razor`.
- [X] T021 [US1] Add the authenticated `/documents` page's basic owner-only accepted-document list and upload entry point in `ContosoDashboard/Pages/Documents.razor`.

**Checkpoint**: User Story 1 can upload a clean supported file, show it to its owner, and reject every unsafe or indeterminate scan result without exposing staged content.

## Phase 4: User Story 2 - Find and Use Authorized Documents (Priority: P1)

**Goal**: Browse, filter, sort, and search authorized documents and securely preview/download files.

**Independent Test**: Seed documents across owners, projects, Personal Files, and explicit recipients; verify each account's list/search scope and authorized or denied content requests, including direct ID requests.

### Tests for User Story 2

- [X] T022 [P] [US2] Add list/search tests for title, description, tags, uploader, project, category/date/project filters, allowed sort keys, and result scoping in `tests/ContosoDashboard.Tests/Services/DocumentQueryAuthorizationTests.cs`.
- [X] T023 [P] [US2] Add authenticated content endpoint tests for authorized PDF/image preview, attachment download, unsupported preview types, inaccessible IDs, and anonymous requests in `tests/ContosoDashboard.Tests/Integration/DocumentContentAuthorizationTests.cs`.
- [X] T024 [P] [US2] Add project access tests for members, managers, removed members, and Personal Files privacy in `tests/ContosoDashboard.Tests/Integration/ProjectDocumentAuthorizationTests.cs`.

### Implementation for User Story 2

- [X] T025 [US2] Implement owner/project/share-scoped query, search, filter, and allowlisted sorting operations in `ContosoDashboard/Services/DocumentService.cs`.
- [X] T026 [US2] Implement authenticated preview/download routes that reauthorize each request, conceal inaccessible document existence, set safe content headers, and stream only accepted files in `ContosoDashboard/Controllers/DocumentsController.cs`.
- [X] T027 [US2] Complete My Documents and Shared with Me tables with required metadata, filters, sort controls, and search fields in `ContosoDashboard/Pages/Documents.razor`.
- [X] T028 [US2] Add authorized project-document listing and manager-only project upload/manage entry points in `ContosoDashboard/Pages/ProjectDetails.razor`.
- [X] T029 [US2] Register controller routing and require authentication for document content routes in `ContosoDashboard/Program.cs`.

**Checkpoint**: User Story 2 returns only authorized metadata and streams only accepted content after fresh service-layer authorization.

## Phase 5: User Story 3 - Manage and Share Documents (Priority: P2)

**Goal**: Edit metadata, replace the current file safely, permanently delete after confirmation, and share with selected users or teams.

**Independent Test**: As an owner, update metadata, replace with a clean file, share with chosen recipients, and delete after confirmation; verify recipients alone gain access and unauthorized management actions fail.

### Tests for User Story 3

- [ ] T030 [P] [US3] Add owner/manager/admin authorization tests for metadata edits, replacement, and rejected replacement preserving the current accepted file in `tests/ContosoDashboard.Tests/Services/DocumentManagementAuthorizationTests.cs`.
- [ ] T031 [P] [US3] Add sharing tests for user/team grants, duplicate grants, Personal Files, revoked access, and denied non-owner sharing in `tests/ContosoDashboard.Tests/Services/DocumentSharingTests.cs`.
- [ ] T032 [P] [US3] Add deletion tests for confirmation, cancellation, owner/project-manager/admin scope, permanent content removal, and denied unauthorized deletion in `tests/ContosoDashboard.Tests/Integration/DocumentDeletionTests.cs`.

### Implementation for User Story 3

- [ ] T033 [P] [US3] Create share records with exactly one user or department recipient and persisted grant metadata in `ContosoDashboard/Models/DocumentShare.cs`.
- [ ] T034 [US3] Configure share relationships, uniqueness constraints, and document-delete cascade behavior in `ContosoDashboard/Data/ApplicationDbContext.cs`.
- [ ] T035 [US3] Implement metadata editing, staged-and-scanned replacement that preserves the old file on failure, confirmed permanent deletion, and owner/manager/admin checks in `ContosoDashboard/Services/DocumentService.cs`.
- [ ] T036 [US3] Implement recipient selection, share creation/revocation, and in-app notifications through the existing notification service in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Services/NotificationService.cs`.
- [ ] T037 [US3] Add metadata edit, replace, share, revoke, and confirm-delete controls with actionable validation results in `ContosoDashboard/Pages/Documents.razor`.

**Checkpoint**: User Story 3 preserves old content on replacement failure, requires delete confirmation, and grants no access beyond explicit recipients and authorized managers.

## Phase 6: User Story 4 - Use Documents from Projects, Tasks, and the Dashboard (Priority: P2)

**Goal**: Attach documents to tasks, inherit the task's project association, notify project members, and show recent uploads/count on the dashboard.

**Independent Test**: Attach an existing accessible document and upload from a task, confirm project consistency and access, trigger project notifications, and verify the user's five recent documents and count.

### Tests for User Story 4

- [ ] T038 [P] [US4] Add task attachment tests for task/document authorization, duplicate attachment rejection, and task/project association consistency in `tests/ContosoDashboard.Tests/Services/TaskDocumentIntegrationTests.cs`.
- [ ] T039 [P] [US4] Add dashboard and project notification tests for recent-five ordering, document count, member recipients, and unauthorized user isolation in `tests/ContosoDashboard.Tests/Services/DocumentDashboardTests.cs`.

### Implementation for User Story 4

- [ ] T040 [P] [US4] Create task-document association with actor, timestamp, and unique task/document relationship in `ContosoDashboard/Models/TaskDocument.cs`.
- [ ] T041 [US4] Configure task/document/user relationships and uniqueness/index rules in `ContosoDashboard/Data/ApplicationDbContext.cs`.
- [ ] T042 [US4] Implement authorized task attachment operations and require a document's project to match the task's project in `ContosoDashboard/Services/DocumentService.cs`.
- [ ] T043 [US4] Add task-document listing, attach-existing, and upload-related-document workflows to `ContosoDashboard/Pages/Tasks.razor`.
- [ ] T044 [US4] Notify project members after accepted project uploads and add recent-document/count queries to `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Services/DashboardService.cs`.
- [ ] T045 [US4] Add the current user's Recent Documents five-item widget and document count summary to `ContosoDashboard/Pages/Index.razor`.

**Checkpoint**: User Story 4 exposes only authorized task/project documents and dashboard data for the signed-in user.

## Phase 7: User Story 5 - Review Document Activity (Priority: P3)

**Goal**: Preserve document activity through deletion and provide administrator-only audit history and aggregate reports.

**Independent Test**: Perform each document operation, verify actor/document/action/time audit entries, generate all three reports as an administrator, and verify non-admin denial.

### Tests for User Story 5

- [ ] T046 [P] [US5] Add audit tests for uploads, downloads, shares, metadata changes, replacements, deletions, retained title/actor snapshots, and activity timestamps in `tests/ContosoDashboard.Tests/Services/DocumentAuditTests.cs`.
- [ ] T047 [P] [US5] Add report authorization and aggregation tests for file types, active uploaders, access patterns, and non-administrator denial in `tests/ContosoDashboard.Tests/Services/DocumentReportAuthorizationTests.cs`.

### Implementation for User Story 5

- [ ] T048 [P] [US5] Create append-only document activity events with nullable historical IDs and actor/title snapshots in `ContosoDashboard/Models/DocumentActivity.cs`.
- [ ] T049 [US5] Configure activity persistence without cascading deletion and add report query indexes in `ContosoDashboard/Data/ApplicationDbContext.cs`.
- [ ] T050 [US5] Record upload, download, delete, share, metadata edit, and replacement events in the authorized document operations in `ContosoDashboard/Services/DocumentService.cs` and `ContosoDashboard/Controllers/DocumentsController.cs`.
- [ ] T051 [US5] Implement administrator-authorized activity queries and file-type/uploader/access-pattern report aggregates in `ContosoDashboard/Services/DocumentService.cs`.
- [ ] T052 [US5] Add an administrator-only activity and reporting page with denied access for all other roles in `ContosoDashboard/Pages/DocumentReports.razor`.

**Checkpoint**: User Story 5 reports all specified activity without losing deletion history and returns no audit data to non-administrators.

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Verify migrations, authorization boundaries, UI usability, performance targets, and documented local setup across the completed stories.

- [ ] T053 [P] Document existing LocalDB backup, verified baseline adoption, additive migration, and explicit opt-in reset steps in `specs/001-document-management/quickstart.md`.
- [ ] T054 [P] Add cross-story security tests for guessed IDs, stale membership/share grants, staged-file isolation, and path disclosure in `tests/ContosoDashboard.Tests/Integration/DocumentSecurityRegressionTests.cs`.
- [ ] T055 Run the narrow/wide UI, 500-document list/search, 25 MB transfer, preview timing, and EICAR scenarios and record environment/results in `specs/001-document-management/quickstart.md`.
- [ ] T056 Run `dotnet build ContosoDashboard/ContosoDashboard.csproj` and `dotnet test tests/ContosoDashboard.Tests/ContosoDashboard.Tests.csproj`, then record outcomes and any blocked LocalDB/ClamAV checks in `specs/001-document-management/quickstart.md`.

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Starts immediately; T001 is required before the test-helper task T002 and all automated test tasks. T003 can proceed in parallel with T001/T002.
- **Foundational (Phase 2)**: Begins after the test project exists; T005 follows migration compatibility tests T004, and T007 follows claims tests T006. Complete Phase 2 before all user stories.
- **User Stories (Phases 3-7)**: Execute in priority order for incremental delivery. Each story's tests precede its implementation. US2 depends on the accepted Document model/service established by US1; US3 depends on upload and authorized document access; US4 depends on document and project access; US5 instruments operations from US1-US4.
- **Polish (Phase 8)**: Depends on the intended user-story scope being complete; cross-story security tests can be authored after the shared test harness is ready.
- **Deferred Azure architecture**: No implementation tasks are included. Azure Functions, Queue Storage, Azure Blob Storage, and asynchronous `PendingScan` processing require separate scope approval and a revised offline/local-first decision before task generation.

### User Story Completion Order

1. **US1 (P1)**: Upload is the MVP foundation and establishes safe accepted-file state.
2. **US2 (P1)**: Browsing and authorized retrieval consume US1's document model and accepted-file service.
3. **US3 (P2)**: Metadata maintenance and sharing extend existing document authorization.
4. **US4 (P2)**: Task/project/dashboard integrations consume established documents and notification flows.
5. **US5 (P3)**: Audit/reporting records the completed operations and remains administrator-only.

### Parallel Execution Examples

- **Setup**: After T001, T002 can run while T003 updates the local setup guide.
- **Foundation**: T004 and T006 are independent tests; after they pass/fail as expected, T005 and T007 can proceed independently.
- **US1**: T008-T011 can be authored in parallel. After the model exists, storage and scanner implementations (T015 and T017) can proceed in parallel; service orchestration waits for both.
- **US2**: T022-T024 can be authored in parallel. Once service signatures are agreed, the Documents page and content controller can be implemented in separate files; route registration follows the controller.
- **US3**: T030-T032 can be authored in parallel. Share entity/configuration and UI changes can proceed separately after test contracts are established; DocumentService edits must be serialized with other phases that edit that file.
- **US4**: T038-T039 can be authored in parallel. Task association model/configuration can be developed separately from dashboard query/UI changes.
- **US5**: T046-T047 can be authored in parallel. Activity model/configuration can proceed separately from the report page; shared service/controller instrumentation should be integrated serially.

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Setup and Foundational phases, including a verified non-destructive database migration path.
2. Complete US1 with scanner/storage fakes and fail-closed upload behavior.
3. Validate the independent upload journey, including clean, threat, unavailable scanner, invalid type, oversize, and mixed-batch cases.
4. Do not expose a file as accepted unless scan and metadata persistence both complete successfully.

### Incremental Delivery

1. Complete Setup + Foundational; preserve existing LocalDB data.
2. Deliver US1 upload MVP; validate its independent acceptance criteria.
3. Deliver US2 authorized discovery and retrieval; validate all allow/deny cases.
4. Deliver US3 management/sharing, then US4 work-context integration, then US5 audit/reporting.
5. Finish cross-story security, responsive UI, and performance checks from `quickstart.md`.

### Independent Test Criteria by Story

- **US1**: Clean supported upload appears with exact required metadata after scanning; every invalid, oversized, unsafe, unavailable-scanner, and ambiguous result remains unavailable.
- **US2**: A user can sort/filter/search their authorized documents and preview/download permitted content; unauthorized direct requests disclose neither content nor existence.
- **US3**: Owner/manager operations follow scope; replacement failure preserves the old file; confirmed deletion is permanent; only intended recipients gain access.
- **US4**: Task associations match project scope; project members get required notifications; dashboard displays the user's latest five uploads and correct count.
- **US5**: All specified actions create attributable timestamped audit records; reports work for administrators and are denied to other roles.

## Notes

- Every task follows `- [ ] T### [P?] [US#?] Description with exact file path`; `[P]` is used only for independent work, and story labels appear only in story phases.
- Automated tests are required by the project constitution even though the general template marks tests optional.
- Azure asynchronous scanning is documented in `plan.md` as a future option only. Do not create cloud implementation tasks or add Azure dependencies under this approved offline feature scope.