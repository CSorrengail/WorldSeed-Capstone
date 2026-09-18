# Architecture Notes

This directory documents the conceptual model before implementation. It should keep the following layers distinct:

1. The universal meta-schema defines how a game can describe itself.
2. A game schema defines one game's concepts and structures.
3. Game data contains instances conforming to that game schema.
4. The change-management ledger records source, proposals, decisions, and immutable revisions for those artifacts.
5. The LLM integration layer connects a user-selected model to future natural-language workflows without making it part of the canonical schema.
6. Structured rule drafting converts a model-guided conversation into traceable, human-readable rule drafts before canonical translation.
7. Design sessions persist that early conversation and its latest draft without turning it into canonical content.
8. Schema translation turns a structured draft into a validated, traceable proposal without applying it.

Source rule text remains authoritative. Any structured interpretation, including one proposed by an LLM, is derived information that requires a human decision before becoming canonical.
