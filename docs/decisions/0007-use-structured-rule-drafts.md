# 0007: Use structured rule drafts before canonical schema translation

## Context

Natural-language input is too ambiguous to convert directly into canonical game-schema structures. However, forcing a designer or model to choose schema entities and relationships during clarification would make early ideation unnecessarily technical.

## Decision

Introduce a strict, human-readable `StructuredRuleDraft` intermediate artifact. A selected LLM may ask one clarifying question or return a draft. Each rule statement must retain source-note references and mark unresolved material as assumptions or open questions.

## Consequences

The application can demonstrate useful model-guided design before canonical conversion exists. The later translator receives clearer, traceable input. Model output that is not valid structured JSON is rejected instead of being silently interpreted.
