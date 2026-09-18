# Local Ollama End-to-End Test

This repeatable developer test runs one deliberately small, source-traceable rule through the actual local workflow. It does not download or modify models, save a design session, write a game schema, or change canonical game data.

## What it verifies

1. WorldSeed contacts the local Ollama `api/chat` endpoint using a chosen installed model.
2. A `DesignSession` preserves the original source note and gives it the stable ID `source-note-001`.
3. The rule-drafting prompt requests exactly one valid JSON action.
4. WorldSeed parses the response and validates its shape.
5. Every returned rule citation is restricted to the original source note.

Run it from the repository root after starting Ollama:

```powershell
dotnet run --project tools/WorldSeed.EndToEnd -- qwen3:8b
```

The tool prints both the raw response and the accepted structured result. If the model returns malformed JSON or an invalid source citation, the tool exits with an error and prints the raw response for diagnosis. This is expected useful feedback about prompt/model compatibility; it never silently accepts malformed output.

The default local profile disables optional model-thinking output and the drafting service requests at most 1,200 generated tokens. These guardrails keep the first interactive workflow from spending several minutes producing internal reasoning or an unnecessarily long JSON artifact.

The draft prompt includes an explicit minimal JSON example. This is intentional: small local models may understand the rule-design request but still omit a required wrapper field without a concrete format example. WorldSeed continues to reject rather than repair an invalid response, preserving a clear boundary between model proposal and validated artifact.

`qwen3:8b` is the initial test model because it is installed locally and should be practical for iterative structured-output experiments. Running the same test later with `llama3.2:3b` can reveal whether smaller models need stronger prompt or repair handling; the 35B model can be reserved for a quality comparison.

The completed initial results are recorded in [local Ollama end-to-end results](local-ollama-end-to-end-results.md).
