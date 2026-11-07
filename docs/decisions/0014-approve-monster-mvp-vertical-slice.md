# 0014 Approve the Monster MVP Vertical Slice

## Status

Owner approved on 2026-11-06.

## Context

The capstone proposal calls for an end-to-end AI-assisted workflow: rule
interpretation, structured knowledge, content generation, validation, balance
evaluation, revision, human control, and export. The project has a verified
foundation for ingestion, structured rules, schema proposals, game-data editing,
and persistence, but it needs one bounded content workflow before expanding to
other generated content types.

## Decision

The initial MVP vertical slice generates and manages **monsters/creatures**. This
is an MVP demonstration scope, not a universal WorldSeed concept and not a
requirement that every TTRPG schema define creatures.

The completed slice must demonstrate, for at least two mechanically different
TTRPG systems:

1. rule ingestion into the existing structured rules knowledge base;
2. generation of a creature that conforms to that game's schema and rules;
3. mechanical validation using declared system-specific rules and constraints;
4. balance evaluation using explicit system-specific rules, constraints, and
   reference values, with separate explanations for mechanical violations and
   balance concerns;
5. designer review that can approve, reject, or request revision, while
   preserving the relevant generated versions and designer decisions; and
6. export of the approved result with the context needed to understand its
   schema/rule and decision provenance.

No universal challenge-rating formula, monster model, dice model, or balance
formula may be introduced. The implementation may use structured rules directly
for the initial MVP. Semantic retrieval and embeddings are deferred, but later
retrieval must be introducible without replacing the structured rules knowledge
base or rewriting the content workflow.

## Consequences

The next product work should be limited to the capabilities required by this
vertical slice. Broader content types, advanced balance prediction, encounter
generation, image generation, full retrieval, and general export expansion stay
outside the initial MVP unless they become necessary to satisfy the decision.

The review/revision history belongs to the separate lifecycle and
change-management layer, not to the universal game schema. A simpler MVP
implementation may expose a constrained history model, but it must preserve
which generated version was reviewed and the designer's resulting decision.

## Remaining Implementation Decisions

- Select the two demonstration systems and document why their creature mechanics
  are materially different.
- Define the export artifact format and minimum human-readable/provenance fields.
- Define the specific system-owned balance profiles and reference values used in
  each demonstration.
- Define the minimum revision request input and regenerated-output comparison
  shown to the designer.
