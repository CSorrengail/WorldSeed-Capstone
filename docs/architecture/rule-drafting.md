# Structured Rule Drafting

Structured rule drafting is the boundary between a designer’s natural-language conversation and later canonical schema/data proposals. It is intentionally an intermediate, human-readable artifact—not a game schema and not a database record.

## Conversation loop

```text
designer message + source IDs
          ↓
selected LLM
          ↓
one clarification question OR a structured rule draft
          ↓
designer reply / later schema translation
```

The drafting service sends conversation history and source IDs through `ILanguageModelClient`. Its system prompt requires JSON only and permits two outcomes:

- `askClarifyingQuestion` — exactly one focused question, used only when an answer materially changes a rule.
- `presentDraft` — a structured draft with rules, concepts, assumptions, open questions, and exclusions.

Every proposed rule must cite at least one source-note ID, and every cited ID must be one supplied with that exact conversation. The drafting service rejects invented, missing, or unrelated citations. This preserves traceability before the change-management ledger begins recording a canonical change.

## Draft shape

A `StructuredRuleDraft` contains:

- a title and intended design outcome;
- rule statements classified as definitions, rules, procedures, or constraints;
- named concepts in plain language;
- explicit assumptions and unanswered questions; and
- exclusions that state what has not been defined.

The structured-rule format is deliberately less rigid than the universal meta-schema. It helps the model and designer make a game idea precise without forcing an early decision about whether a concept is eventually an entity, value type, relationship, or procedure.

## Boundaries

The service does not persist a transcript, call the change ledger, accept a decision, create a game-schema revision, or execute rules. A future GUI owns the conversation display and calls this service for each turn. A later schema-translation service will consume a reviewed draft and produce a separately validated canonical change proposal.
