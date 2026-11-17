# WorldSeed Project Status

## Approved MVP Status

**In progress.** On 2026-11-13 the owner approved decision 0015, replacing the
creature-focused demonstration as the active MVP finish line. The class MVP is
now the complete experienced-designer workflow from several loose files to a
substantially fleshed-out TTRPG with cohesive rules, expanded systems and
game-specific content, evaluation, preserved decisions, and organized readable
publication. The completed creature slice remains verified evidence for the
architecture; it is not described as the completed WorldSeed MVP.

Standalone testing and status commits were consolidated locally on 2026-11-07.
QA reports retain the original commit IDs they actually tested; rewriting commit
parents changed the equivalent IDs on `main`. The pre-consolidation history is
preserved at `backup/main-before-test-doc-squash-20261107`.

## Current Development Task

The owner approved generic source/rule/schema coverage work, superseding the
SOT-specific Skill-first checkpoint. Revision `ffa6ea5` adds advisory inventory
observations for uncited complete source passages, current draft rules without
Working Rule citations, and unresolved current content-proposal schema gaps.
The planner can recommend review of these observations. Fully cited narrative
rules require no schema definition. Existing persisted observation enum values
are preserved. Developer tests passed 16/16, including armor and roleplay prose.
The updated local SOT inventory surfaced 59 uncited passages and 7 reported
schema gaps, including the original Armor Set/Armor Value source statement.
Advisor review is GREEN with a nonblocking observation-volume risk.
Independent QA passed the exact `ffa6ea5` revision with 16 ProjectPlanning tests,
5 headless Inventory/Planning tests, and clean API/App Release builds. Evidence:
`artifacts/qa/20261117-generic-coverage/report.md`.

Owner clarification: SOT is a development stop-gap, not the final presentation
project. The final comparison demonstration will use existing TTRPG sample
documents supplied separately by the owner. Do not create additional SRD copies.
The deadline is Week 9; prioritize functional completion before polish.

The owner authorized the proposed SOT plan revision and feature-focused work,
superseding the earlier requirement to stop this cycle for refreshed usage.
Revision `df1ad32` added a linked-plan revision command; `e15c420` added a
reviewable existing-project content command. The local model saved plan version 2
with canonical content before evaluation. It remains unapproved. Two generated
unapproved samples exposed a concrete project-schema coverage gap: Armor Class
represents only weight category, and Skill only cooldown duration. Chainmail
reported missing protection/description; Guard Break reported missing AP cost,
activation requirement, and effect description. No sample became canonical.
These are partial candidates, not evidence of a complete game.
Plan v2 was generated as a linked replacement after recording the owner's
revision guidance. Its content recommendations include Action Point, Armor Class,
and Skill. The Action Point modeling choice still needs designer review.
The source audit found that Working Rules b003-r002/b003-r003/b004-r002 support
skill AP cost and activation requirements, but armor protection details in the
imported Armor source are absent from the current Working Rules. Recover that
source coverage before proposing protection fields. Advisor: YELLOW; prepare
rule-backed project schema enrichment, with explicit review/activation.
QA passed at `e15c420`: 24 Content tests, 11 ProjectPlanning tests, 3 headless
Development Plan tests, and the EndToEnd Release build. Static checks confirmed
both commands preserve proposal-only authority. Evidence:
`artifacts/qa/20261116-plan-demo-revision/report.md`.
Owner review checkpoint: approve the revised content-first direction while
deferring Action Point catalog generation. Recommend Skill as the first useful
content slice, with rule-backed schema enrichment before canonical application;
Armor needs source-to-Working-Rule coverage recovery first.

Milestones 1 through 5 are complete. The three-iteration Milestone 3 cycle added the
persisted, designer-reviewable multi-type expansion run at revision `73f8f68`
(`584b70d`, `c178ca8`, `73f8f68`). Runs select arbitrary active-schema-owned
content types, quantities, and explicit earlier-stage dependencies while binding
the exact active-schema and Working Rule Set revisions. Each generated item
remains an independent proposal. A downstream stage cannot generate until every
upstream slot has been explicitly approved and applied to canonical Game Data.
No run operation approves or applies content automatically.

Independent QA passed 21 Content tests, 8 focused Avalonia headless tests, both
Release builds, static genericity/authority review, and an isolated tmpfs
Mongo/random-port API lifecycle with 8/8 checks. It proved persistence, reload,
idempotent linkage, pre-application dependency refusal, post-application release,
and project-delete cleanup. Evidence:
`artifacts/qa/20261115-multitype-expansion-cycle/report.md`. The milestone advisor
assessed the result GREEN and authorized progression to Milestone 4 Evaluation
Assistance without a new owner design decision.

