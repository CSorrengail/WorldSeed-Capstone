# 0015 Expand The MVP To Complete Game Authoring

## Status

Owner approved on 2026-11-13. This decision supersedes decision 0014 as the
active MVP completion target. Decision 0014 remains historical evidence for the
first verified vertical slice.

## Context

The creature-focused demonstration proved individual parts of the architecture,
but it is not the class-completion product the owner intends to demonstrate.
WorldSeed must support an experienced designer moving from a handful of loose,
incomplete source files to a substantially fleshed-out TTRPG with cohesive
rules, generated game-specific content, system-specific evaluation, designer
control, and an organized human-readable result.

## Decision

The active MVP is the complete designer workflow:

1. Import several loose source files into one persistent project while retaining
   exact source provenance.
2. Clarify ambiguous ideas and produce a cohesive project-wide Working Rule Set.
3. Identify important gaps, contradictions, missing systems, and missing content
   rather than assuming the submitted files are comprehensive.
4. Let the designer iteratively add, revise, connect, or reject proposed rules
   and systems.
5. Build and safely evolve a game-specific schema from the approved rules without
   hard-coding universal TTRPG concepts.
6. Generate substantial game-specific content across the types that particular
   game defines, such as items, creatures, skills, locations, or entirely
   different concepts.
7. Mechanically validate generated content and explain balance concerns using
   that game's own rules, constraints, and reference values.
8. Preserve review, revision, approval, rejection, schema-transition, and source
   history while keeping the designer authoritative.
9. Assemble the canonical rules and approved content into an organized,
   human-readable game document suitable for an experienced user to inspect and
   continue refining.

The MVP may remain visibly technical and require an experienced user. General
usability polish, beginner onboarding, semantic retrieval, automated migration
of incompatible schemas, and production distribution improvements are later
work unless they directly block this workflow.

## Consequences

The prior creature slice is no longer described as the completed WorldSeed MVP.
It is retained as verified architectural evidence. Current development should
prioritize project-wide planning and gap analysis, coordinated rule/system
expansion, scalable multi-type content generation, evaluation assistance, and
complete publication over cosmetic polish.

Completion requires an observable end-to-end demonstration using a substantial
real project, initially the owner's Sword of Truth notes, plus a mechanically
different TTRPG check that demonstrates the architecture remains generic.

The owner has not established a universal numeric threshold for “fully fleshed
out.” Acceptance therefore depends on demonstrated coverage and the owner's
judgment that the resulting rules and content constitute a usable game rather
than an isolated feature sample. Any proposed numeric class-demonstration target
must be recorded as a planning measure, not silently promoted to a requirement.
