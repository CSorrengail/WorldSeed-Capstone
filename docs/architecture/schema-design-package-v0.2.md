# Schema-Design Package: v0.2 Draft

## Purpose

This package is a design artifact, not application code. It proposes a revision to the universal meta-schema while preserving `meta_schema.v0.1.json` unchanged. The draft is judged by whether very different games can describe their own structures without adding universal, genre-specific concepts.

## Decisions embodied in the draft

- A game schema describes game-defined entity types, reusable value types, relationships, rules, procedures, and validations.
- A rule's approved natural-language text is authoritative. Structured information is optional derived support for automation.
- Raw designer input, LLM proposals, review decisions, and canonical definitions are distinct artifacts. The canonical schema contains approved definitions; the application can retain the fuller review history.
- A simple link stored as a field is a reference property. A link with its own meaning, roles, constraints, or attributes is a relationship.
- Procedures replace the assumption that every game has actor-owned abilities. A procedure may be an action, move, phase, ritual, resolution process, or any other game-defined process.
- Validation is a separately defined, executable check that may be traced to a rule. Non-executable rules remain valid and authoritative.
- No universal balance model is included. Balance analysis belongs to a particular game schema and only exists where that game defines meaningful measures.

## Intentionally deferred

The draft does not commit the project to inheritance, traits/interfaces, a formula language, a dice language, persistence technology, or a formal JSON Schema implementation. It leaves structured rule payloads open until a concrete MVP needs a safely executable representation.

## Acceptance criteria

The example schemas in `schemas/examples/` are conceptual acceptance tests. A useful v0.2 must let each example express its central concepts using only game-defined names. The examples are not built-in templates, and their terms must never become universal application classes.

## Proposed next implementation boundary

After this draft is reviewed, select one example game as the MVP demonstration game. Implement only the structural validation needed to load its approved schema and data, then add one human-reviewed interpretation path and one game-specific validation or measurement.