The two-iteration Evaluation Assistance cycle is independently verified at
revision `d0ab3f3` (`cb6edde`, `d0ab3f3`). Local models can propose either
game-specific deterministic validation or advisory balance-profile coverage,
but every proposal must cite existing Working Rules and real properties from the
exact active schema/rule revisions. Proposals and decisions persist separately;
creation changes nothing. Only the exact approved version can prefill the existing
Schema workflow or save a versioned, project/schema-specific balance profile.
QA passed 24 Content tests, 8 focused Avalonia headless tests, both Release builds,
static authority/genericity review, and 9/9 isolated API/Mongo lifecycle checks.
Evidence: `artifacts/qa/20261115-evaluation-assistance/report.md`. The milestone
advisor assessed the result GREEN and authorized progression to Complete Game
Publication. Live Sword of Truth proposal quality remains final-demonstration
evidence rather than a blocker.

Milestone 6 is active. A three-iteration stabilization cycle at `fed57c9`
(`fd7a8ae`, `8eed959`, `fed57c9`) added a loopback-only existing-project plan
demonstration command, required every recommendation category to be supported by
the kind of inventory observation it cites, and added at most two corrective
local-model retries with the exact validation error. QA passed 11 ProjectPlanning
tests, 3 focused headless Development Plan tests, and EndToEnd/API/App Release
builds. Evidence: `artifacts/qa/20261116-development-plan-evidence/report.md`.

The real SOT inventory now records 6 source documents, 32 Working Rules, 6
active-schema content types, 0 canonical entries, and 12 observations covering
empty content and missing evaluation. The post-fix local `gemma3:12b` plan is
factually supported but proposes only evaluation work despite acknowledging the
empty types. It remains unapproved and executed nothing. The advisor assessed
this YELLOW and recommends that the owner request revision before any expansion
is authorized. The older pre-fix plan also remains unapproved immutable history;
it must not be treated as current or executable.

The one-iteration Complete Game Publication cycle is independently verified at
revision `89fefcd`. Immutable snapshots now incorporate only the latest approved
game-specific balance profile for the exact active-schema revision. Broken rule,
content-type, or property provenance is refused. Ready editions show friendly
messages, ranges, named references, and rationales without internal IDs, while
the snapshot and edition manifests retain exact profile, signal, and Working
Rule provenance. Projects without evaluation profiles remain supported. QA
passed 13 Publication tests, 4 focused Avalonia headless tests, and both Release
builds. Evidence: `artifacts/qa/20261115-evaluation-publications/report.md`.
The milestone advisor assessed the result GREEN and directed work to Milestone 6
Full Demonstration And Stabilization. The lack of a separate live API/Mongo run
for this newest wiring is final-demonstration coverage, not missing functionality.

The inventory cycle used four implementation iterations followed by one
consolidated QA pass. QA passed 45 focused/regression tests, Release API/App
builds, static ownership/no-model inspection, and an isolated tmpfs Mongo/API
lifecycle proving immutable r1/r2 history, latest selection, factual observation
changes, reload, and project-delete cleanup. Evidence:
`artifacts/qa/20261113-project-inventory/report.md`.

The Development Plan cycle used five implementation iterations followed by one
consolidated QA pass. QA passed 6 planning tests, 4 focused headless Avalonia
tests, both Release builds, static boundary review, and an isolated tmpfs
Mongo/API lifecycle proving immutable plan v1/v2 lineage, separate plan/item
decisions, reload, and project-delete cleanup. Evidence:
`artifacts/qa/20261113-development-plan-cycle/report.md`. The milestone advisor
assessed the result GREEN and recommended Accepted Recommendation -> Expansion
Request as the next implementation priority; no owner decision is currently
required.

The three-iteration Expansion Request cycle is complete and independently
verified at revision `9b7c159` (core boundary `7aac7d9`, desktop handoff
`4c52321`). An accepted
recommendation can now become one immutable Expansion Request bound to the exact
inventory, plan, recommendation, and acceptance decision. The API refuses stale
inventory/plan lineage, rejected or revision-pending plans, and any recommendation
whose latest decision is not Accepted. It creates a traceable project source note
that explicitly treats the inventory as planning evidence rather than approved
mechanics. The desktop exposes Start/Open Expansion only after acceptance, opens
the generated traceable source on the Sources tab, and the drafting pipeline
supplies an expansion-specific instruction while deterministically refusing any
first-turn rule draft instead of clarification. QA passed 8 ProjectPlanning
tests, 6 DesignSessions tests, 3 focused headless Avalonia tests, both Release
builds, and an isolated tmpfs Mongo/API lifecycle covering acceptance gating,
idempotency, provenance/source reload, stale/rejected/nonaccepted refusal paths,
and project-delete cleanup. Evidence:
`artifacts/qa/20261113-expansion-request-handoff/report.md`.

A subsequent three-iteration progress cycle is verified at revision `d1f9127`
(`2a55859`, `a7adb15`, `d1f9127`). Expansion state is derived from persisted
sessions and Working Rule provenance as Ready For Clarification, Clarification
In Progress, Draft Ready, or Working Rules Incorporated; the desktop refreshes
and navigates accordingly. QA passed 18 focused tests, both Release builds, and
an isolated tmpfs Mongo/API Ready -> Clarification -> Draft -> Incorporated
lifecycle with exact session, draft, Working Rule revision/ID evidence and
cleanup. Evidence: `artifacts/qa/20261114-expansion-progress/report.md`.
The milestone advisor assessed this checkpoint GREEN, with a YELLOW requirement
gap: expansion still needs an explicit designer-controlled handoff from its
incorporated Working Rules into schema proposal/review/activation, including a
valid "no schema change required" outcome, before milestone 3 becomes primary.

