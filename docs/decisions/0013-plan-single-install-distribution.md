# 0013: Plan for a single-install desktop distribution

## Context

The development environment currently uses a native Avalonia desktop application with an API and MongoDB running through Docker Compose. This is useful for repeatable development and local database testing, but it is not an acceptable long-term end-user installation experience.

## Decision

Treat a one-download, one-uninstall desktop distribution as a stretch goal. It must include every WorldSeed component except user-owned local Ollama and its model files. The installed application should require no separate Docker Desktop, database, or manual service setup from an end user, and it should be removable through one normal uninstall entry.

## Rationale

Ollama models can be large and are independently chosen by the designer, so WorldSeed should discover and use an existing local Ollama installation rather than package or delete models. WorldSeed's own desktop UI, API/service layer, and project database should instead be installed, started, upgraded, and removed as one product.

## Consequences

Docker Compose remains a development and reproducibility tool, not the final desktop distribution mechanism. A later packaging design must choose between an embedded local database and a private managed database/service started by the desktop application. It must include migration, backup/export, upgrade, shutdown, and uninstall behavior before it can replace the development stack.

## Unresolved questions

- Whether the installed product uses an embedded database or a managed local service.
- Windows installer technology and cross-platform packaging strategy.
- How backups and restoration are exposed without making project storage opaque.
