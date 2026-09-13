# 0004: Version schemas as immutable approved revisions

## Context

Changing a game schema can invalidate or alter the meaning of existing game data.

## Decision

Treat an approved game schema version as immutable. A revision produces a new version that records its predecessor. Game data identifies the schema version it conforms to. Migration policy is deferred, but incompatible changes must be visible rather than silently overwriting prior definitions.

## Consequences

The first implementation can use JSON files and simple version identifiers. A database, migrations, and compatibility tooling are later concerns, but the model will not assume that all revisions are interchangeable.