The four-iteration Expansion-to-Schema cycle is complete at revision `ad30f40`
(`087cc42`, `e428996`, `018a240`, `ad30f40`). Designers can send the exact
expansion-derived Working Rules into the existing Schema queue without manually
regrouping them, or record a rationale-backed No Schema Change Required decision.
Schema proposal and activation remain separate designer actions, and progress is
derived through Schema Work Requested, Proposal Ready, and Activated. QA's first
isolated lifecycle found formatting-sensitive schema comparison defect ES-001;
the semantic-JSON correction passed 9 regressions, a clean API build, and 8/8
isolated API/Mongo lifecycle checks including actual activation and mismatch
refusal. Evidence: `artifacts/qa/20261115-expansion-schema-handoff/report.md` and
`artifacts/qa/20261115-expansion-schema-handoff-es001-retest/report.md`.
The advisor assessed milestone 2 GREEN and complete. A later Sword of Truth live
model/owner demonstration remains milestone 6 evidence, not a blocker to
multi-type content expansion.

The previously described post-creature work is now recognized as foundation for
the expanded MVP. Its first complete sequence is independently verified: bounded context-backed content
generation (`36f638d`), explicit promotion of approved proposals into canonical
Game Data (`776fbba`), and deterministic human-readable publication (`a05f827`).
The Publication workspace creates immutable Markdown snapshots and provenance
manifests from the exact active-schema-bound Working Rule Set revision and valid
canonical Game Data. Draft, rejected, and unapplied proposals are excluded.
QA passed all 124 tests, focused service and headless UI checks, both Release
builds, and an isolated live API/Mongo exercise proving two immutable snapshots,
distinct fingerprints after an upstream data change, and project-delete cleanup.
Evidence is retained at
`artifacts/qa/20261110-publication-snapshots/report.md`.

Revision `7490b9c`, followed by section-ID correction `585f4d0`, adds a
designer-reviewed Publication Edition layer bound to one immutable snapshot.
Designers can revise title/subtitle, top-level section visibility/order, and
game-owned content-type order without editing mechanics. Draft and Ready
revisions are append-only; only Ready Markdown is export-eligible, and every
edition manifest retains snapshot, rule, schema, fingerprint, revision, and
state provenance. Revision `ae4f7e6` also makes suspected watchdog inactivity
silent and records the owner-approved Codex usage gate. QA passed all 127 tests,
focused Publication and headless UI checks, both Release builds, an isolated
live r1/r2/r3 Mongo/API history with stale-update refusal and cleanup, and silent
notification checks. Evidence is retained at
`artifacts/qa/20261110-publication-editions-final/report.md`.

Revision `66abd7b` adds immutable structured publication units for individual
rules, value types, content types, canonical entries, and relationships. Each
unit retains a stable ID, parent where applicable, deterministic body, and exact
canonical source references. New snapshots render Markdown and editions from
these units instead of reconstructing structure from presentation headings;
legacy snapshots without units retain the previous compatibility path. QA passed
all 129 tests, six focused Publication tests, two headless Publication workspace
tests, both Release builds, and an isolated API/Mongo round trip covering every
unit kind, legacy documents, manifests, edition fallback, and cleanup. Evidence
is retained at `artifacts/qa/20261110-publication-units/report.md`.

Revision `b1c50fd` removes technical Rule ID lines from newly generated
player-facing snapshot and edition Markdown while retaining the exact working-rule
mapping in structured publication units and provenance manifests. QA passed all
six Publication service tests and both headless Publication workspace tests in a
usage-bounded run. Evidence is retained at
`artifacts/qa/20261110-player-publication-text/report.md`.

Revision `0e56e4d` establishes the safe core contract for section-scoped
editorial proposals. Targets are derived from one immutable snapshot's structured
publication units; a proposal must cite every unit in its selected section
exactly once. Revisions preserve project, snapshot, section, series, and prior
proposal linkage, while approve/reject/revision-request decisions remain
append-only and separate from canonical mechanics. QA passed all eight focused
Publication tests and found no contract defect. Evidence is retained at
`artifacts/qa/20261110-editorial-contract/report.md`.

Three subsequent verified iterations make that contract operational. Revision
`5790db5` adds project-scoped Mongo/API persistence, indexes, validation, listing,
and deletion cleanup for editorial proposals and decisions. Revision `fa64dbb`
adds bounded local-model drafting for one immutable section: the model receives
at most 24 exact source units and can return only one proposed-Markdown field;
WorldSeed retains all provenance and revision authority. Revision `4a06e4a`,
corrected at `6363e26`, adds Section Editorial Review to the Publication
workspace with target selection, guidance, proposal history, and persisted
approve/reject/revision decisions. QA found and verified fixes for model-selection
button refresh and API rejection of forged blank-guidance revision decisions.
Focused evidence is retained under `artifacts/qa/20261110-editorial-persistence`,
`artifacts/qa/20261110-editorial-generation`, and
`artifacts/qa/20261110-editorial-workspace-retest`.

