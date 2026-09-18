# LLM Integration

WorldSeed's natural-language features use a model selected by the designer. The integration is a GUI-ready service layer: it accepts a safe model profile, sends a conversation to the selected runtime, and returns assistant text. It has no dependency on a command-line interface, file storage, the canonical meta-schema, or the change ledger.

## Local-first runtime

v0.2 supports the native local [Ollama API](https://docs.ollama.com/api). A standard installation needs no API key and no user-facing endpoint setup: WorldSeed uses `http://127.0.0.1:11434/` by default. It can safely list models already installed in Ollama through `GET /api/tags`; it never downloads, imports, creates, changes, or deletes a model.

```text
Avalonia model settings
        |
local Ollama model library
        |
OllamaModelCatalog + LlmClientFactory
        |
ILanguageModelClient
        |
Ollama /api/chat
```

An Ollama request contains the ordered system, user, and assistant messages, has streaming disabled, and returns one complete assistant response. WorldSeed disables optional model thinking for its default local profile, keeping the structured answer focused and responsive. The adapter maps a requested temperature and output limit to Ollama's `temperature` and `num_predict` options. Rule drafting and schema translation ask Ollama for a JSON object; their separate validators still enforce the required WorldSeed shape and provenance. Rule drafting currently requests at most 1,200 generated tokens. The adapter does not decide the prompt, interpret rules, persist conversations, or alter game data; the rule-drafting and translation layers own those jobs.

## Profile safety

`LlmModelProfile` is safe to place in application preferences: it contains only an identifier, display name, provider kind, local endpoint, and selected model name. The normal local profile needs no secret. Provider failures intentionally expose only a safe status-oriented message, never raw response bodies.

## Deferred hosted-provider work

The existing OpenAI-compatible adapter and credential abstraction are retained as technical debt for a later hosted-provider feature. They are deliberately not surfaced by the current application workflow. Before enabling them, the project needs secure operating-system credential storage, saved-profile management, clear privacy/cost disclosures, provider-specific tests, and a GUI configuration experience. Secrets must never be written to game files, ledgers, Git, logs, or error messages.
