# Complete Game Authoring MVP Plan

## Goal

An experienced TTRPG designer can take a handful of loose and incomplete source
files through WorldSeed and produce a substantially fleshed-out game containing
cohesive rules, game-specific systems and content, mechanical/schema validation,
preserved designer decisions, and an organized readable document.

Decisions 0015 and 0016 define the active scope; balance is the first stretch
goal, not an MVP acceptance gate. The earlier creature vertical slice
is completed architectural evidence, not the current finish line.

## Existing Foundation

- Source ingestion, clarification, traceable drafts, and project-wide Working
  Rules.
- Game-specific schema proposals, scoped structure discovery, safe compatible
  activation, and immutable transition history.
- Canonical Game Data, bounded content proposals and expansion queues,
  validation, game-specific balance profiles, decisions, and application.
- Deterministic publication snapshots, editorial review, revision history, and
  human-readable Markdown output.

## Remaining Milestones

### 1. Project Planner And Gap Analysis

Provide a project-wide view of existing rules, schema, content, and publication
coverage. Identify contradictions, incomplete mechanics, missing dependencies,
missing content types, underdeveloped areas, and missing minimum game-specific
mechanical validation coverage. Present a prioritized plan that the designer can
accept, reject, revise, or defer.

### 2. Coordinated Rule And System Expansion

Turn an approved plan item into a bounded clarification and proposal workflow.
Allow new systems and related rules to be created, connected to existing rules,
reviewed, and safely incorporated without requiring the designer to manually
assemble every schema grouping. Carry forward any explicitly accepted minimum
mechanical validation needs without building the later automated assistance
feature prematurely.

### 3. Scalable Multi-Type Content Expansion

Generate reviewable batches across arbitrary game-owned content types. Respect
dependencies, existing canonical examples, designer decisions, validation, and
duplicate prevention. Keep every generated entry individually reviewable and
never auto-approve or silently apply it.

### 4. Mechanical Validation

Check generated content against the game-owned schema and implemented
constraints, explain mechanical violations, and retain designer review for
proposed validation changes. A missing balance profile must not block the core
approval/application/publication workflow. Do not claim unevaluated content is
balanced.

### 5. Complete Game Publication

Assemble canonical rules and approved Game Data into a coherent document with
designer-controlled organization, terminology, examples, cross-references, and
source-grounded editorial revision. Player-facing output must omit internal IDs
while manifests preserve technical provenance.

### 6. Full Demonstration And Stabilization

Run the owner-supplied, deliberately incomplete SRD 5.1 corpus through the
complete workflow, resolve blocking defects, and document observable results.
Use preserved rules, a designer-reviewed completion of an incomplete mechanic,
and original generated content for the primary presentation comparison. Sword
of Truth remains a development test case. Repeat a smaller check with a
mechanically different TTRPG to confirm the universal layer remains generic.
Prepare saved demonstration checkpoints and readable exports before owner
rehearsal. The target is a coherent D&D-derived supplement using the generic
application, with functional completion before the Week 9 reset.

## Deferred Until After This MVP

- **First stretch goal:** game-specific balance evaluation, profile coverage, and
  balance assistance, if time and usage remain after MVP completion (0016).
- Beginner-oriented onboarding and broad visual polish.
- Semantic embeddings when structured retrieval remains sufficient.
- Automated migration of incompatible schema revisions.
- Universal content types, dice assumptions, or balance formulas.
- Production-grade installer, distribution, and platform certification beyond
  what is necessary for the class demonstration.

## Completion Standard

The workflow must be usable without undocumented database edits, hand-authored
JSON repair, or code changes between stages. QA must verify the deterministic
boundaries and a connected demonstration. The owner determines whether the real
project is sufficiently fleshed out to represent a usable game rather than a
collection of isolated examples.
