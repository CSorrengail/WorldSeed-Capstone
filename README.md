# WorldSeed

This repository is being organized around an architecture-first capstone project. The immediate focus is testing and refining the universal meta-schema before application code is introduced.

## Repository layout

`src/` now contains reusable .NET validation and LLM-integration services; a future GUI will compose those services rather than reimplement them.

- `docs/architecture/` — architecture notes and diagrams.
- `docs/decisions/` — concise records of decisions and open questions.
- `schemas/meta/` — versioned universal meta-schema definitions.
- `schemas/examples/` — deliberately different hypothetical game schemas used to stress-test the meta-schema.
- `src/` — future .NET application source.
- `tests/` — future automated validation and application tests.
- `deploy/` — future Docker Compose and supporting deployment configuration.

`schemas/meta/meta_schema.v0.1.json` is the preserved original prototype. It should not be treated as a final architecture.
