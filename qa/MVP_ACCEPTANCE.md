# Complete Game Authoring MVP QA Acceptance Contract

QA verifies the assigned integrated cycle and final demonstration. It does not
invent game rules, approve designer decisions, or modify production code.

## Required Final Evidence

1. **Loose-file ingestion:** several source files enter one persistent project;
   exact source excerpts remain traceable through resulting Working Rules.
2. **Cohesive rules:** clarification and formalization produce a project-wide
   ruleset while preserving uncertainty, conflicts, and natural-language rules.
3. **Planning:** WorldSeed identifies meaningful missing or incomplete systems,
   dependencies, and content coverage. The designer can accept, revise, reject,
   or defer recommendations.
4. **System expansion:** an approved gap can become reviewed rules and schema
   changes through the normal provenance and decision boundaries.
5. **Content breadth:** the project generates and manages multiple game-owned
   content types with individually reviewable entries, duplicate prevention,
   dependency awareness, and explicit canonical application.
6. **Evaluation:** deterministic mechanical violations and game-specific balance
   concerns are separate, source/reference-backed, and demonstrably different
   across mechanically different games.
7. **Designer control:** approval, rejection, revision, schema-transition, and
   content-application histories survive reload and identify the reviewed
   revisions.
8. **Publication:** canonical rules and approved content produce an organized,
   human-readable document without internal IDs; its manifest retains exact
   technical provenance.
9. **Real-project demonstration:** a substantial Sword of Truth workflow reaches
   readable output without database edits, manual JSON repair, or code changes
   between stages.
10. **Genericity check:** a smaller mechanically different TTRPG follows the
    same universal workflow without introducing universal monsters, items,
    skills, dice, or challenge ratings.

## Verification Strategy

- Run focused deterministic tests during development, then one consolidated QA
  pass after each bounded multi-iteration development cycle.
- Use connected local-model/API/Mongo testing only at integration boundaries or
  when deterministic tests cannot establish the behavior.
- Preserve exact revision, commands, fixtures, outputs, pass/fail/blocked checks,
  process isolation, and observed limitations in the QA report.
- Final acceptance includes an owner-observed real-project run because “fully
  fleshed out” is a product sufficiency judgment, not only a structural test.

## Failure Conditions

- A stage requires undocumented repair, direct database manipulation, or a code
  change to continue.
- Generated rules or content silently become canonical.
- Game-specific concepts leak into the universal meta-schema or application
  domain model.
- Validation or balance claims lack game-owned rules/reference values.
- Publication omits approved canonical material or exposes technical IDs as
  player-facing prose.
