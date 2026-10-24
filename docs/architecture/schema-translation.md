# Schema Translation Proposals

Schema translation converts a small selected group of validated Working Rules into a candidate game-schema proposal. It is intentionally proposal-only: it neither overwrites a schema nor records a decision to apply one.

The desktop Schema page uses a FIFO work queue. A designer may add any number of Working Rules, but WorldSeed sends exactly one rule per proposal so a local model produces a compact, isolated schema addition. The queue pauses on a clarification question or validation failure and leaves that rule at the front until the designer resolves it. The request requires a minimal response shape and avoids conventional definitions that the selected rule does not require. If the first response is malformed or incomplete, WorldSeed makes a corrective retry before reporting a failure. Before validation, the parser normalizes lossless model presentation variants such as a definition-source map, an outer `id`/`name` placed outside metadata, or a named primitive type expressed with repeated `name` fields. It never invents definitions, properties, or source links. `SchemaTranslationJsonParser` then validates that document with `GameSchemaValidator` and checks a second traceability map: every top-level schema definition must cite rule IDs that exist in the selected input.

```text
selected Working Rules
      ↓
candidate schema + definition-to-rule map
      ↓
structural and traceability validation
      ↓
later change-set creation and review
```

The desktop workspace persists one current non-canonical schema proposal per project. It records the exact Working Rule Set revision and selected rule IDs that informed it. Rebuilding the Working Rule Set clears this proposal rather than silently leaving a stale schema behind.

This allows the model to make a proposal while keeping the canonical schema and change-management decision separate. Cross-proposal assembly, reconciliation, and approval remain later steps.
