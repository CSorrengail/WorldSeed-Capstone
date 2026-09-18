# 0006: Use a user-selected, provider-neutral language-model client

## Context

WorldSeed’s natural-language processing must use a model chosen by the designer, while later GUI code should not need provider-specific logic or store credentials alongside game content.

## Decision

Define a provider-neutral `ILanguageModelClient` and a safe `LlmModelProfile`. Implement an OpenAI-compatible Chat Completions adapter first. Resolve credentials separately through `ILlmCredentialProvider` when a request is made.

## Consequences

The future GUI can offer saved profiles and sensible presets without coupling the rest of the project to a particular hosted service or local model. The first adapter works with compatible endpoints; non-compatible providers require a new adapter, not a rewrite of the rule-design workflow. Secrets remain outside JSON artifacts and source control.
