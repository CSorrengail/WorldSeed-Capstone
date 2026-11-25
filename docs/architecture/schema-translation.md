# Schema Translation Proposals

Schema processing accounts for selected Working Rules without requiring every
statement to become a new data structure. Structural changes accumulate in a
non-canonical proposal; activation and game-data compatibility review stay separate.

## Explicit Rule Outcomes (Owner Approved 2026-11-25)

- `create`: genuinely new structure, with a definition-to-rule source mapping.
- `extend`: new fields/entries on an existing definition, using its identity and
  the controlled-change merger. Ordinary processing cannot change its collection,
  kind, or existing member semantics (types, requirements, constraints, values).
  Conflicting redesigns use the designer schema-change request path.
- `alreadyRepresented`: explicit links to unchanged saved definitions, without
  copying them or claiming the rule originated those definitions.
- `retainAsText`: a procedure, interpretation, or design goal needs no new fields.
- `excludeFromMechanics`: document metadata, attribution, or licensing needs no
  structures. Original Working Rules/evidence remain retained; this does not
  delete text from the corpus or automatically rewrite publication.
- `needsClarification`: a specific unresolved detail, using the answerable
  clarification workflow. This is not completed work.

`recordOutcomes` is a successful no-change response with per-rule reasons, not a
placeholder schema. A structural `presentProposal` contains `outcomes` beside
`schema` and `definitionSources`. New native requests require an outcome for every
selected rule. Legacy structural responses remain readable; only actual mappings
yield inferred structural outcomes. Contradictory mappings fail validation.

Append-only `ProjectSchemaProposal.RuleAssessments` history lives outside the
meta-schema. The API validates new entries against the actual Working Rule Set
identity/revision, rule fingerprint and saved schema snapshot. Linked definitions
and their dependencies have fingerprints: changes invalidate coverage, while
unrelated schema additions do not. Nonstructural outcomes remain bound to their
rule revision. Model rationale is evidence, not proof of semantic correctness.

The picker uses current assessments for completion/filtering. Historical citations
and name matches remain visible as unassessed hints. Existing schema/provenance
are preserved, without automatic cleanup of potentially incorrect earlier output.

The desktop Schema page uses a FIFO work queue. A designer may add any number of Working Rules, but WorldSeed sends exactly one rule per proposal so a local model produces a compact, isolated schema addition. The queue pauses on a clarification question or validation failure and leaves that rule at the front until the designer resolves it. The request requires a minimal response shape and avoids conventional definitions that the selected rule does not require. If the first response is malformed or incomplete, WorldSeed makes a corrective retry before reporting a failure. Before validation, the parser normalizes lossless model presentation variants such as a definition-source map, an outer `id`/`name` placed outside metadata, or a named primitive type expressed with repeated `name` fields. It never invents definitions, properties, or source links. `SchemaTranslationJsonParser` then validates that document with `GameSchemaValidator` and checks a second traceability map: every top-level schema definition must cite rule IDs that exist in the selected input.

```text
selected Working Rules
      ↓
per-rule outcomes + optional candidate schema/source map
      ↓
outcome, structural, and traceability validation
      ↓
independent saved progress; explicit activation when structures change
```

The desktop persists one current non-canonical proposal per project. Successful
items save independently; failed persistence never replaces the saved in-memory
proposal or completes a queue item. Rebuilding Working Rules retains older schema
work as stale evidence, not verified coverage of the new revision.

Automatic discovery remains selective for content types and supported relationships,
not a complete audit of every rule. Existing type names alone no longer justify
skipping an unassessed supporting rule. The manual queue can account for arbitrary
selected rules, including nonstructural outcomes. Canonical activation is explicit.
