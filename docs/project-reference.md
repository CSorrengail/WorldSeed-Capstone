# WorldSeed Project Reference Register

## Purpose

This register identifies the evidence a non-implementing project advisor uses to
assess WorldSeed. It records authority and current gaps; it does not create new
product requirements or replace a decision by the project owner.

## Authority Order

1. **Current owner direction** is authoritative when it is explicit. It may
   refine earlier planning documents.
2. **Capstone proposal** (`../7-2 Capstone Proposal.docx`) defines the project
   purpose, target users, proposed end-to-end workflow, planned milestones, and
   original MVP boundaries. This file is intentionally not tracked; this register
   preserves its role when it is available beside the repository.
3. **`AGENTS.md`** defines non-negotiable architectural principles for the
   project, especially the game-agnostic meta-schema boundary and separation of
   source material, schemas, game data, and lifecycle information.
4. **Accepted decision records** in `docs/decisions/` refine the architecture.
   A later accepted decision takes precedence over an earlier note when they
   conflict. In particular, decisions 0001 through 0009 define provenance and
   proposal boundaries; 0010 through 0013 define UI, local-model, localization,
   and distribution directions.
5. **Architecture documents** in `docs/architecture/` describe the intended
   component boundaries and current workspace design. They are implementation
   guidance and must yield to the first four sources when inconsistent.

Implementation, tests, QA reports, commit history, and generated artifacts are
evidence of current status. They do not define requirements or make an unfinished
feature complete by themselves.

## Capstone Outcome To Assess

The capstone aims to demonstrate a general-purpose, AI-assisted TTRPG design
tool. A designer supplies natural-language rules and design goals; WorldSeed
turns them into structured, traceable game knowledge; the system can use that
knowledge to propose content; deterministic checks and, where feasible,
quantitative balance evaluation inform revision; the designer retains control of
what becomes established game knowledge.

The initial demonstrable workflow is intentionally narrower than a finished game
authoring product. It should establish rule interpretation, structured data,
content generation, validation, balance evaluation, revision, and human control
for a limited set of content types. Stretch work includes model-based balance
prediction, encounter support, export, and image generation.

## Current Evidence To Review

- `README.md` and `deploy/README.md` for the current runnable development
  topology.
- `docs/architecture/README.md`, `application-workspace.md`,
  `rule-drafting.md`, `schema-translation.md`, `game-data-workspace.md`, and
  `local-persistence.md` for the implemented pipeline and intended next stages.
- `qa/FEATURES.md` and `qa/STATUS.md` for the feature inventory, testing scope,
  and known QA limits.
- The latest bounded QA report under `artifacts/qa/` for test results associated
  with a specific commit. Reports must be evaluated by their tested revision and
  scope, never generalized beyond it.

## Documentation Gaps And Ambiguities

These are review questions, not requirements:

- No single, current MVP acceptance checklist reconciles the proposal's original
  week-by-week plan with the implemented schema-first workflow.
- The exact first end-to-end content-generation demonstration, its chosen content
  types, and its designer review criteria are not yet specified in one approved
  document.
- Balance evaluation is an intended core capability in the proposal, but there is
  no approved first balance metric, data requirement, or fallback demonstration
  plan.
- Retrieval/embedding research is planned but has no accepted decision defining
  whether it is required for the MVP.
- The change-management ledger and the language-versioning model have accepted
  architectural direction but no implementation milestone or acceptance criteria.
- QA has focused evidence for selected workflows. Approved visual baselines and a
  broader independent desktop review remain outstanding.

An advisor must surface these as YELLOW risks or questions unless an explicit
owner decision turns one into a blocking requirement.
