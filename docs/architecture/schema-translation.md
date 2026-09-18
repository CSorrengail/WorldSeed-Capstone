# Schema Translation Proposals

Schema translation converts a validated structured rule draft into a candidate canonical game schema. It is intentionally proposal-only: it neither overwrites a schema nor records a decision to apply one.

The selected model receives the draft and may ask one clarification or return a full v0.5 game-schema document. `SchemaTranslationJsonParser` then validates that document with `GameSchemaValidator` and checks a second traceability map: every top-level schema definition must cite rule IDs that exist in the input draft.

```text
structured rule draft
      ↓
candidate schema + definition-to-rule map
      ↓
structural and traceability validation
      ↓
later change-set creation and review
```

This allows the model to make a proposal while keeping the canonical schema and change-management decision separate.