Revision `9f47210` closes the publication loop for approved prose. A new edition
revision can compose only the latest explicitly approved editorial proposal for
each visible section; sections without approved prose retain deterministic
snapshot text. Rejected, missing, mismatched, duplicate, omitted-section, and
legacy-unitless selections are refused. Edition records and manifests preserve
the exact editorial proposal revision, approval decision, source units, snapshot,
rules, schema, and Game Data provenance. QA passed 11 Publication tests, 3
headless Publication workspace tests, and clean API/App Release builds. Evidence
is retained at `artifacts/qa/20261111-editorial-composition/report.md`.

Revision `998779a` separates player-facing Ready Markdown from technical
publication records. Reader exports omit internal IDs, provenance headers, and
schema type labels while manifests and structured units retain exact technical
traceability. Revision `ebf71be` then makes the editorial safety boundary
visible: the selected section's immutable source material appears separately
from proposed reader prose, proposal selection synchronizes its source section,
and missing legacy source references fail visibly instead of crashing. QA passed
11 Publication tests, 3 headless Publication workspace tests, and a clean App
Release build for the latest revision. Evidence is retained at
`artifacts/qa/20261111-reader-publication/report.md` and
`artifacts/qa/20261111-editorial-source-review/report.md`.

Revision `329b1fb` closes the normal-launcher publication review finding by
distinguishing current editable snapshots from legacy snapshots that require a
new current snapshot. The same desktop pass confirmed that Publication is
functional but visually dense; broader layout polish remains optional. Revisions
`3617be5`, `8426fcc`, and `9cfab98` then add and harden a bounded sequential
Content Expansion Queue. It generates 2-5 entries one at a time, retains each as
an independently validated and reviewable proposal, supports stop-after-current,
refuses stale project/schema/rule results, and never approves or applies content
automatically. Revision `1900df5` lets each completed queue row open its exact
proposal for review. QA passed 8 headless Content Review tests, 17 Content tests,
and a clean App build. An isolated connected smoke with `gemma3:12b`, a real API,
and tmpfs Mongo proved two sequential calls, distinct persistence/reload,
prior-ID exclusion, independent mechanical validation, no decisions or
applications, and complete cleanup. Evidence is retained under
`artifacts/qa/20261111-content-expansion-queue`,
`artifacts/qa/20261111-content-expansion-connected`, and
`artifacts/qa/20261111-expansion-queue-navigation`.

Advisor review selected rule-conforming content expansion before publication.
Revision
`36f638d` implements the first bounded increment: Content Review can suggest one
new entry from current-schema canonical examples and applicable designer
decisions, record the exact context IDs, refuse duplicate IDs, and display
advisory similarity warnings without mutating canonical Game Data. Independent
QA passed 117 tests, focused headless UI and persistence checks, both Release
builds, and one isolated `gemma3:12b` observation that produced a distinct
`stone-shambler`. Evidence is retained at
`artifacts/qa/20261110-context-backed-generation/report.md`.

Revision `776fbba` resolves that question. Approval is now an eligibility
condition, while a separate **Apply Approved To Game Data** action performs
current-schema revalidation and records a durable Pending/Applied application
event. It never overwrites a duplicate ID, requires a second confirmation for
visible advisory warnings, and recovers idempotently if canonical data was saved
before event finalization. QA passed 121 tests and an isolated live API/Mongo
exercise covering unapproved refusal, one-entry application, repeat idempotency,
duplicate conflict, and project cleanup. Evidence is retained at
`artifacts/qa/20261110-content-application/report.md`.

The owner-authorized creature MVP implementation, isolated connected QA, and
normal-launcher smoke workflow are complete. Tested revision `6887ab9` (current equivalent `c569cbc`) connected the
actual Content Review controls and production generation path to local
`gemma3:12b`, an isolated Docker API and
Mongo database, persistence/reload, mechanical validation, game-specific
balance evaluation, revision history, approval/rejection, and approval-gated
export. QA independently passed that flow without reproducing the corrected
proposal-list stack overflow. On 2026-11-10 the owner completed the normal-launcher
smoke workflow: the corrected chainmail request generated without a value-shape
error, routed an unrepresentable armor-value concept to Schema, and exported
successfully. The owner marked the run passed while identifying proposal lineage
after schema changes as the remaining usability problem.

Manual smoke testing then exposed unclear recovery from mechanical validation
failures and an incomplete revision workflow. Revision `0202da6` now lists
allowed enumeration IDs, explains how to configure a missing game-specific
balance profile, and turns a recorded revision request into an explicit
Generate Requested Revision workflow with replacement linkage. Independent QA
passed the focused headless workflow, blank-guidance and new-proposal checks,
and 34 content/schema regressions. Evidence is retained at
`artifacts/qa/20261108-content-revision-guidance/report.md`. Owner verification
through a rebuilt normal-launcher client remains the next manual check.

