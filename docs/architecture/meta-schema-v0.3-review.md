# v0.3 Draft Review

v0.3 responds to the review of v0.2 without turning the meta-schema into a D&D-specific model.

## Changes from v0.2

- Every approval-controlled definition uses the same metadata and provenance structure.
- Source material and interpretation proposals now have explicit conceptual shapes, outside the canonical game schema.
- Rules and validations use generic schema targets, allowing them to address fields, relationships, procedures, or the game structure rather than only entity types.
- Rules and procedures are listed once at the game-schema level. Links from those definitions are canonical; an entity's associated rules can be derived.
- Executable structured support must name its representation kind and version.
- Type expressions now include map and union forms, and structural constraints have a small explicit vocabulary.
- The model now defines generic game-data, entity-instance, and relationship-instance structures tied to one schema version.
- Relationship bounds state precisely how they are interpreted.

## Still intentionally deferred

Inheritance, reusable schema fragments, units, dice/formula languages, migration mechanics, and a formal JSON Schema validator remain deferred. The 5e subset will show whether reusable composition and a constrained dice representation should be the next additions.
