# 0005: Use a separate append-only change-management ledger

## Context

WorldSeed needs to preserve original designer input, model-assisted interpretations, human decisions, and revisions without making canonical game documents verbose or coupling them to one approval workflow.

## Decision

Use a standalone change ledger. It stores source material, interpretation proposals, change sets, decisions, and immutable artifact revisions. A change set references the canonical artifact by kind and identifier, names its base and resulting revisions, and links back to supporting source and proposals.

## Consequences

Canonical game schemas and game data remain focused on game meaning. The first implementation can store complete JSON snapshots in files. A later application is responsible for append-only persistence, access control, hashing, branching, merging, and presentation of history.
