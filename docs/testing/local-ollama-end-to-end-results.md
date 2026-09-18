# Local Ollama End-to-End Results

## Scenario

The repeatable runner used one complete designer source note about three route cards, traveler choices, shared or personal supply tokens, and discarding unchosen cards. The test exercised the real local flow:

```text
source note -> DesignSession -> RuleDraftingService -> Ollama -> JSON parser -> source-trace validator
```

No session, game schema, game data, or model library entry was written by the runner.

## Results

| Model | Result | Observation |
| --- | --- | --- |
| `qwen3:8b` | Rejected | Produced garbled, non-JSON text. |
| `llama3.2:3b` before a JSON example | Rejected | Returned a reasonable draft but omitted the required `action` field and used concept strings rather than concept objects. |
| `llama3.2:3b` after an explicit JSON example | Rejected | Added `action`, but left a rule without a source-note ID. |
| `llama3.2:3b` with Ollama JSON mode | Structurally accepted | Produced valid JSON and source IDs, but fabricated an unsupported constraint and a fake book/page citation. This demonstrates that a valid source ID is not semantic proof that the rule is supported by that source. |
| `llama3.2:3b` after a stronger no-invention instruction | Rejected | Avoided the fake citation but returned an empty duplicate rule with no source ID. |

## Changes made from evidence

- Local Ollama profiles disable optional thinking output.
- Rule drafting caps output at 1,200 tokens.
- Rule drafting and schema translation request Ollama's JSON-object mode.
- The drafting prompt now includes a minimal exact response shape, requires source IDs on every rule, and explicitly bans invented mechanics and external citations.

## Assessment

The integration boundary works as intended: malformed output and missing traceability are rejected before a draft can advance. `llama3.2:3b` is useful for exercising the failure paths but is not reliable enough to be the recommended drafting model. `qwen3:8b` is not currently usable for this task on this installation.

Even a structurally accepted result needs designer review, because the present validator verifies JSON shape and declared provenance—not whether the natural-language statement is semantically entailed by a cited source note. A future source-support review step should make that limitation visible to the designer rather than silently treating citations as proof.

## Next benchmark decision

The installed `qwen3.6:35b` model is the natural quality-comparison candidate. It is 23 GB, so its test may take substantially longer and use much more memory or GPU capacity. Benchmark it only after confirming the machine can comfortably run it. The first comparison should use this exact runner and scenario, then evaluate response time, format validity, source citations, and fidelity to the original note.
