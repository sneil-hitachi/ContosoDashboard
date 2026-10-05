# Quickstart: Document Upload and Management

This guide validates the feature in the local training environment. It does not establish production security or compliance.

## Prerequisites

- Windows with the .NET 10 SDK and SQL Server LocalDB.
- A local ClamAV Windows installation with `clamscan.exe` and a usable local signature database. Prepare/update signatures before disconnecting; the application must not contact an update service at runtime.
- The scanner executable path and local storage root configured for the development environment. Storage must resolve outside `ContosoDashboard/wwwroot`.
- Existing LocalDB users/projects may contain training data. Back up before schema changes. Do not drop the database unless you explicitly choose the documented reset fallback.

If ClamAV or its signature database is unavailable, uploads are expected to fail closed.

## Build and Automated Tests

From the repository root:

```powershell
dotnet restore ContosoDashboard/ContosoDashboard.csproj
dotnet restore tests/ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
dotnet build ContosoDashboard/ContosoDashboard.csproj
dotnet test tests/ContosoDashboard.Tests/ContosoDashboard.Tests.csproj
```

The test project uses a fake scanner for deterministic upload/security cases and isolated LocalDB databases for relational and authorization integration tests. Tests must not use the default shared training database.

## Database Preparation

- **New LocalDB**: apply the EF Core migrations documented by the feature's database setup, then start the app. Verify seed users, projects, tasks, and announcements are present.
- **Existing `EnsureCreated` LocalDB**: back up the database, compare its schema to the checked-in baseline, run the documented non-destructive baseline-adoption step, and apply the document migration. The app must not automatically drop, recreate, or mark an unverified schema as migrated.
- **Schema cannot be adopted**: only use the documented reset as an explicit opt-in after confirming the backup and accepting loss of local training data. Restart the app and verify seeded data is recreated.

## Manual End-to-End Scenarios

1. Start the application with `dotnet run --project ContosoDashboard/ContosoDashboard.csproj` and sign in through the mock login as an employee.
2. Upload a supported PDF or image with title/category and optional description, tags, and project. Confirm progress, success, captured metadata, My Documents visibility, and the local file exists only under the configured non-public storage root.
3. Upload the standard EICAR test string as a `.txt` file. Confirm the scanner rejects it, the UI gives an actionable message, and neither an available document row nor final stored file remains. Do not test with live malware.
4. Submit a file over 25 MB and an unsupported extension. Confirm each is rejected before it becomes available. For a mixed batch, confirm each selected file gets an independent result.
5. As the owner, edit metadata, replace the file with a clean test file, then verify the new content is current. Try a scanner failure during replacement and confirm the prior file remains usable.
6. Share a non-personal document with one user and with a department/team. Confirm only recipients can find/open it and each receives an in-app notification. Confirm an unselected user cannot discover it in search or fetch content by guessed document ID.
7. Verify a team lead can manage within their team/project scope but cannot open another user's Personal Files unless explicitly shared. Verify project members can access project documents, a project manager can manage documents in their project, and an administrator can access audit/report views.
8. Attach an existing authorized document to a task and upload another from the task view. Confirm the task/project association, project visibility, and duplicate-attachment behavior.
9. Confirm the dashboard shows the five most recent documents uploaded by the signed-in user and the correct document count. Confirm project upload and explicit-share notifications.
10. Confirm the administrator report contains file-type totals, uploader activity, and document access events; confirm a non-administrator is denied.
11. Test authorized preview/download and direct unauthorized preview/download. Unauthorized and nonexistent IDs must not return file bytes or reveal a local path.
12. At narrow and wide browser widths, verify upload validation/progress, sortable/filterable list, search, preview, and project/task entry points remain usable without overlapping content.

## Performance Checks

- Time a supported 25 MB file transfer separately from malware scanning; transfer target is under 30 seconds on a typical network.
- Populate 500 authorized documents and measure list and search response; each target is under 2 seconds.
- Measure a PDF/image preview; target is under 3 seconds.
- Record hardware, scanner/signature state, LocalDB state, and timings. Do not report a threshold as passing if the environment or scan result was not verified.

See [the data model](data-model.md) for state and persistence rules and [the document access contract](contracts/document-access.md) for authorization and content responses.