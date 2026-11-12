# Project Workspace Information Architecture

## Purpose

WorldSeed is a project workspace for evolving a TTRPG from incomplete natural-language design material into structured, game-specific knowledge. It is not a one-way wizard and it is not a collection of unrelated source-file conversions.

A project owns the material and artifacts produced for one game. Source files are collective evidence for that project; a rule may be supported by several files, and a source file may support several rules. No imported source is presumed comprehensive.

## Workspace model

The desktop application remains one application. Its local API and MongoDB database are supporting services, not a second user-facing application.

```text
Project
├── Overview
├── Sources
├── Rules
├── Schema
├── Game Data
├── Content Review
├── Validation and Balance
├── Publication
└── Settings
```

These are navigation areas, not locked sequential steps. A designer may return to Sources after working on Schema, refine rules after adding content, or inspect provenance from any downstream artifact.

## Areas

### Overview

Shows the project name and description, source counts, working-rule-set summary, schema status, unresolved questions, and links to the most relevant next action. It must communicate uncertainty rather than implying that a project is complete merely because it has imported sources.

### Sources

The project source library preserves imported original notes and their origins. It supports adding, removing, viewing, and selecting source material.

The existing source-by-source LLM conversation belongs here. Its completed structured rule drafts are interpretation artifacts: they preserve evidence and clarify local portions of the design, but are not themselves the project rule set. An unfinished model conversation is temporary and ends when the application closes.

The future source UI should let the designer select one or more relevant source artifacts when asking WorldSeed to update the project working rule set. Large projects may need several interpretations or integration passes; the UI must not send every source to a model indiscriminately.

### Rules

Rules is the center of the MVP. Each project has one mutable **Working Rule Set**. It is a cohesive proposal built from the project’s source interpretations and designer answers.

The area must provide:

- a rule list with clear kinds and unresolved-item indicators;
- an inspectable rule detail view with authoritative wording, evidence, related concepts, and limitations;
- controls to generate or update the working proposal from selected evidence;
- model clarification questions when necessary;
- validation feedback before persistence; and
- optional schema suggestions shown as suggestions, never silently applied changes.

The UI does not need a formal approval screen in the MVP. Saving a valid working rule set makes it the current project proposal. It is still non-canonical and may be revised.

### Schema

Schema consumes a particular revision of the project working rule set and produces a non-canonical game-schema proposal. It will use the existing schema-translation and schema-validation services after those services are connected to persistence and the workspace.

The area must show the proposed game structure, the working rules that support each definition, validator results, and unresolved modeling choices. It must not automatically overwrite a schema or transform schema hints into authoritative definitions.

### Game Data

Game Data contains canonical instances that conform to one exact active-schema revision. Direct entry editing remains available, while approved generated proposals require an explicit application action before becoming canonical. Validation refuses malformed types, missing required properties, invalid references, and game-specific structured constraints.

### Content Review

Content Review generates one bounded proposal for a game-owned content type. It uses the active schema, relevant working rules, canonical examples, and applicable designer decisions. Proposals preserve schema gaps, revisions, decisions, and provenance. Approval makes a proposal eligible for application; it does not itself mutate Game Data.

### Validation and Balance

Mechanical validation is deterministic and schema-specific. Balance evaluation is advisory and exists only when the project defines a matching game-specific profile with explicit signals and reference values. WorldSeed does not apply a universal challenge rating or scoring formula.

### Publication

Publication creates immutable, provenance-bearing snapshots from the exact Working Rule Set, active schema, and valid canonical Game Data. Designers can arrange a presentation-only edition, request section-scoped local-model prose, compare it with exact source material, and approve or reject it. Ready Markdown is player-facing; manifests and structured units retain technical traceability.

### Settings and future history views

Settings exposes model and inactivity configuration. Revision and decision history currently appears in the workspace where it is used. A consolidated history view, richer navigation, localization, semantic retrieval, and additional export formats remain optional future work.

## Cross-cutting behavior

### Provenance

Every downstream artifact can lead back to the original source passages that support it. A user must be able to inspect this without leaving the current task: for example, selecting a working rule should reveal its supporting draft statements and exact source excerpts.

### Uncertainty

Open questions, exclusions, conflicting source statements, and automation limitations are project information. They are not errors to hide or evidence that a rule must be discarded.

### LLM boundary

The model proposes interpretations, normalized rules, and schema hints. Deterministic code validates identifiers, references, artifact shape, and provenance. The model does not silently create canonical game knowledge or mutate data outside the artifact currently being reviewed.

### Future versioning

The MVP exposes one current working proposal per project. Records still have stable identifiers, timestamps, and an internal revision number. This permits later immutable revisions and change-ledger integration without presenting source-control workflows now.

## Navigation direction

The current tabbed workspace implements the project-level areas incrementally. Its functional boundaries are stable, but visual hierarchy and navigation remain eligible for a later usability pass. New capabilities should extend these project-owned artifacts rather than creating disconnected session files or a one-way wizard.
