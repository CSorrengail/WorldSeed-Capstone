# LLM Integration

WorldSeed’s natural-language features use a model selected by the designer. The integration is a GUI-ready service layer: it accepts a safe model profile and resolves a credential only at request time. It has no dependency on a command-line interface, file storage, the canonical meta-schema, or the change ledger.

## Initial adapter

v0.1 implements the widely supported OpenAI-compatible Chat Completions HTTP contract. This allows one adapter to communicate with compatible hosted services and many local model servers. Additional provider-specific adapters can implement `ILanguageModelClient` later without changing conversation, proposal, or GUI code.

```text
GUI profile selection
        +
secure credential lookup
        ↓
LlmClientFactory
        ↓
ILanguageModelClient
        ↓
hosted provider or local model endpoint
```

## Configuration and secrets

`LlmModelProfile` contains only information that can safely be stored in user preferences:

- profile identifier and display name;
- provider kind;
- base endpoint URI;
- selected model name; and
- whether a bearer credential is needed.

`ILlmCredentialProvider` supplies the API key only when the selected profile is used. A future GUI should store it in an operating-system secret store or another secure secret mechanism. It must not place keys in game files, ledgers, Git, logs, or error messages.

A normal hosted setup should need only a provider choice, model selection, and one stored key. A local model profile can disable bearer authentication and point to its local endpoint. Docker is therefore optional: a containerized local model is simply another OpenAI-compatible endpoint from WorldSeed’s point of view.

## Present boundary

The adapter sends an ordered list of system, user, and assistant messages and returns assistant text plus available token usage. It does not yet construct prompts, ask clarifying questions, persist transcripts, or write rule drafts. Those are the next application-level workflow and should use the provider-neutral interface rather than call HTTP directly.

Provider failures intentionally return a safe status-only error. Response bodies are not surfaced, preventing accidental exposure of credentials or provider-side details.
