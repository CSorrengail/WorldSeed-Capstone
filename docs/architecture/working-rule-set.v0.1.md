# Working Rule Set v0.1

## Role and boundary

A **Working Rule Set** is the current, project-level proposal for a game’s rules. It turns one or more traceable structured rule drafts into validated records that are ready to inform schema translation.

It is deliberately not a canonical game schema, game data, an approval decision, a revision ledger, or a universal executable rules engine. A rule may remain prose-first when no honest structured representation exists.

Each project has one mutable working rule set in the MVP. It is a working proposal by default. Saving it replaces the current proposal after structural validation; the project UI does not require a formal approval or visible version-management action.

## Required top-level fields

```json
{
  "id": "working-rules-project-001",
  "projectId": "project-001",
  "formatVersion": "0.1",
  "name": "Example Game Working Rules",
  "description": "Current project-level rule proposal.",
  "revision": 3,
  "createdAt": "2026-10-13T00:00:00Z",
  "updatedAt": "2026-10-13T00:00:00Z",
  "rules": [],
  "concepts": [],
  "openQuestions": [],
  "exclusions": [],
  "conflicts": []
}
```

| Field | Meaning |
| --- | --- |
| `id` | Stable identifier for this project’s current rule set. |
| `projectId` | The owning WorldSeed project. |
| `formatVersion` | Version of this working-rule-set contract. |
| `name`, `description` | Human-readable identification. |
| `revision` | Internal incrementing number for safe updates and later history. It is not a user-managed version system. |
| timestamps | Creation and most recent successful update times. |
| `rules` | The current integrated rule records. |
| `concepts` | Named project concepts discovered while integrating rules. |
| `openQuestions`, `exclusions`, `conflicts` | Explicit uncertainty and scope boundaries. |

## Rule record

```json
{
  "id": "combat-initiative-turn-order",
  "name": "Combat Initiative Turn Order",
  "kind": "procedure",
  "authoritativeText": "At the start of combat, determine the order in which combatants take turns using Combat Initiative.",
  "evidence": [],
  "sourceDraftRules": [{ "sessionId": "session-combat", "ruleId": "draft-ci-turn-order" }],
  "conceptIds": ["combat-initiative", "combatant"],
  "schemaHints": [],
  "structuredSupport": {
    "automationStatus": "proposed",
    "representationKind": "worldseed-rule-support",
    "representationVersion": "0.1",
    "payload": {},
    "limitations": []
  },
  "openQuestions": [],
  "exclusions": []
}
```

| Field | Requirement and purpose |
| --- | --- |
| `id` | Stable, project-unique identifier. |
| `name` | Human-readable label. |
| `kind` | One of `definition`, `rule`, `procedure`, or `constraint`, matching the existing drafting vocabulary. |
| `authoritativeText` | Required human-readable rule wording. This is authoritative within the working proposal even if no structured support exists. |
| `evidence` | At least one exact supporting source passage for every substantive rule. |
| `sourceDraftRules` | One or more session-scoped structured-draft rule references from which the rule was integrated. Session scope prevents collisions between model-generated draft IDs. |
| `conceptIds` | Optional links to project concepts; they do not require a schema decision. |
| `schemaHints` | Optional, non-authoritative suggestions for a later game-schema proposal. |
| `structuredSupport` | Optional machine-oriented representation. It is usable only when its automation status and interpreter are clear. |
| `openQuestions`, `exclusions` | Local uncertainty and intentional boundaries for this rule. |

## Evidence

An evidence item preserves source identity and literal text rather than trusting an LLM-produced quotation.

```json
{
  "sourceNoteId": "source-combat",
  "passageId": "source-combat:p006",
  "excerpt": "At the beginning of combat, subtract CI equal to the lowest CI among all combatants."
}
```

The validator must confirm that:

- the source note belongs to the project;
- the passage identifier belongs to that source note when one is present;
- the excerpt exactly matches the preserved source material;
- each cited session-scoped draft rule exists in the project interpretation artifacts; and
- every substantive working rule has evidence.

Rules may have evidence from many source files. No source file is a complete rule set by assumption.

## Concepts

Concepts are lightweight project vocabulary, not entity-type decisions.

