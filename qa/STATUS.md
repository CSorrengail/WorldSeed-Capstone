# QA configuration handoff

Routine QA is non-implementing and parent-scoped. The reusable qa_engineer custom agent is installed in this repository's .codex/agents directory. See SUBAGENT.md, RUNBOOK.md and REPORT_TEMPLATE.md.

Focused Avalonia headless testing is integrated in this checkout through `tests/WorldSeed.Qa.Tests`. It uses process-local data-directory overrides and has no database, model, or normal-launcher dependency.

The broader test harness remains in the WorldSeed-QA worktree, branch qa/autonomous-system. Its last saved comprehensive run (before this configuration-only continuation) is 20261107T052705Z-1da172: 247 tests passed, no failures/skips, 2989/4278 instrumented lines covered. These are historical results for that dirty QA snapshot, not a new run or a claim about the development revision.

No new comprehensive suite, application fixes, synthetic corpus, desktop session or live model call is authorized by this setup continuation. Only configuration inventory, report self-tests, Codex agent discovery and patch applicability checks are performed.

Outstanding: initial approved screenshot baselines, a finalized desktop-review record, and review of the broader sibling harness remain incomplete. Historical candidates, screenshots, logs and prior application edits are preserved. See SETUP_REPORT.md and the prepared integration manifest.

Routine outputs and current run status go under artifacts/qa; QA must not edit this tracked handoff, tests, application files or approved baselines.
