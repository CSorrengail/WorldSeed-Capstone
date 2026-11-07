# Creature MVP Demonstration Contracts

These two fixture-backed contracts are the fixed inputs for the approved creature
MVP. They make the evaluation observable without asserting that either system is
universal or that a TTRPG needs creatures at all.

| Contract | Mechanical Model | Hard Validation | Advisory Balance Signal |
|---|---|---|---|
| Emberbound | Numeric tactical creature values | Schema bounds for vitality, guard, technique impact, and technique count | Values above the named local reference band |
| Quiet Horizons | Narrative tags, prompt, response, and clock pressure | Schema bounds for tag count and escalation-clock impact | Scene pressure above the named local reference band |

The systems are deliberately fictional and small. This avoids importing an
external game's copyrighted content while still demonstrating two materially
different mechanical models. Emberbound does not prescribe a global combat
formula. Quiet Horizons has no hit points, armor, damage, dice, or challenge
rating.

## Fixture Packages

- [`emberbound.contract.json`](../qa/fixtures/mvp/creatures/emberbound.contract.json)
- [`quiet-horizons.contract.json`](../qa/fixtures/mvp/creatures/quiet-horizons.contract.json)

Each package contains source-note passages, a traceable Working Rule Set revision,
a valid game schema, and three deterministic creature-data cases:

1. a mechanically valid creature with no balance concern;
2. an invalid creature rejected by a declared schema constraint; and
3. a mechanically valid creature that receives only listed advisory balance
   concerns.

The future balance evaluator must report the second category as **mechanical
violations** and the third as **balance concerns**. It must not turn an advisory
reference-band signal into a structural failure.

## Common Lifecycle And Export Contract

Both packages require a proposal to retain the working-rule-set ID/revision and
the game schema ID/version used to generate it. A designer can approve, reject,
or request a revision. A revision request names its base proposal/version; the UI
comparison shows content values, mechanical violations, balance concerns, and the
resulting designer decision.

An approved export is JSON for the MVP and must include `content`, game schema and
working-rule context, proposal/version provenance, and the designer decision. The
future export implementation may add human-readable rendering, but must preserve
these fields.

## Scope Boundary

The fixture contracts are game-owned demonstration data, not additions to the
universal meta-schema. They also do not introduce semantic retrieval, a universal
creature type, dice assumptions, a generic simulation engine, or a cross-game
balance formula.
