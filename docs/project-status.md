# WorldSeed Project Status

Standalone testing and status commits were consolidated locally on 2026-11-07.
QA reports retain the original commit IDs they actually tested; rewriting commit
parents changed the equivalent IDs on `main`. The pre-consolidation history is
preserved at `backup/main-before-test-doc-squash-20261107`.

## Current Development Task

The owner-authorized creature MVP implementation and isolated connected QA are
complete. Tested revision `6887ab9` (current equivalent `c569cbc`) connected the
actual Content Review controls and production generation path to local
`gemma3:12b`, an isolated Docker API and
Mongo database, persistence/reload, mechanical validation, game-specific
balance evaluation, revision history, approval/rejection, and approval-gated
export. QA independently passed that flow without reproducing the corrected
proposal-list stack overflow. A final normal-launcher manual desktop run remains
before the reopened usability objective can be called complete. The configured
Windows automation binaries exist and run directly, but both supported tool
surfaces still fail to initialize after a kernel reset, indicating a stale or
broken Codex computer-control session rather than a missing WorldSeed resource.

The completed workflow covers traceable fixture rules and schemas, bounded
proposal generation, hard mechanical validation, game-specific advisory balance
profiles, designer approval/rejection/revision history, approval-gated JSON
export, and documented results for the mechanically distinct Emberbound and
Quiet Horizons systems. See `docs/mvp-demonstration-results.md`.

The tracked Codex Stop hook still requires the owner's normal `/hooks` trust
review; no hook-trust bypass is permitted. The direct-call notification fallback
remains available for any later owner-action stop.

## Workflow Verification

- **Coordinator policy:** documented in `AGENTS.md` at revision `c79819d`.
- **QA confirmation:** verified that the historical clarification-dialog report
  applies exactly to `cb3e884`; `python qa/verify_setup.py` passed. It correctly
  did not claim that the newer documentation-only revision was application-tested.
- **Advisor assessment:** GREEN for evidence handling and role separation, with
  YELLOW reminders that the QA scope is narrow and owner MVP decisions remain
  open.
- **Agent boundaries:** QA and the advisor made no file, test, service, or Git
  changes during this demonstration.

## Latest Verified Application Revision

- **Tested revision:** `6887ab9` - Prevent content proposal selection recursion,
  following deterministic Content Review readiness at `438a01f`.
- **Current equivalent after consolidation:** `c569cbc`, following `12ea0ff`.
- **QA result:** PASS on the exact clean revision: 9 Avalonia QA tests, 11
  Content tests, an API Release build, and one connected real-model lifecycle.
- **Connected behavior:** the real local model generated an Emberbound creature;
  the UI persisted and reloaded it, displayed separate mechanical and balance
  results, recorded revision/approval/rejection decisions, preserved replacement
  linkage, and produced an approved export with provenance. The previous native
  stack overflow did not recur.
- **Evidence:** `artifacts/qa/connected-desktop-6887ab9/report.md`.
- **Boundary:** this is headless production-shaped UI evidence with isolated
  services. It does not claim manual launcher, interactive file-picker, or
  visual screenshot verification.

## Historical Clarification Dialog Verification

- **Revision:** `cb3e884` — Integrate focused Avalonia QA infrastructure.
- **QA result:** PASS on the exact revision: two Avalonia headless
  clarification-dialog tests and 26 schema-translation tests passed.
- **Evidence:**
  `artifacts/qa/clarification-dialog-integrated-cb3e884/report.md`.
- **Scope:** the dialog's question display, blank-answer refusal, accepted
  answer, cancellation, and same-request resume behavior; not whole-application
  accessibility or visual-regression approval.

## Latest Verified Desktop Shutdown Revision

- **Revision:** `09c4c4f` — Isolate native shutdown smoke API, following the
  asynchronous shutdown fix at `94c355b`.
- **QA result:** PASS: 5 headless QA checks and the App Release build passed. An
  isolated native process closed through the normal Avalonia lifetime, exited 0
  in 3.35 seconds, and left no process behind.
- **Behavior:** close housekeeping no longer blocks the UI thread; it has an
  eight-second limit and cannot prevent shutdown. The native smoke path can use
  a process-local API URI so QA never contacts user projects.
- **Evidence:** `artifacts/qa/desktop-shutdown-ds001-09c4c4f/report.md`.

## Latest Verified Content Persistence Revision

- **Revision:** `bf19ae5` - Add isolated content API verifier, including the
  Mongo persistence correction at `d70d7b4`.
- **QA result:** PASS on the exact clean revision. The focused persistence test,
  11 content tests, and 5 Avalonia QA tests passed; the API and verifier Release
  builds completed without warnings or errors.
- **Live isolation:** QA used tmpfs Mongo storage, a random database, and an
  ephemeral loopback API port. A nested proposal and approved decision were
  saved and reloaded successfully, then the disposable project, process, and
  container were removed. No serialization or unhandled-error markers appeared.
- **Safety:** the reusable .NET verifier refuses the normal development port
  5080, so it cannot accidentally exercise the user's regular project stack.
