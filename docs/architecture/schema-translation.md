# Schema Translation Proposals

Schema translation converts a small selected group of validated Working Rules into a candidate game-schema proposal. It is intentionally proposal-only: it neither overwrites a schema nor records a decision to apply one.

The selected model receives no more than six related Working Rules and may ask one clarification or return a compact valid v0.5 game-schema document containing only the definitions needed by that group. `SchemaTranslationJsonParser` then validates that document with `GameSchemaValidator` and checks a second traceability map: every top-level schema definition must cite rule IDs that exist in the selected input.

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