```json
{
  "id": "combat-initiative",
  "name": "Combat Initiative",
  "description": "A value used to determine turn order during combat.",
  "evidence": []
}
```

A concept may later become an entity type, value type, property, procedure participant, or remain only explanatory vocabulary. The Working Rule Set must not decide that automatically.

## Schema hints

Schema hints are permitted in the MVP because they help the later translator, but they are proposals only. They never create or mutate schema definitions directly.

```json
{
  "kind": "property",
  "suggestedId": "combatant-combat-initiative",
  "suggestedName": "Combat Initiative",
  "parentSuggestedId": "combatant",
  "rationale": "The rule treats Combat Initiative as a value associated with each combatant.",
  "sourceRuleIds": ["combat-initiative-turn-order"]
}
```

Allowed hint kinds are `entityType`, `valueType`, `schemaFragment`, `property`, `relationship`, `rule`, `procedure`, and `validation`. `parentSuggestedId` is optional and is useful for a suggested property or nested value. A hint’s identifiers are only candidate names; schema translation may merge, rename, reject, or ask about them.

## Structured support

`structuredSupport` follows the Meta-Schema v0.5 principle. Its `automationStatus` is one of `none`, `proposed`, `partial`, `reviewed`, or `executable`.

For v0.1, the default is absent or `none`. The formalizer may create a `proposed` payload, but no generic rules engine will execute it. `executable` is prohibited until WorldSeed defines and implements a named interpreter for that representation.

This avoids prematurely forcing dice, cards, probabilities, turns, attributes, or any other genre-specific mechanism into the universal layer.

## Integration behavior

When updating the working proposal, the model works on a bounded batch of completed source-draft rules. It may propose to:

- add a rule or concept;
- consolidate several source-draft rules into one cohesive rule;
- clarify or refine wording while preserving evidence;
- identify a contradiction between sources;
- identify missing information as an open question; or
- attach optional schema hints.

It may not invent evidence, overwrite source text, or silently make a schema change. When a current proposal exists, the formalizer receives it as context and should preserve or deliberately consolidate its rules while still citing completed source drafts. The service returns a complete replacement proposal that deterministic validation checks before it becomes the project’s current working rule set.

The first implementation keeps this operation intentionally simple: it does not attempt a hidden merge or independently treat a source file as the owner of a rule. The generated proposal is a fresh, project-wide view of all completed interpretation drafts, with the current proposal supplied to reduce accidental regression. A future change-management layer can introduce explicit diff and merge decisions without changing the provenance model.

### Bounded-batch implementation

The initial implementation now uses small batches of no more than four source-draft rules. It intentionally does not consolidate rules across batches, infer cross-batch contradictions, or attach schema hints. Those require a later, separately bounded reconciliation step; this prevents large projects from requiring one oversized and fragile model response merely to create an initial proposal.

Every source-draft rule in a batch must appear in at least one resulting working rule. Each batch is parsed and provenance-validated before deterministic code combines the batches into one proposal. If a response is incomplete, WorldSeed retries only that batch as two smaller batches. The existing descriptions of whole-proposal consolidation and prior-proposal context describe the intended future reconciliation layer, not the current bounded-batch formalizer.

The model receives short opaque citation IDs (for example, `d001`) rather than internal session identifiers. It returns those IDs, and deterministic code resolves them to the actual session-scoped `sourceDraftRules` and literal evidence before validation and storage. This keeps the model-facing format small while retaining exact provenance in the stored Working Rule Set.

## Persistence and future evolution

MongoDB should store the set as a dedicated document, separate from the existing `DesignProject` document. Suggested collections are:

- `designProjects` — sources and temporary/completed source interpretation artifacts;
- `workingRuleSets` — one active working rule set per project in the MVP;
- `schemaProposals` — future schema proposals tied to a working-rule-set revision;
- `gameSchemas` and `gameData` — future canonical snapshots and instances.

The API should enforce one active rule set per project with a unique project ID index. A later change-management subsystem can add immutable historical records or snapshots without changing the v0.1 rule-record meaning.

## Non-goals for v0.1

- Formal human approval, branching, merging, or source-control history.
- Automatic application of schema hints.
- A universal executable expression language.
- Game-data editing or generated content.
- Generic balance scoring.
- Assuming that sources are complete, consistent, or mechanically executable.
