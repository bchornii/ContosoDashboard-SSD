<!--
SYNC IMPACT REPORT
==================
Version change: (unfilled template) → 1.0.0
Modified principles: N/A (initial ratification — all sections new)
Added sections: Core Principles (I–V), Technology Stack Constraints, Development Workflow, Governance
Removed sections: None
Templates requiring updates:
  ✅ .specify/templates/plan-template.md — Constitution Check gates align with principles below
  ✅ .specify/templates/spec-template.md — No changes required; template is principle-agnostic
  ✅ .specify/templates/tasks-template.md — No changes required; framework is principle-agnostic
Follow-up TODOs: None — all placeholders resolved on initial ratification
-->

# ContosoDashboard Constitution

## Core Principles

### I. Spec-Driven Development (SDD) — NON-NEGOTIABLE

Every feature MUST begin with a written specification before any implementation code
is written. The workflow is: Specify → Plan → Tasks → Implement — no step may be
skipped or reversed. Specs MUST document user scenarios with acceptance criteria,
functional requirements, and key entities. Implementation that precedes a spec is
considered out-of-process and MUST be retroactively documented.

### II. Offline-First with Infrastructure Abstraction

All infrastructure dependencies (database, file storage, authentication, external
services) MUST be hidden behind interface abstractions (e.g., `IFileStorageService`,
`IProjectService`). The training implementation MUST work fully offline without any
Azure subscription, cloud account, or external network access. Every abstraction MUST
have a documented production migration path (e.g., local filesystem → Azure Blob
Storage via DI swap). No business logic may reference a concrete infrastructure class
directly.

### III. Security-Conscious Development — NON-NEGOTIABLE

All new features MUST implement defense in depth: middleware authorization,
`[Authorize]` page attributes, AND service-level authorization checks. IDOR
(Insecure Direct Object Reference) protection is mandatory — every data access call
MUST verify the requesting user has rights to the target resource. Security headers
(CSP, X-Frame-Options, X-XSS-Protection) MUST remain intact. Known training-only
security limitations (mock auth, no password hashing) MUST be explicitly documented
in code comments and never silently expanded.

### IV. Clean Separation of Concerns

The four-layer architecture — Models, Data (EF Core), Services, Pages — MUST be
maintained. Pages MUST NOT contain business logic; they call services only. Services
MUST NOT reference Blazor/UI concerns. Models MUST be plain entities without
presentation logic. New cross-cutting concerns MUST be introduced as services
registered via dependency injection, not as static helpers or page-level utilities.

### V. Simplicity with Documented Limitations

YAGNI (You Aren't Gonna Need It) applies: implement only what the current spec
requires. All known limitations, training-only shortcuts, and intentional
simplifications MUST be documented (in code comments, README, or spec). Adding
production-grade complexity (e.g., real OAuth, cloud storage) is out of scope for
training features unless explicitly required by a spec. Complexity MUST be justified
in the spec's requirements section.

## Technology Stack Constraints

- **Framework**: ASP.NET Core 8.0 — no framework upgrades without a spec and
  migration plan
- **UI**: Blazor Server — client-side Blazor (WASM) is out of scope for training
- **Database**: SQL Server LocalDB via EF Core — no alternative ORMs; connection
  strings are the only permitted swap point for migration
- **Authentication**: Cookie-based mock auth for training; any replacement MUST use
  the existing `CustomAuthenticationStateProvider` abstraction
- **Styling**: Bootstrap 5.3 + Bootstrap Icons — no additional CSS frameworks
- **Target runtime**: .NET 8.0 LTS — no preview or release-candidate runtimes
- **Language**: C# — no mixing of other languages in the server-side codebase

## Development Workflow

1. **Specify** — Run `/speckit.specify` to produce `specs/[###-feature]/spec.md`
   with user stories, acceptance criteria, and requirements.
2. **Plan** — Run `/speckit.plan` to produce `plan.md`, `research.md`,
   `data-model.md`, and `contracts/` under the same spec folder.
3. **Tasks** — Run `/speckit.tasks` to produce `tasks.md` ordered by dependency.
4. **Implement** — Run `/speckit.implement` to execute tasks; each task is
   committed atomically where possible.
5. **Analyze** — Run `/speckit.analyze` to validate cross-artifact consistency
   before closing the feature branch.

All PRs MUST reference the corresponding spec. Constitution compliance MUST be
verified in the PR description before merge. Breaking changes to existing services
or models MUST be flagged in the spec's requirements with a migration note.

## Governance

This constitution supersedes all informal conventions. Amendments require:

1. A written rationale describing what changed and why.
2. A version bump following semantic versioning:
   - **MAJOR**: Removal or redefinition of an existing principle.
   - **MINOR**: Addition of a new principle or section.
   - **PATCH**: Clarification, wording fix, or non-semantic refinement.
3. Propagation review across all `.specify/templates/` files (update where needed).
4. A commit with message format:
   `docs: amend constitution to vX.Y.Z (<summary of change>)`

For runtime development guidance, refer to `README.md` and
`.github/copilot-instructions.md`.

**Version**: 1.0.0 | **Ratified**: 2026-04-28 | **Last Amended**: 2026-04-28
