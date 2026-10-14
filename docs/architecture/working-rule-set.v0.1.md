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
  "sourceDraftRuleIds": ["draft-ci-turn-order"],
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
| `sourceDraftRuleIds` | One or more structured-draft rule IDs from which the rule was integrated. |
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
  "excerpt": "At the beginning of combat, subtract CI equal to the lowest CI among all combatants.",
  "sourceDraftRuleId": "draft-ci-turn-order"
}
```

The validator must confirm that:

- the source note belongs to the project;
- the passage identifier belongs to that source note when one is present;
- the excerpt exactly matches the preserved source material;
- the cited draft rule exists in the project interpretation artifacts; and
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

When updating the working proposal, the model may propose to:

- add a rule or concept;
- consolidate several source-draft rules into one cohesive rule;
- clarify or refine wording while preserving evidence;
- identify a contradiction between sources;
- identify missing information as an open question; or
- attach optional schema hints.

It may not discard a previously stored working rule, invent evidence, overwrite source text, or silently make a schema change. The service returns a complete replacement proposal that deterministic validation checks before it becomes the project’s current working rule set.

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
