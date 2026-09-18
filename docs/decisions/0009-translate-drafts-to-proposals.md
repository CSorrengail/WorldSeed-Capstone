# 0009: Translate structured drafts into validated schema proposals

## Context

The project needs to move from clarified human-readable rules toward canonical schemas without allowing a model to silently alter the game.

## Decision

Use a separate translation service that returns a full candidate schema and a definition-to-rule traceability map. Validate both before returning the proposal. Do not apply or merge the candidate at this layer.

## Consequences

The next workflow can present a concrete schema proposal to the designer while retaining both structural validation and links to the rules that motivated each definition.
