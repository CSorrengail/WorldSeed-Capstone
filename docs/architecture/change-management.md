# Change Management

The change-management ledger is a separate, append-only record of how a canonical artifact came to exist. It does not add workflow fields to a game schema or game-data document, and it does not make an LLM output authoritative by itself.

## Core flow

```text
raw source material
        ↓
interpretation proposal (human or model)
        ↓
change set against a base revision
        ↓
human decision
        ↓
immutable resulting revision
```

Source material retains the original wording. An interpretation proposal records the complete human-readable rewrite presented to the designer, plus a structured change list and rationale. A change set identifies the canonical artifact and the before/after revisions it concerns. Decisions record the human outcome. A revision contains a snapshot, so a later edit cannot silently rewrite history.

## Deliberate boundaries

- The ledger supports a single designer, a team, direct editing, or model-generated proposals. It does not require a particular UI or review sequence.
- `actor` identifies whether an action came from a human, model, or service; model details are optional metadata, not authority.
- `applied` means a change set has a resulting immutable revision. It does not mean that the rules are balanced or mechanically executable.
- The validator checks document shape and cross-references. Append-only storage, content hashing, authentication, permissions, and merge/conflict tooling belong to the future application and persistence layer.
- Canonical schema and data validators remain independent. The ledger references their revisions but does not try to interpret their rules.

## Initial lifecycle

1. Capture original input in `sourceMaterials`.
2. Create one or more `interpretationProposals` that cite that input.
3. Create a `changeSet` naming the artifact, base revision (when one exists), intended resulting revision, and source/proposal links.
4. Record a human `decision`.
5. When applying a decision, store a new immutable revision snapshot and mark the change set `applied`.

The v0.1 ledger intentionally has no branches, merges, permissions, automatic patch execution, or database design. Those should follow real application needs rather than be assumed into the meta-schema.
