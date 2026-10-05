# Data Model: Document Upload and Management

The model extends the existing EF Core entities in `ContosoDashboard/Models` and `ApplicationDbContext`. IDs use SQL integer keys. User-entered names and descriptions are metadata only; they never determine storage paths.

## Entities

### Document

Represents one accepted, currently available file and its searchable metadata.

| Field | Type / rule | Notes |
|---|---|---|
| DocumentId | Integer primary key | Required; consistent with existing User and Project keys. |
| Title | String, required | User-provided display title. |
| Description | Nullable string | Optional searchable description. |
| Category | String, required | One of the six exact text values in FR-004; not an integer enum. |
| OriginalFileName | String, required | Display/download name only; never a path. |
| FilePath | Relative string, required, unique | Server-generated GUID name and validated extension under the configured local storage root. Never return it to the browser. |
| FileSize | 64-bit integer, required | Positive and no larger than the configured 25 MB per-file limit. |
| FileType | String, required, max 255 characters | Validated/normalized MIME type for supported file types. |
| UploadedAtUtc | Date/time, required | Server-assigned UTC upload time. |
| UploadedByUserId | Integer FK to User, required | Document owner/uploader. |
| ProjectId | Nullable integer FK to Project | Null for documents without a project. Task uploads inherit the task's current project. |

**Relationships and indexes**: User has many documents; Project has many associated documents. Index uploader, project, category, and upload time for common filters, dashboard queries, and access checks. `FilePath` has a unique index. The database never stores file content.

### DocumentTag

Represents one normalized custom tag for a document.

| Field | Type / rule | Notes |
|---|---|---|
| DocumentTagId | Integer primary key | |
| DocumentId | Integer FK to Document, required | Delete with document. |
| Value | String, required | Trimmed display value. |
| NormalizedValue | String, required | Trimmed, case-folded value used for duplicate detection and tag search. |

**Constraint**: Unique `(DocumentId, NormalizedValue)`; index `NormalizedValue` for tag search. An absent tag collection is valid.

### TaskDocument

Associates a document with a task, including when an existing document is attached to more than one task.

| Field | Type / rule | Notes |
|---|---|---|
| TaskDocumentId | Integer primary key | |
| TaskId | Integer FK to TaskItem, required | |
| DocumentId | Integer FK to Document, required | |
| AttachedByUserId | Integer FK to User, required | Actor who attached the existing document. |
| AttachedAtUtc | Date/time, required | Server-assigned UTC time. |

**Constraint**: Unique `(TaskId, DocumentId)`. Attaching a document requires access to both the task and the document. A task's project must agree with the document's project; a task without a project cannot create an inconsistent project association.

### DocumentShare

Represents an explicit grant from a document owner to a user or department/team.

| Field | Type / rule | Notes |
|---|---|---|
| DocumentShareId | Integer primary key | |
| DocumentId | Integer FK to Document, required | Delete grants when the document is permanently deleted. |
| GrantedByUserId | Integer FK to User, required | Must be the owner for v1 sharing. |
| RecipientUserId | Nullable integer FK to User | Set for a user grant. |
| RecipientDepartment | Nullable string | Set for a team grant; uses the existing Department value. |
| GrantedAtUtc | Date/time, required | Server-assigned UTC time. |

**Constraint**: Exactly one recipient form is set. Duplicate active grants for the same document and recipient are prevented. Only the owner may create a grant; access checks re-evaluate the current recipient identity or department on every request. Project/team membership does not silently grant access to Personal Files.

### DocumentActivity

An append-only audit event for upload, download, deletion, share, metadata change, and replacement.

| Field | Type / rule | Notes |
|---|---|---|
| DocumentActivityId | Integer primary key | |
| DocumentId | Nullable integer | Historical identifier; deliberately not a cascading FK so permanent deletion cannot erase the audit record. |
| DocumentTitleSnapshot | String, required | Minimal identifying metadata retained for reports after deletion. Never store file content. |
| ActorUserId | Nullable integer | Historical identifier, not a cascading FK. |
| ActorNameSnapshot | String, required | Display-name snapshot for audit interpretation. |
| Action | String, required | One of the specified activity types. |
| OccurredAtUtc | Date/time, required | Server-assigned UTC time. |

**Access**: Complete event access and reports are administrator-only. No retention duration is introduced by this feature; existing audit records are not deleted with a document.

## State and consistency rules

1. A request has transient states `Validating`, `Staged`, `Scanning`, and `Accepted` or `Rejected`; no `Document` row is visible before a clean scan and successful persistence.
2. Upload order: authenticate and authorize; validate metadata, extension, declared/actual size; copy to private staging with a server-generated name; scan the complete staged content; generate the final relative path; move into the private accepted-file area; save metadata, tags, association, and activity. If a later database operation fails, delete the just-written final file and leave no accepted record.
3. Replacement follows the same validation and scan path. Keep the existing accepted file until the replacement is clean and ready; switch the current path atomically and then remove the old file. On failure, preserve the prior document.
4. A confirmed deletion removes the accepted file, document metadata, tags, task links, and grants. The deletion activity event remains. A cancel action changes nothing.
5. Service authorization derives the actor from the authenticated principal. Project access comes from ProjectManagerId or ProjectMember; team scope comes from the actor/uploader Department; explicit shares are evaluated against current user or department. Personal Files are private to their owner and administrators unless explicitly shared.
6. Scanner absence, threat detection, timeout, unusable signatures, or an incomplete/ambiguous result rejects the upload or replacement. Staged content is cleaned up and never served.