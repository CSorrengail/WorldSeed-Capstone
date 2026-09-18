# 0008: Persist pre-canonical design sessions separately

## Context

An isolated model response is insufficient for iterative game design. Designers need to retain their original notes, follow-up answers, and the latest structured draft before anything is converted into a canonical schema or reviewed as a change.

## Decision

Add a persistent design-session model and local JSON store. Sessions reference a model profile by identifier and contain source notes, conversation messages, and the latest validated drafting turn. They contain no secrets or approval state.

## Consequences

The future GUI has a stable conversation-level service boundary. The application can persist early design work without prematurely treating it as canonical game content. Translation and change management remain later, distinct steps.
