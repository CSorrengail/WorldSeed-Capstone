# Monster MVP Demonstration Plan

## Approved Scope

This plan implements the owner-approved monster/creature vertical slice in
[decision 0014](decisions/0014-approve-monster-mvp-vertical-slice.md). It is the
minimum capstone demonstration, not the long-term product boundary.

## Demonstration Flow

```text
system source rules
  -> structured working rules and game schema
  -> creature-generation request and proposal
  -> system-specific validation and balance evaluation
  -> designer approve, reject, or request revision
  -> preserved decision/version history
  -> export
```

The flow must run for two TTRPG systems whose creature mechanics differ in a
documented way. A test system may be small, but each system must define the
specific schema, constraints, and reference values used to evaluate its creature.

## Implementation Milestones

1. **Demonstration contracts:** select the two systems; model the creature
   content requirements, validation constraints, balance-reference values, and
   expected export fields as game-specific data.
2. **Creature proposals:** add a bounded generation request and proposal artifact
   that uses established project rules and an active game schema. It must retain
   the rules/schema revision it used.
3. **Evaluation:** run existing structural/reference validation plus a
   game-specific balance profile. Present mechanical violations separately from
   non-fatal balance concerns and their supporting reference values.
4. **Designer decisions:** support approve, reject, and revision-request actions.
   Preserve the proposal version, resulting decision, and replacement proposal
   relationship without placing workflow fields into canonical game data.
5. **Export:** produce a documented export artifact for an approved creature,
   including its content, game-schema/rule context, and decision/version data.
6. **Two-system demonstration:** run the complete flow for both systems and
   document the observable results, limitations, and differences.

## Boundaries

- Use the structured rules knowledge base first. Retrieval/embedding work is a
  future extension behind a retrieval boundary, not a prerequisite.
- Balance profiles are system-specific and evidence-based. They must not imply
  that a single challenge-rating or formula applies across games.
- LLM output remains a proposal. Deterministic code validates structure,
  references, declared constraints, and balance-profile calculations; designers
  control final decisions.
- Preserve the universal meta-schema's game-agnostic boundary. Creature-specific
  definitions live only in the selected game schemas and demonstration data.

## Exit Criteria

The MVP is ready for capstone evaluation only when QA has independently verified
the acceptance contract, the two-system results are documented, and the designer
can observe the complete flow without relying on undocumented manual repair.
