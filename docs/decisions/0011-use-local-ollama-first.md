# 0011: Use local Ollama as the first model runtime

## Context

WorldSeed should work toward a simple desktop experience where designers supply an installed local model without configuring a hosted API or handling credentials. The project must remain capable of adding other runtimes later, but hosted services are not part of the current implementation focus.

## Decision

Make Ollama's native local API the first supported runtime. Use its default local endpoint, list only models already installed on the computer, and provide a provider-neutral chat interface to the rule-drafting and schema-translation layers. Do not download or import models as part of WorldSeed.

## Consequences

The initial desktop workflow is local-first and credential-free. Users must install and start Ollama themselves. Model quality and performance depend on the installed model and hardware. The existing OpenAI-compatible code remains documented technical debt, not an exposed application feature, until a secure and deliberate hosted-provider design is implemented.