- **Evidence:** `artifacts/qa/mongo-content-proposal-bf19ae5/report.md`.

## Latest Verified Normal-Flow Stabilization

- **Local generation (`4e4dee8`):** the production Ollama prompt/parser with
  local `gemma3:12b` produced a complete Emberbound creature that passed
  deterministic schema validation. Evidence:
  `artifacts/qa/content-generation-live-4e4dee8/report.md`.
- **Validation and balance presentation (`e071693`):** Content Review now shows
  separate mechanical and advisory panels, including clear no-profile behavior
  without inventing a universal score. App build, 8 QA tests, 11 content tests,
  and default/minimum-size control-tree checks passed. Evidence:
  `artifacts/qa/content-review-presentation-e071693/report.md`.
- **Game Data entry creation (`0da828e`):** Add Selected Entry now derives all
  required starter fields from the active game-owned schema instead of creating
  an empty values object. App build, 5 Game Data tests, and 8 QA tests passed.
  Evidence: `artifacts/qa/schema-aware-game-data-0da828e/report.md`.
- **Decision lifecycle (`0656cb2`):** isolated live API/Mongo verification passed
  for revision request, version-2 replacement linkage, approval, rejection,
  reload, and approval-gated export provenance. Evidence:
  `artifacts/qa/content-decision-lifecycle-0656cb2/report.md`.
- **Current launcher (`66978af`):** normal startup detects stale desktop and API
  sources, uses bounded single-worker restore/build, supports update-only and
  desktop-only rebuilds, reuses current services, opens no GUI in update-only
  mode, and leaves no launcher-owned build worker. Evidence:
  `artifacts/qa/launcher-bounded-66978af/report.md`.

## Latest Verified Development Infrastructure

- **Revision:** `f2fc132` — Add owner notification and watchdog infrastructure.
- **QA result:** PASS on the exact clean revision. QA independently verified
  PowerShell parsing, project Stop-hook filtering, asynchronous playback-worker
  receipts for all three alert categories, ordinary-completion silence, watchdog
  suspected/confirmed interruption classification, terminal-state suppression,
  cooperative stop, and ignore rules.
- **Evidence:** `artifacts/qa/owner-notifications-f2fc132/report.md`.
- **Known limitation:** only the owner can trust a project-local Codex hook.
  Until `/hooks` (or Desktop Settings > Hooks) approves it, the coordinator must
  invoke the tested direct alert fallback before any marked owner-action stop.

## Latest Verified MVP Milestone

- **Revision:** `32c8f01` — Define two-system creature MVP contracts.
- **Advisor assessment:** GREEN. The contract work is game-owned, remains within
  the meta-schema boundary, and needs no new owner decision. The advisor's YELLOW
  risk—fixed system, balance, revision, and export inputs before implementation—is
  addressed by `docs/mvp-demonstration-contracts.md` and its two fixtures.
- **QA result:** PASS on the exact clean revision: 23 Release schema-validation
  tests passed. QA verified both schemas, valid/invalid/advisory fixture behavior,
  rule/profile traceability, required lifecycle/export fields, and mechanical
  distinction without universal creature, dice, or challenge-rating code.
- **Evidence:** `artifacts/qa/mvp-contracts-32c8f01/report.md`.

## Latest Verified MVP Evaluation Layer

- **Revision:** `5f0449c` — Add generic content balance evaluation service.
- **QA result:** PASS on the exact clean revision: 3 content-evaluation tests
  and 23 schema-validation regression tests passed.
- **Scope:** generic, game-owned entity/property profile evaluation; existing
  active-schema validation remains the source of hard mechanical violations,
  while numeric and explicitly requested collection-count signals remain advisory
  balance concerns. No creature, dice, CR, or cross-game formula was added.
- **Evidence:** `artifacts/qa/content-evaluation-5f0449c/report.md`.

## Latest Verified MVP Lifecycle Layer

- **Revision:** `24f67b1` — Add content proposal lifecycle persistence.
- **QA result:** PASS on the exact clean revision: 6 content lifecycle tests and
  a Release API build passed with no warnings/errors.
- **Scope:** proposal versions retain exact active-schema and Working Rule Set
  revisions; revision guidance/replacement linkage is required; decisions are
  append-only artifacts separate from canonical game data; Mongo/API endpoints
  remain project-scoped.
- **Evidence:** `artifacts/qa/content-lifecycle-24f67b1/report.md`.

## Latest Verified MVP Generation Layer

- **Revision:** `0fa44c9` — Add guarded content proposal generation.
- **QA result:** PASS: 8 content tests and 23 schema-validation regression tests
  passed. Model output is limited to a strict, reviewable candidate for the
  requested game-owned entity type; it never persists or applies game data.
- **Evidence:** `artifacts/qa/content-generation-0fa44c9/report.md`.

## Latest Verified MVP Review Layer

- **Revision:** `f2a8b97` — Add content proposal review workspace.
- **QA result:** PASS on the exact clean revision: 3 Avalonia headless tests,
  8 content tests, and 23 schema-validation regression tests passed.
