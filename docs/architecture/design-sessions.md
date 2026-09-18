# Design Sessions

A design session is the persistent working space for a designer’s early conversation with WorldSeed. It sits after model communication and structured drafting, but before approval and canonical schema translation.

```text
original source note
      ↓
designer / model conversation
      ↓
clarifying question or structured rule draft
      ↓
saved design session
      ↓
later canonical change proposal
```

## Stored information

A session records its project identifier, selected model-profile identifier, original source notes, ordered conversation messages, and latest validated drafting turn. It never stores an API key, a canonical revision, or a decision to apply a change.

The model’s original response is retained as an assistant message so later turns have the same context the model saw. The parsed `LatestTurn` gives the future GUI a safe, structured representation to display.

## Local persistence

`JsonDesignSessionStore` saves one session as a JSON document using an atomic temporary-file replacement. The future GUI should provide an application-data directory; the library deliberately does not hard-code a repository location or assume that designers want work-in-progress conversations committed to Git.

## GUI boundary

The future GUI can start a session from a note, add a designer reply, request the next model turn, save, and reopen. Those operations are exposed by `DesignSessionService`; screens do not need to know prompt details, provider protocols, or JSON parsing rules.
