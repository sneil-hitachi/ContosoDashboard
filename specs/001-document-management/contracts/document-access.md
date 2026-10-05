# Document Access Contract

This is the internal browser/application contract for the Blazor Server feature. It is not a public or cloud API. All operations require the existing authenticated mock cookie unless noted; mock authentication is training-only.

## User-facing routes and actions

| Surface | Contract |
|---|---|
| `/documents` | Lists only the signed-in user's documents. Shows title, category, uploaded time, size, and project; supports the specified sort, filters, and search. |
| `/documents/shared` | Lists documents with an active explicit grant to the signed-in user or their current department/team. Every open/download rechecks access. |
| Project details | Lists documents for a project only after project membership or manager authorization. Project members may view/download; project managers may upload/manage within that project. |
| Task view | Lists authorized task attachments. Attach/upload requires task access; uploads inherit the task's project and cannot create a mismatched association. |
| Dashboard `/` | Shows the current user's five most recent uploads and their document count. |
| Administrator reporting view | Shows aggregate file-type, uploader, and access activity. Non-administrators receive no report data. |

Upload requires one title and one allowed text category per file. Description, project, and tags are optional. A user may select multiple files; each gets its own validation, scan, and result. The accepted extension set is enforced server-side; client MIME type and file name are not trusted as security controls.

## Authenticated content endpoints

### `GET /documents/{documentId:int}/download`

Returns an authorized file as an attachment. The service resolves the integer ID, re-evaluates the current principal's ownership/project/team/share/admin access, and obtains the file through `IFileStorageService`. The response never exposes a physical path or storage key.

### `GET /documents/{documentId:int}/preview`

Returns an authorized PDF or JPEG/PNG image inline. Other file types are rejected for preview and remain available through download. Set the validated MIME type and `X-Content-Type-Options: nosniff`.

### Responses

| Status | Meaning |
|---|---|
| `200` | Complete authorized content with validated content type and safe content disposition. |
| `401` | No authenticated application identity. |
| `404` | Document is absent or inaccessible; do not reveal whether another user's document exists. |
| `400` | Invalid document identifier or preview requested for an unsupported preview type. |
| `500` | Generic actionable failure; log diagnostic details server-side without logging file content, credentials, or absolute paths in the response. |

Content routes do not accept filesystem paths, client-provided storage names, or client-supplied user IDs. They do not issue public or bearer URLs. Every request rechecks access so stale list results do not preserve revoked project membership or share access.

## Service boundaries

- `IDocumentService`: upload/replace validation, metadata CRUD, scoped list/search, task attachment, share/notification, permanent deletion, audit/report queries, and resource authorization.
- `IFileStorageService`: local upload, delete, download, and URL/key operations as required by the storage abstraction. Local data remains outside the web root; content is exposed only through the authorized document controller. A storage key is never itself an authorization grant.
- `IMalwareScanner`: scans a staged file and returns a clean, threat, or indeterminate result. Only a complete clean result permits publication; threat and indeterminate results fail closed.

The service derives actor identity and role from the authenticated principal, not a request parameter. Department/team scope is populated from the persisted mock-login user. Document, task, project, and recipient identifiers from the browser are independently checked against persisted relationships before reads or writes.

## Access rules

- Owner: view/download and edit metadata, replace, share, or delete their own document.
- Project member: view/download project documents; no management permission solely from membership.
- Project manager: manage documents within their projects, subject to Personal Files privacy.
- Team lead: manage documents within their department or project scope; Personal Files remain private unless explicitly shared.
- Explicit recipient: view/download the document granted to that user or their current department/team; no owner/manager rights arise from a share.
- Administrator: access all documents and audit/report data.
- Personal Files: private to owner and administrators unless explicitly shared, regardless of team/project visibility.
- Anonymous or out-of-scope user: no list metadata, preview bytes, download bytes, or filesystem information.