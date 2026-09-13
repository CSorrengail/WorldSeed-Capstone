# 0003: Keep rules, procedures, and validations distinct

## Context

Not all games have abilities, and not all rules can be deterministically checked.

## Decision

A rule expresses authoritative game intent. A procedure represents a game-defined process that may be invoked or followed. A validation is an executable or review-oriented check, optionally derived from a rule. Each may contain approved natural-language text; only structured portions explicitly marked executable may be run by software.

## Consequences

The system can retain prose-only mechanics without treating them as errors. It can later add a constrained expression language without retroactively changing the meaning of existing natural-language rules.
