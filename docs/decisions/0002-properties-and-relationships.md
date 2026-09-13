# 0002: Separate fields from meaningful relationships

## Context

v0.1 permits both reference-valued properties and standalone relationships without saying which should represent a game fact.

## Decision

Use a reference property for a simple field on one type, such as a ship's current captain. Use a relationship when the connection itself has game meaning, named roles, bounds, rules, or attributes, such as a treaty between factions.

## Consequences

The same fact must not be represented canonically in both forms. A relationship may define attributes and constraints; when it becomes sufficiently complex, a game schema can instead model it as its own entity type.
