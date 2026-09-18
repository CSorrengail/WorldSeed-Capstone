# Schema Validation

The portable JSON Schema file validates the basic JSON document shape. The C# `GameSchemaValidator` performs semantic checks JSON Schema cannot perform alone: definition identity, reference resolution, type-reference resolution, schema-fragment references, relationship endpoints and attributes, value-type definitions, bounds, procedure links, and rule/validation targets. Reusable invalid-schema fixtures prove these failures are rejected.

`GameDataValidator` validates a game-data document against one loaded game schema. It checks schema identity and version, entity types, required and nullable properties, primitive, enumeration, record, alias, collection, map, and union value shapes; property and collection bounds; forward and backward references; relationship attributes, endpoints, and cardinality. An omitted property with a declared default is valid; applying that default to a stored instance remains an application concern.

`ChangeLedgerValidator` validates the separate provenance ledger. It checks unique identities, preserved designer input, source and proposal references, immutable revision lineage, artifact consistency, timestamps, and the requirement that an applied change has a human apply decision. It cannot make a file system append-only; that is a persistence-layer responsibility.

`RuleDraftJsonParser` validates strict structured-rule responses from the selected model. It rejects non-JSON responses, unknown actions, incomplete drafts, duplicate identities, and rule statements with no source-note reference.

The validator intentionally does not evaluate game rules, balance, dice, cards, or other game mechanics. It validates only the universal structural contract.
