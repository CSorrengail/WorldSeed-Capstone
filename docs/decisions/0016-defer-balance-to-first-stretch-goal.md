# 0016 Defer Balance Evaluation To The First Stretch Goal

## Status

Owner approved on 2026-11-19. This supersedes the balance-evaluation portion of
decisions 0014 and 0015 as a minimum MVP requirement; all other approved scope
remains in force.

## Decision

Prioritize completing the usable game-authoring build. Game-specific balance
evaluation, balance-profile configuration, and AI balance assistance are the
first stretch goal, pursued only after the MVP is complete and time and usage
remain.

Mechanical/schema validation remains required: generated content must conform
to the game's declared structures and implemented constraints. Designer review,
revision, approval/rejection, provenance, persistence, multi-type generation,
and human-readable publication remain MVP requirements.

## Consequences

- Missing balance profiles and absent balance advice do not fail MVP acceptance
  or justify delaying the core walkthrough.
- Preserve existing balance functionality. This decision does not request its
  removal or redesign.
- An unevaluated result must remain clearly unevaluated; it must not be described
  as balanced or safe merely because mechanical validation passed.
- The first stretch goal still uses explicit game-specific rules and reference
  values, with designer control; no universal balance formula is assumed.
- This scope change does not activate another autonomous development cycle.
