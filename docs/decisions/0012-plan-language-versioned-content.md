# 0012: Plan for Language-Versioned Content

## Status

Accepted as a future requirement; no implementation yet.

## Decision

WorldSeed must eventually permit language-specific versions of human-facing game content without duplicating or changing the underlying game identity and structure. This includes names, descriptions, rule text, and other displayable content. A language version may inherit a default value and supply an explicit override when a translation needs different wording.

## Boundary

This does not make translation automatic, does not select a source language, and does not add language fields to the current non-canonical drafting workflow. The eventual data model should be designed after canonical projects, schemas, and data persistence are in use.

## Rationale

This preserves the useful reference-table pattern of virtual language tables with physical overrides while avoiding a premature localization system in the universal meta-schema.
