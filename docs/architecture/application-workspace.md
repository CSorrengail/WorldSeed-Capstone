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
├── Game Data and Content        (future)
├── Validation and Balance       (future)
└── History and Settings         (future)
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

### Future areas

Game Data and Content will create instances that conform to an exact game-schema revision. Validation and Balance will run deterministic checks or game-specific analyzers where structured support exists. History and Settings will eventually expose formal revisions, decisions, model configuration, and exports.

These areas may appear as disabled or informative navigation entries in the MVP, but their absence must not force a redesign of project navigation later.

## Cross-cutting behavior

### Provenance

Every downstream artifact can lead back to the original source passages that support it. A user must be able to inspect this without leaving the current task: for example, selecting a working rule should reveal its supporting draft statements and exact source excerpts.

### Uncertainty

Open questions, exclusions, conflicting source statements, and automation limitations are project information. They are not errors to hide or evidence that a rule must be discarded.

### LLM boundary

The model proposes interpretations, normalized rules, and schema hints. Deterministic code validates identifiers, references, artifact shape, and provenance. The model does not silently create canonical game knowledge or mutate data outside the artifact currently being reviewed.

### Future versioning

The MVP exposes one current working proposal per project. Records still have stable identifiers, timestamps, and an internal revision number. This permits later immutable revisions and change-ledger integration without presenting source-control workflows now.

## MVP navigation transition

The source-drafting screen is the Sources area. The Rules area is now the first project-level workspace extension: it builds, saves, reloads, and inspects one validated Working Rule Set. Schema follows once a persisted Working Rule Set can be translated. The UI uses project-level navigation from this transition onward rather than growing the source screen into a long page of unrelated panels.
