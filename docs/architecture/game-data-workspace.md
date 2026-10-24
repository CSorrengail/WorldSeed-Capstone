# Game Data Workspace

The Game Data workspace is the stage after schema design. It holds the actual
game-specific entries—such as weapons, skills, armor categories, or creatures—rather
than additional source notes or proposed schema definitions.

## Boundary

There are three deliberately separate layers:

1. **Working Rule Set:** source-backed statements of the game's intended rules.
2. **Schema proposal:** an editable proposal for the kinds of game data the project
   needs. It remains non-canonical and can be rebuilt from the current rules.
3. **Active schema and game data:** a validated schema snapshot and the individual
   entries that conform to that exact snapshot.

Activating a schema makes a snapshot from the saved proposal. Editing or replacing a
later proposal therefore cannot silently reinterpret already-saved game data. Once
data exists, replacing the active snapshot requires a deliberate migration workflow;
that workflow is intentionally outside this first vertical slice.

## Stored documents

Each project has at most one active schema and one game-data document in this MVP.
The active schema records its source proposal, Working Rule Set revision, and its own
revision. The game-data document records the active-schema id/revision and includes
the schema's own `gameSchemaId` and `gameSchemaVersion` in its JSON.

An empty game-data document is created when a schema is first activated:

```json
{
  "id": "game-data-project-id",
  "gameSchemaId": "schema-id",
  "gameSchemaVersion": "0.1",
  "entityInstances": [],
  "relationshipInstances": []
}
```

The existing `GameDataValidator` checks property types, required fields, references,
relationships, constraints, and the schema id/version. The Game Data service adds the
separate check that the document is attached to the exact active-schema snapshot.

## First vertical slice

The first UI iteration will let a designer activate the SoT schema proposal, create
and edit schema-defined entries, validate them before saving, and inspect the JSON
when diagnosing a problem. It will not ask an LLM to invent entries yet. Future LLM
generation will create reviewable suggestions in this workspace, with source or
design rationale, before any saved game data changes.
