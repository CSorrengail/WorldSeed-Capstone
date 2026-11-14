# QA configuration handoff

Routine QA is non-implementing and parent-scoped. The reusable qa_engineer custom agent is installed in this repository's .codex/agents directory. See SUBAGENT.md, RUNBOOK.md and REPORT_TEMPLATE.md.

Focused Avalonia headless testing is integrated in this checkout through `tests/WorldSeed.Qa.Tests`. Connected checks use isolated temporary data directories, ephemeral API ports, random Mongo databases, volume-free labelled containers, and fixture projects. They never use the normal launcher, port 5080, development volumes, or owner project data.

Current verified application evidence is summarized in `docs/project-status.md`; detailed run reports remain under ignored `artifacts/qa`. The latest bounded content-expansion milestone passed deterministic Content Review and domain suites, clean Release builds, and an isolated real-`gemma3:12b`/API/tmpfs-Mongo smoke. The normal-launcher Publication review is also complete and produced the verified legacy-snapshot guidance correction.

Schema evolution is now verified through deterministic review, an explicit
two-step desktop activation workflow, and immutable transition history. The
isolated Mongo/API run retained exact revision-1/revision-2 schema and Game Data
snapshots, preserved authored entries through a compatible update, and cleaned
history on project deletion. Evidence is under
`artifacts/qa/20261112-schema-transition-*`.

The typed designer-idea source path is verified deterministically and through the real local-model source-to-Working-Rule-Set protocol. Connected .NET tools must be built with bounded workers and executed as DLLs; do not use `dotnet run` for connected QA on this Windows environment. See `qa/RUNBOOK.md`.

The broader historical QA worktree remains reference material only. Its older comprehensive result does not verify later development revisions. Approved visual-regression baselines and broad accessibility/high-DPI coverage remain optional future QA work, not blockers for the currently verified workflows.

Routine outputs and current run status go under artifacts/qa; QA must not edit this tracked handoff, tests, application files or approved baselines.
# 2026-11-12 - Connected Structured Schema Translation

Revision `ed2ff8e` passed the bounded connected designer-path retest. The real
`gemma3:12b` flow used the discovered Habitat plan's single Working Rule,
supplied a native response schema on every translation call, persisted and
reloaded the resulting proposal, and did not activate it. Evidence:
`artifacts/qa/20261112-schema-structured-output-connected/report.md`.