The owner's chainmail smoke test then identified a value-shape defect: the local
model inferred the correct `heavy` category but copied its schema descriptor into
`candidate.values`. Revision `0550b71` replaces the broad schema prompt with a
selected-type runtime-value contract and clarifies descriptor-shaped validation
errors. QA passed 36 focused regressions, and a non-persisting live
`gemma3:12b` fixture run returned `"weightCategory": "heavy"` with zero
mechanical violations. Evidence is retained at
`artifacts/qa/20261108-runtime-value-contract/report.md`.

Revision `3241c82` closes the remaining silent-omission problem exposed by that
test. Content generation now records advisory schema gaps separately from the
candidate, shows them in Content Review, points the designer to the existing
Schema workspace, retains them through proposal versions and Mongo persistence,
and includes them as non-canonical export provenance. QA passed 47 automated
checks and an API build; a non-persisting live `gemma3:12b` SOT fixture returned
`"weightCategory": "heavy"` plus a separate `armorValue` schema gap with zero
mechanical violations. Evidence is retained at
`artifacts/qa/20261110-content-schema-gaps/report.md`.

Revision `29719c4` closes the proposal-lineage problem from the successful manual
smoke. WorldSeed retains the original designer request, labels proposals tied to
older schema revisions, and can re-evaluate one against the current schema as a
linked replacement. Only the current lineage tip appears in the proposal list;
older versions and decisions remain visible in its history. QA passed all 114
tests, six focused headless UI checks, persistence compatibility, and both
Release builds. Evidence is retained at
`artifacts/qa/20261110-proposal-lineage/report.md`.

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

- **Tested revision:** `3be5ce2` - Preserve schema transition history, following
  deterministic review at `6ecb209` and explicit compatible activation at
  `88fbeec`.
- **QA result:** PASS. Ten Game Data tests, two headless transition-workspace
  tests, Release API/App builds, and an isolated Mongo/API exercise passed. The
  connected exercise preserved authored data across a compatible optional-field
  update, retained exact immutable before/result snapshots for revisions 1 and
  2, and removed transition history with project deletion. A first apparent
  one-record failure was superseded after raw JSON proved it was a disposable
  PowerShell array-parsing error rather than a product defect.
- **Behavior:** the first desktop action reviews a saved proposal without
  mutation; a compatible result requires a second explicit activation action;
  changes requiring data work remain blocked with affected paths. Successful
  activations retain an immutable transition record and server-side review is
  repeated before writes.
- **Evidence:** `artifacts/qa/20261112-schema-transition-review/report.md`,
  `artifacts/qa/20261112-schema-transition-activation/report.md`, and
  `artifacts/qa/20261112-schema-transition-history/report.md`.

- **Tested revision:** `ed2ff8e` - Constrain local schema translation output,
  following scoped queued-rule discovery at `47141b3`.
- **QA result:** PASS. All 26 Schema Translation tests and the App/probe Release
  builds passed. An isolated connected `gemma3:12b` run then completed typed
  source -> Working Rules -> bounded Habitat structure discovery -> saved schema
  proposal. Every live translation request carried the native structured-output
  schema; the saved definition-source mapping used only the one rule selected by
  the discovered plan; no active schema was created.
- **Evidence:**
  `artifacts/qa/20261112-schema-structured-output-connected/report.md`.

- **Tested revision:** `47141b3` - Build schema structures from queued rules.
- **QA result:** PASS on the exact revision: 2 focused Avalonia tests, all 26
  Schema Translation tests, and a clean App Release build. The bounded run left
  zero MSBuild or compiler-server workers.
- **Behavior:** designers can queue several related Working Rules and discover
  schema structures using only that selected scope. The existing sequential,
  traceable proposal flow handles each discovered structure; clarification
  resumes the same plan; only successfully handled rules leave the queue; saved
  proposals remain editable and non-canonical; activation remains explicit.
- **Evidence:** `artifacts/qa/20261112-scoped-schema-discovery/report.md`.

- **Tested revision:** `1463eda` - Preserve typed design ideas as sources.
- **QA result:** PASS on the exact revision: 4 Source Ingestion tests, 2
  Avalonia headless tests, and a clean App Release build.
- **Behavior:** the Sources workspace can preserve a designer's incomplete
  mechanic, system, or concept as exact original source text. The idea is saved
  in the project database, selected for inspection, and enters the existing
  clarification/rule-drafting flow only when the designer starts it. It does not
  call a model, create rules, or change schema merely by being preserved.
- **Evidence:** `artifacts/qa/20261111-design-idea-source/report.md`.
- **Connected evidence:** revision `bbc9e83` passed the bounded direct-DLL
  `gemma3:12b` reliability suite after an environment cleanup: complete
  procedural and facilitator-driven ideas became validated, traceable Working
  Rule Sets, while the deliberately incomplete idea produced a focused
  clarification. Nine LLM adapter tests and the Release tool build also passed.
  Evidence: `artifacts/qa/20261111-design-idea-connected-retest/report.md`.
