# 0001: Preserve source material separately from approved game definitions

## Context

Designers may begin with loose notes. An LLM can turn those notes into clearer proposed rules, but it must not silently become the authority on game mechanics.

## Decision

Retain raw designer input as source material. Store a human-confirmed rewrite as the authoritative text of an approved rule or definition. Record that the approved artifact was derived from its source material and, where available, from an interpretation proposal.

## Consequences

The approved rewrite is what generation and deterministic validation use. The original wording remains available to investigate ambiguity or revise the game later. Detailed UI history and audit presentation are application concerns, but the links between source, proposal, and approved artifact are part of the conceptual data model.
