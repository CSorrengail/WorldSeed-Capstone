# Monster MVP QA Acceptance Contract

Use this contract only after the implementation is assigned. QA verifies the
implemented revision and reports evidence; it does not choose systems, invent
balance rules, approve content, or repair failures.

## Required Demonstration Evidence

For each of two mechanically different TTRPG systems, QA must be able to verify:

1. **Ingestion:** the selected source rules become traceable structured rules and
   game-schema context for that system.
2. **Generation:** a creature proposal identifies the exact rule/schema revision
   it used and conforms to the game-specific creature structure.
3. **Mechanical validation:** deliberate invalid examples demonstrate that the
   system detects declared system-specific constraints or references.
4. **Balance evaluation:** a declared, game-specific balance profile supplies
   explicit constraints/reference values. The result distinguishes hard
   mechanical violations from explanatory balance concerns. QA must not accept a
   universal challenge-rating formula as evidence for both systems.
5. **Designer decisions:** approve, reject, and revision-request paths preserve
   the reviewed proposal/version, decision, and relationship to a replacement
   proposal where one exists.
6. **Export:** an approved creature exports the documented content plus the
   selected schema/rule and decision/version provenance fields.

## Verification Approach

- Use deterministic fixtures for each system's rules, schema, creature data,
  balance profile, expected violations, and expected calculated concerns.
- Keep model observations separate from deterministic assertions. A live model
  may be used only when the parent authorizes it; preserve raw output and do not
  use a second model as an oracle.
- Exercise the designer workflow through isolated UI tests or documented manual
  review where an interaction cannot be automated.
- Record the exact revision, systems, source fixtures, commands, exports,
  pass/fail/blocked checks, and observable two-system differences.

## Preconditions Still Needed Before Final MVP QA

- The owner or implementation design must name the two systems.
- Each system needs an explicit balance profile/reference dataset.
- The export format and required fields must be documented.
- The revision-request comparison behavior must be specified.
