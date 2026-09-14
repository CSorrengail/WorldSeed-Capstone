# v0.4 Draft: Canonical Model and Change-Management Boundary

v0.4 removes approval, proposal, and source-tracking fields from canonical game schemas and game data. Those fields described the lifecycle of an artifact rather than the game it represents, creating repetitive noise and coupling the meta-schema to one workflow.

## Canonical layer

The meta-schema now describes only stable game meaning: identifiers, value shapes, entity types, relationships, rules, procedures, validations, schemas, and data instances.

## Change-management layer

A future, separate subsystem tracks changes much like source control. It can store source material, LLM interpretation proposals, review decisions, actors, timestamps, base revisions, result revisions, and structured diffs. It references the canonical artifact IDs but does not alter the canonical schema shape.

This separation permits different workflows—single-designer approval, team review, automated proposals, or direct editing—without redefining the game model.

## Result

The v0.4 5e subset is structurally equivalent to the v0.3 test but has no approval or provenance fields. Its source and proposal history would instead be stored by the future change-management system.
