# WorldSeed

This repository is being organized around an architecture-first capstone project. The immediate focus is testing and refining the universal meta-schema before application code is introduced.

The local desktop application stores projects through a local API backed by MongoDB. Docker Compose runs that API and database; the Avalonia desktop UI and local Ollama models stay on the host machine. See [the local-stack instructions](deploy/README.md).

On Windows, double-click `RunWorldSeed.bat` to launch the desktop application. It reuses healthy Docker services immediately, and starts/builds them only when the local API is unavailable. The normal launcher is quiet; add `-v` or `--verbose` to see Docker and build details. Run `RunWorldSeed.bat --check` to verify only the Docker-backed API. After changing desktop or backend code, run `RunWorldSeed.bat --rebuild` once to intentionally rebuild the desktop app and replace the API container.

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