- **Resource incident:** an interrupted unbounded SDK build and a `dotnet run`
  preflight left 310 orphaned MSBuild workers consuming about 10.6 GB. Their
  parent wrappers no longer existed; development removed only those exact
  orphan workers. Connected QA now requires bounded build-then-direct-DLL
  execution, which left zero MSBuild workers. Ollama failures now retain bounded
  structured local-runtime diagnostics.

- **Tested revision:** `1900df5` - Open proposals from expansion queue,
  following queue implementation and concurrency corrections through `9cfab98`.
- **QA result:** PASS: 8 Content Review headless tests, 17 Content tests, clean
  App Release builds, and an isolated connected two-entry `gemma3:12b` run with
  real API and tmpfs Mongo.
- **Behavior:** a bounded 2-5 item queue generates strictly sequential,
  separately persisted proposals; later prompts exclude earlier successful IDs;
  stop, workspace replacement, and stale-result UI paths are safe; completed
  rows open their exact proposal; approval and application remain individual.
- **Evidence:** `artifacts/qa/20261111-content-expansion-queue/report.md`,
  `artifacts/qa/20261111-content-expansion-connected/report.md`, and
  `artifacts/qa/20261111-expansion-queue-navigation/report.md`.

- **Tested revision:** `ebf71be` - Show exact sources during editorial review,
  following reader-facing export cleanup at `998779a`.
- **QA result:** PASS: 11 focused Publication tests, 3 headless Publication UI
  tests, and a clean App Release build.
- **Behavior:** designers compare immutable section sources with separately
  labeled proposed reader prose; proposal selection follows its source section,
  and missing legacy source units show an explicit unavailable state.
- **Evidence:** `artifacts/qa/20261111-editorial-source-review/report.md` and
  `artifacts/qa/20261111-reader-publication/report.md`.

- **Tested revision:** `9f47210` - Compose approved editorial sections.
- **QA result:** PASS: 11 focused Publication tests, 3 headless Publication UI
  tests, and clean API/App Release builds.
- **Behavior:** approved section prose is incorporated into a new immutable
  edition revision; every unapproved section uses deterministic fallback text,
  and exact editorial/source/canonical provenance remains exportable.
- **Evidence:** `artifacts/qa/20261111-editorial-composition/report.md`.

- **Tested revision:** `6363e26` - Validate editorial review interactions,
  following the Publication workspace implementation at `4a06e4a`.
- **QA result:** PASS after two corrected medium defects: 10 Publication tests,
  3 headless Publication workspace tests, and clean API/App Release builds.
- **Behavior:** designers can generate one source-bound section draft with a
  selected local model, inspect persisted history, and approve, reject, or
  request revision. Model selection immediately updates availability, and the
  API revalidates decision guidance before persistence.
- **Evidence:** `artifacts/qa/20261110-editorial-workspace-retest/report.md`.

- **Tested revision:** `fa64dbb` - Generate source-bound editorial proposals.
- **QA result:** PASS: 10 focused Publication tests and independent prompt,
  parsing, limit, provenance, and mutation-boundary inspection.
- **Behavior:** a local model receives one immutable section and its exact units;
  output is restricted to bounded proposed Markdown and remains non-canonical.
- **Evidence:** `artifacts/qa/20261110-editorial-generation/report.md`.

- **Tested revision:** `5790db5` - Persist publication editorial reviews.
- **QA result:** PASS: 8 Publication tests, 4 Mongo serialization tests, a clean
  API Release build, and static endpoint/store inspection.
- **Behavior:** proposal and decision history is project-scoped, source-validated,
  revision-specific, listed through the API, and removed with its project.
- **Evidence:** `artifacts/qa/20261110-editorial-persistence/report.md`.

- **Tested revision:** `0e56e4d` - Define section editorial proposal contract.
- **QA result:** PASS: 8 focused Publication tests plus independent contract
  inspection. UI and live persistence were intentionally outside this bounded
  foundation increment.
- **Behavior:** editorial work is limited to one immutable snapshot section with
  exact source-unit coverage, append-only revision lineage, and explicit designer
  decisions. It cannot mutate canonical rules, schema, or Game Data.
- **Evidence:** `artifacts/qa/20261110-editorial-contract/report.md`.

- **Tested revision:** `b1c50fd` - Hide rule IDs from publication text.
- **QA result:** PASS: 6 focused Publication tests and 2 headless Publication UI
  tests. The full suite and live services were intentionally excluded from this
  small usage-bounded correction.
- **Behavior:** new player-facing snapshot and edition Markdown omit technical
  Rule ID lines; source-unit and manifest provenance still identify each working
  rule. Existing immutable snapshots are not rewritten.
- **Evidence:** `artifacts/qa/20261110-player-publication-text/report.md`.

- **Tested revision:** `66abd7b` - Add stable publication source units.
- **QA result:** PASS: 129 solution tests, 6 focused Publication tests, 2
  headless Publication UI tests, clean App/API Release builds, and isolated live
  Mongo/API persistence and legacy-compatibility verification.
- **Behavior:** immutable snapshots now carry stable source-bound units for
  rules, value types, content types, canonical entries, and relationships.
  Markdown and editions use those units directly; existing snapshots without
  units remain compatible.