- **Scope:** Content Review is a proposal-only workspace. It loads a project’s
  persisted proposal/decision history, prevents no-context actions, submits one
  bounded local-model candidate against the active schema and Working Rule Set,
  shows structural validation only for the matching active-schema revision, and
  records approval, rejection, or a guided revision request without changing
  canonical game data. The screen explicitly does not invent a universal balance
  score when a project has no game-specific balance profile.
- **Evidence:** `artifacts/qa/content-review-f2a8b97/report.md`.
- **Limit:** QA intentionally did not exercise live Ollama, API, Mongo, Docker,
  or user data. The existing deterministic service and fixture tests remain the
  evidence for those isolated domains until an end-to-end demonstration run.

## Latest Verified MVP Balance-Profile Layer

- **Revision:** `d75b24c` — Scope balance profile upserts to projects (following
  implementation at `e451968`).
- **QA result:** PASS after one corrected medium-severity isolation defect: API
  Release build passed and 9 content tests passed. The original QA pass also
  covered 3 headless UI tests and 23 schema-validation regressions.
- **Scope:** Profiles are project-owned, revisioned, and tied to an exact active
  schema revision and content type. They retain source Working Rule IDs, named
  signals, and references. Content Review applies only a matching profile and
  presents advisory concerns separately from mechanical violations; otherwise it
  assigns no universal score. The profile editor remains optional and expects
  explicit game-owned JSON data.
- **QA correction:** BP-001 found a global Mongo upsert key. `d75b24c` scopes
  the filter to both project and profile ID; QA independently retested it.
- **Evidence:** `artifacts/qa/balance-profile-e451968/report.md` and
  `artifacts/qa/balance-profile-bp001-d75b24c/report.md`.
- **Limit:** Mongo/API behavior was statically reviewed, not live-database
  exercised. This remains a final two-system demonstration check.

## Latest Verified MVP Export Layer

- **Revision:** `ac87891` — Add approved content JSON export.
- **QA result:** PASS: 11 content tests, 3 headless UI tests, and 23
  schema-validation regressions passed; the App Release build passed with no
  warnings or errors.
- **Scope:** Only a proposal whose latest decision is approval can export. The
  portable JSON artifact contains the candidate content plus game-schema,
  Working Rule Set, proposal-version, and designer-decision provenance. Export
  does not modify canonical game data.
- **Evidence:** `artifacts/qa/content-export-ac87891/report.md`.
- **Limit:** The interactive save picker/file-write path and live services were
  intentionally not exercised by isolated QA; final demonstration evidence must
  cover observable exported artifacts.

## Open Defects And Technical Limits

- No confirmed product defect is currently open.
- A user-running Debug `WorldSeed.App.exe` can lock Debug build output. QA uses
  isolated Release builds and must not terminate a user-owned process.
- Approved screenshot baselines and broader desktop accessibility/high-DPI review
  remain future QA work, not a failure of the verified dialog workflow.

## Approved MVP Direction And Remaining Decisions

The owner-approved MVP requires two mechanically different TTRPG demonstrations
of creature generation, system-specific mechanical validation and balance
evaluation, designer approve/reject/revise actions with preserved history, and
export. See decision 0014, `docs/mvp-plan.md`, and `qa/MVP_ACCEPTANCE.md`.

No implementation decision remains for the approved MVP. The owner reopened
completion until the normal user workflow, rather than only its individual
components and deterministic fixtures, has credible connected evidence.

## Advisor Review Of Approved Scope

- **Revision reviewed:** `4b082d3`.
- **Assessment:** GREEN. The creature vertical slice preserves the proposal's
  complete-workflow intent while keeping creature concepts and balance profiles
  game-specific.
- **Critical owner decisions:** none beyond explicit activation of autonomous
  development.
- **YELLOW implementation risks:** select and document the two systems, their
  balance-reference datasets, export fields/format, and revision comparison before
  the corresponding implementation milestones.

## Latest Advisor Stabilization Review

- **Revision reviewed:** `6887ab9` and its connected QA report.
- **Assessment:** GREEN. The connected Emberbound run closes the reopened
  functional-usability blocker, while the deterministic Emberbound and Quiet
  Horizons demonstration remains the required cross-system evidence.
- **Optional hardening:** manual launcher/file-picker visual verification, a
  fully API-activated balance-profile fixture path, and applying approved
  proposals to canonical Game Data are not approved MVP requirements.
- **Owner decisions:** none.

## Remaining Authorized Work

- Run one final manual desktop check from the normal launcher: generate and
  select a proposal, inspect mechanical and balance output, request a revision,
  approve or reject, reload the project, export the approved proposal, and close
  the app without a residual WorldSeed process.
- Further product expansion or optional hardening requires a new authorized
  task.

## Next Action Required

The owner must either restart the Codex desktop session so computer control can
be retried, or perform the final normal-launcher desktop check manually. Both
available computer-control surfaces currently fail before they can observe an
application window. Development should evaluate any observed WorldSeed failure
before the usable MVP is marked complete.
