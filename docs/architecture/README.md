# Architecture Notes

This directory documents the conceptual model before implementation. It should keep the following layers distinct:

1. The universal meta-schema defines how a game can describe itself.
2. A game schema defines one game's concepts and structures.
3. Game data contains instances conforming to that game schema.

Source rule text remains authoritative. Any structured interpretation, including one proposed by an LLM, is derived information that requires human review before becoming canonical.
