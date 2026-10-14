# Local persistence and Docker boundary

WorldSeed separates its native desktop interface from persistent services:

```text
Avalonia desktop app (host machine)
        | HTTP: localhost:5080
WorldSeed API (Docker container)
        | MongoDB driver
MongoDB (Docker container and named volume)
```

The desktop app reads local `.txt` files only after the designer chooses them. It sends the imported text to the local API as part of that project's source record; Docker does not mount, index, or scan host document folders. The API does not invoke Ollama. Ollama and its models remain host-local and are called by the desktop drafting workflow.

The API owns database access and exposes local-only project CRUD endpoints. Mongo stores each `DesignProject` as one document, preserving the project, original source notes, active drafting state while the app is open, and completed reviewable drafts. This document shape is intentionally a working-design container, not the future canonical game-schema, game-data, or change-ledger storage model.

`deploy/worldseed-local.bat` creates a local ignored `.env` file, builds the API image, and starts Mongo plus the API. Stopping the stack preserves the named database volume. The `reset` option explicitly removes that volume before rebuilding, which provides a reproducible empty installation.