- **Evidence:** `artifacts/qa/20261110-publication-units/report.md`.

- **Tested revision:** `ae4f7e6` - Guard autonomous usage and stop-only alerts,
  including Publication Editions from `7490b9c` and normalized section IDs from
  `585f4d0`.
- **QA result:** PASS: 127 solution tests, 4 Publication tests, 2 headless
  Publication UI tests, clean App/API Release builds, and isolated live Mongo/API
  edition history and deletion cleanup.
- **Behavior:** edition r1/r2/r3 history is immutable and snapshot-bound; stale
  revision writes return conflict; Ready state gates Markdown export; suspected
  inactivity and ordinary completion queue no audio. New autonomous iterations
  require reliable five-hour and weekly usage values above 5% and never redeem
  resets or spend promotional/purchased credits without explicit authorization.
- **Evidence:** `artifacts/qa/20261110-publication-editions-final/report.md`.

- **Tested revision:** `a05f827` - Publish canonical project snapshots as
  Markdown.
- **QA result:** PASS on the exact revision: 124 solution tests, two Publication
  service tests, a focused headless Avalonia Publication check, clean API and App
  Release builds, and an isolated live API/Mongo exercise.
- **Behavior:** publication uses the exact active-schema-bound Working Rule Set
  and mechanically valid canonical Game Data; it creates append-only Markdown
  snapshots and machine-readable provenance manifests without an LLM. Later
  canonical changes create a new fingerprint and snapshot without altering the
  old one, and project deletion removes publication history.
- **Evidence:** `artifacts/qa/20261110-publication-snapshots/report.md`.

- **Tested revision:** `29719c4` - Re-evaluate proposal lineages after schema
  changes.
- **QA result:** PASS on the exact clean revision: 114 solution tests, six
  focused headless Avalonia checks, five lifecycle/export checks, two Mongo
  compatibility checks, and clean API and App Release builds.
- **Behavior:** original designer requests survive persistence and export;
  historical-schema proposals receive explicit unavailable evaluation state;
  re-evaluation produces a linked current-schema replacement while the proposal
  list shows one current lineage tip and preserves earlier versions in history.
- **Evidence:** `artifacts/qa/20261110-proposal-lineage/report.md`.
- **Manual evidence:** the owner passed the normal-launcher chainmail and export
  workflow immediately before this correction and accepted schema routing for
  an unrepresentable concept.

- **Tested revision:** `3241c82` - Report content schema coverage gaps.
- **QA result:** PASS on the exact revision with only this accumulated status
  file dirty: 13 Content tests, 23 SchemaValidation tests, 2 Mongo persistence
  tests, 9 headless UI tests, an API Release build, and one non-persisting live
  `gemma3:12b` SOT fixture probe.
- **Behavior:** representable candidate values remain schema-conformant;
  unsupported requested concepts are explicit advisory gaps rather than invented
  fields or silent omissions. Reported, no-gap, and legacy/unreported states are
  distinct. Gaps survive persistence, revision context, and approved export
  provenance without modifying canonical schema or game data.
- **Evidence:** `artifacts/qa/20261110-content-schema-gaps/report.md`.
- **Isolation:** QA used fixture data only and did not access the normal launcher,
  port 5080, user projects, the development API/Mongo stack, or Docker volumes.

- **Tested revision:** `0550b71` - Distinguish schema types from content values.
- **QA result:** PASS on the exact revision with only the accumulated status file
  dirty: 12 Content tests, 23 SchemaValidation tests, one focused Content Review
  test, and one non-persisting live `gemma3:12b` fixture probe.
- **Behavior:** content prompts describe runtime property values rather than
  exposing the entire schema as an output template. Enumeration fields name
  their exact string entry IDs and show a correctly typed example. Descriptor
  objects remain invalid and now receive explicit correction guidance.
- **Evidence:** `artifacts/qa/20261108-runtime-value-contract/report.md`.
- **Isolation:** QA used fixture schema/rules only and did not access the normal
  app, API, Mongo, Docker volumes, port 5080, or user project data.

- **Tested revision:** `0202da6` - Guide content proposal revisions.
- **QA result:** PASS on the exact clean revision: one tracked Content Review
  headless test, one disposable headless UI probe, 11 Content tests, and 23
  SchemaValidation tests.
- **Behavior:** mechanical enumeration errors show allowed IDs; missing balance
  profiles point to the profile editor without inventing a universal score;
  revision guidance becomes a visible Designer Request; requested regeneration
  creates and links a replacement; blank guidance is refused; Start New Proposal
  resets the workflow.
- **Evidence:**
  `artifacts/qa/20261108-content-revision-guidance/report.md`.
- **Boundary:** no live model/API/Docker/normal-launcher path was used for this
  focused correction. An existing user-owned app process was observed and left
  untouched.

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

## Latest Verified Complete Publication Layer

- **Revision:** `89fefcd` — Include evaluation context in publications.
- **QA result:** PASS on the exact revision: 13 Publication tests, 4 focused
  Avalonia headless Publication tests, and API/App Release builds.
