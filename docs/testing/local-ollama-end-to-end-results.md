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
| `llama3.2:3b` with the full response schema | Structurally accepted | Supplied all required draft fields and allowed source IDs. It omitted two source details: the guide revealing three cards and discarding unchosen cards. |
| `qwen3.6:35b` with the full response schema | Timed out | Produced no response within three minutes. Ollama reported 59% CPU / 41% GPU execution on the test machine. |

## Changes made from evidence

- Local Ollama profiles disable optional thinking output.
- Rule drafting caps output at 1,200 tokens.
- Rule drafting and schema translation request Ollama's JSON-object mode.
- Rule drafting now supplies a full JSON Schema that requires all draft fields and restricts citations to the source IDs available in the current session.
- The drafting prompt now includes a minimal exact response shape, requires source IDs on every rule, and explicitly bans invented mechanics and external citations.

## Assessment

The integration boundary works as intended: malformed output and missing traceability are rejected before a draft can advance. The full response schema materially improved `llama3.2:3b`'s format compliance, but the model still omitted source material and is not reliable enough to be the recommended drafting model. `qwen3:8b` is not currently usable for this task on this installation, and the installed 35B model is not responsive enough on this hardware.

Even a structurally accepted result needs designer review, because the present validator verifies JSON shape and declared provenance—not whether the natural-language statement is semantically entailed by a cited source note. A future source-support review step should make that limitation visible to the designer rather than silently treating citations as proof.

## Next benchmark decision

The 35B comparison has now shown that a 23 GB model is too large for the test machine's 12 GB GPU. The next candidate should be a model that fits primarily in GPU memory. A 9–12B model is the reasonable size range for a quality comparison. The first comparison should use this exact runner and scenario, then evaluate response time, format validity, source citations, completeness, and fidelity to the original note.
