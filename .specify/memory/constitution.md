<!--
Sync Impact Report (temporary review material; remove before committing)
Version change: 1.0.0 -> 1.0.0 (no governance changes)
Modified principles: None
Added sections: None
Removed sections: None
Deferred items:
- TODO(RATIFICATION_DATE): Maintainer must confirm the original adoption date.
-->
# ContosoDashboard Constitution

## Core Principles

### I. Specification-Led Brownfield Changes

Every feature change MUST have a specification with observable acceptance criteria before
implementation. Its plan MUST identify the existing behavior, affected layers, and any deliberate
compatibility changes. Implementation tasks MUST trace to the specification. Existing behavior
outside the approved scope MUST be preserved; unrelated refactors MUST be separately scoped.
This keeps Spec-Driven Development teachable against a working brownfield application.

### II. Training-Only, Local-First Operation

The application MUST remain explicitly designated for training, not production use. Core training
workflows MUST run locally without cloud accounts, cloud credentials, or external service APIs.
New persistence and file-storage features MUST provide local implementations. Initial setup
dependencies, including package restoration and any externally hosted UI assets, MUST be documented
with their offline limitations; no change may silently introduce a new runtime network dependency.
Cloud migration examples MUST remain optional and MUST NOT become prerequisites for training.

### III. Authorization at Every Data Boundary

Protected pages MUST require authentication, and services MUST authorize reads and mutations against
the current user's claims, role, and resource access rules. UI visibility alone MUST NOT authorize
an operation. Resource identifiers supplied by clients MUST NOT bypass membership or ownership
checks. Changed authorization behavior MUST include allowed and denied cases, including attempts
to access another user's resources. Mock user-selection authentication MUST remain identified as
a training simplification, not a production security guarantee.

### IV. Layered Design and Proportionate Abstractions

Changes MUST respect the existing separation between Pages and Shared UI, Services, Models, and
Data. Business rules and resource authorization MUST reside in services rather than being duplicated
in page handlers. Infrastructure implementations MUST be supplied through dependency injection;
new replaceable storage or external-service dependencies MUST expose interfaces so local and
optional cloud implementations do not require changes to business logic. New abstractions MUST
have a concrete substitution or complexity-reduction purpose, not merely add architectural layers.

### V. Verifiable Behavior and Explicit Limitations

Acceptance criteria MUST be verified before a change is considered complete. Changed business
rules MUST have focused automated tests; changes to service contracts, persistence relationships,
or authorization MUST include integration-level verification. Bug fixes MUST include a regression
check that fails for the original defect. UI changes MUST be checked through the affected user
workflow, including relevant roles and narrow and wide layouts. Failures MUST produce actionable
diagnostics without exposing secrets or document contents. Known limitations and checks that could
not run MUST be recorded rather than reported as passing.

## Technology and Scope Constraints

- The current baseline MUST be C# on .NET 10 / ASP.NET Core, Blazor Server with Razor Pages for
	login and logout, and Entity Framework Core with SQL Server LocalDB. The project file is the
	authority for the target framework when older documentation disagrees.
- UI changes MUST reuse the existing Bootstrap and Bootstrap Icons conventions unless a design
	system change is explicitly specified and approved.
- Mock cookie authentication and claims-based roles MUST remain the default training setup.
	Production identity, cloud deployment, and external integrations require separately approved
	scope and MUST NOT be represented as existing production capabilities.
- Stored configuration MUST NOT contain real credentials or production connection strings.
	Training fixtures MUST use fictional users and data.
- Changes to persistent data MUST document compatibility with existing LocalDB data and the
	required migration or reset steps. Destructive resets MUST be explicit and opt-in.
- File-handling features MUST validate accepted uploads, enforce resource authorization, and use
	server-generated unique storage identifiers rather than treating client filenames as paths.
- Documentation MUST distinguish implemented capabilities from proposed migration examples and
	MUST describe prerequisites and limitations accurately.

## Development Workflow and Quality Gates

1. Capture stakeholder requirements in a specification and resolve ambiguities that affect
	 acceptance criteria, authorization, or persistence before implementation.
2. Produce a plan and dependency-ordered tasks that identify impacted code, validation steps,
	 data compatibility, and compliance with each core principle.
3. Implement the smallest scoped change using existing conventions. Record any necessary
	 deviation and its rationale for maintainer approval; do not silently bypass governance.
4. Run `dotnet build ContosoDashboard/ContosoDashboard.csproj` and the focused tests required
	 by the changed behavior. Record results and any environment blockers. Manual verification
	 MUST document steps and expected outcomes and MUST NOT replace required automated tests.
5. Review the diff for acceptance coverage, authorization, data compatibility, dependency changes,
	 and accurate training documentation. Acceptance MUST NOT be claimed while a required check
	 remains unverified; any temporary waiver MUST be explicit and maintainer-approved.

## Governance

This constitution governs specifications, plans, tasks, implementation, and review. Conflicting
project guidance MUST be reconciled with it before work proceeds. Every plan and change review
MUST assess compliance with the core principles and record approved deviations.

Amendments MUST state the changed rules, rationale, compatibility impact, and any migration or
follow-up work. A project maintainer MUST approve an amendment before it is treated as ratified.
Constitution amendments MUST be separate from unrelated feature implementation. Dependent
templates and commands read this document at runtime; this workflow MUST NOT edit those artifacts.

Versions MUST follow semantic versioning: MAJOR for incompatible removals or redefinitions of
governance, MINOR for new principles or materially expanded guidance, and PATCH for clarifications
that do not change obligations. Amendments MUST update the version and last-amended date; the
original ratification date MUST be preserved once confirmed. The temporary Sync Impact Report
MUST be reviewed and removed before the amended constitution is committed.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): Maintainer must confirm the original adoption date. | **Last Amended**: 2026-10-02