- **Scope:** immutable snapshots combine canonical Working Rules, the active
  schema, applied Game Data, and current game-specific evaluation references.
  Ready Markdown remains reader-facing; manifests preserve technical provenance.
- **Evidence:** `artifacts/qa/20261115-evaluation-publications/report.md`.
- **Advisor:** GREEN. Milestone 5 is functionally complete; proceed to the final
  connected demonstrations rather than adding another publication subsystem.

## Open Defects And Technical Limits

- No confirmed product defect is currently open.
- Owner-operated manual testing of the Project Inventory, Development Plan,
  coordinated expansion, and schema-handoff screens is deferred to the testing
  backlog. These areas retain automated evidence but must not be described as
  owner-verified until that later hands-on pass is completed.
- The environment still cannot retrieve authoritative Codex allowance
  percentages automatically. The owner reported 100% five-hour and 35% weekly
  remaining before the `89fefcd` cycle on 2026-11-16. A later autonomous
  iteration requires a supported reading or a new explicit owner report.
- QA observed 47 residual `dotnet` processes that predated the latest bounded
  test run. Current low-worker commands did not increase the count. Their command
  lines could not be read without elevated Windows process inspection, so they
  were not terminated while ownership remained uncertain.
- On 2026-11-10 both supported Windows computer-control surfaces failed before
  listing windows with `failed to start Node runtime: The system cannot find the
  path specified. (os error 3)`, including after a runtime reset. This is an
  environment/tooling blocker to automated normal-launcher visual evidence, not
  a reproduced WorldSeed defect.
- A user-running Debug `WorldSeed.App.exe` can lock Debug build output. QA uses
  isolated Release builds and must not terminate a user-owned process.
- Approved screenshot baselines and broader desktop accessibility/high-DPI review
  remain future QA work, not a failure of the verified dialog workflow.

## Approved MVP Direction And Remaining Decisions

The owner-approved class MVP is the complete experienced-designer workflow in
decision 0015, `docs/mvp-plan.md`, and `qa/MVP_ACCEPTANCE.md`. It begins with
several loose files and ends with a substantially fleshed-out, evaluated, and
human-readable game. The prior creature demonstration satisfies a subset of this
contract and remains evidence rather than the finish line.

No owner decision blocks the first milestone. The owner deliberately left
“fully fleshed out” as a product-sufficiency judgment rather than a universal
numeric threshold; final acceptance therefore includes an owner-observed Sword
of Truth demonstration.

## Advisor Review Of Expanded MVP Scope

- **Date reviewed:** 2026-11-13.
- **Assessment:** GREEN. Decision 0015 expands the demonstration standard while
  preserving the original capstone architecture, game-agnostic boundary, human
  authority, and system-specific evaluation.
- **YELLOW guidance adopted:** project planning will surface minimum evaluation
  coverage early; dependencies and publication links remain bounded to explicit
  project-owned evidence; beginner polish and universal planning/retrieval
  engines remain deferred.
- **Owner decisions:** none required before milestone 1.

## Historical Advisor Review Of Creature Slice

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

## Latest Advisor Schema-Coverage Review

- **Revision reviewed:** `0550b71` and the owner's SOT chainmail observation.
- **Assessment:** YELLOW, non-blocking. Correct schema conformance was GREEN;
  silent omission of requested but unrepresentable concepts was a bounded
  usability risk rather than an MVP architecture failure.
- **Response:** completed at verified revision `3241c82`. WorldSeed now reports
  game-specific schema gaps without inventing an armor property or universal
  balance model.
- **Remaining owner decision:** whether the SOT schema should actually gain an
  armor-value rule/property and references is a game-design choice, not an
  implementation assumption. It is not required to verify the generic workflow.

## Historical Creature-Slice Completion Review

- **Revision and evidence reviewed:** application revision `29719c4`, its QA
  report, the two-system demonstration contracts/results, connected Emberbound
  evidence, and the owner's normal-launcher smoke.
- **Assessment:** GREEN for the historical creature-slice criteria. Decision
  0015 subsequently expanded the active MVP; this finding does not claim that
  the complete-game authoring MVP is finished.
- **Non-blocking limitation:** Windows visual automation remains unavailable, and
  the lineage QA was headless rather than screenshot-based. This is optional UX
  and automation debt, not missing functional acceptance evidence.
- **Continuation boundary:** rule-conforming expansion, full-game planning, and
  human-readable publication are now part of the active MVP under decision 0015.

## Remaining Authorized Work

- Continue SOT development checks, then demonstrate the complete workflow using
  owner-supplied existing-TTRPG sample documents and a mechanically different
  genericity check. SOT is excluded from the final presentation.
- Preserve the optional future retrieval seam; semantic embeddings remain
  unnecessary while the structured project context is sufficient.

## Next Action Required

Use the new inventory observations to guide source/rule coverage recovery and
rule-backed schema proposals through existing designer review controls. Preserve
authoritative narrative rules without requiring structured fields. Then resume
multi-type content generation, evaluation, and publication. The owner authorized
feature-focused continuation beyond the earlier usage estimate. No new owner
product-scope decision is pending for the generic coverage checks.
