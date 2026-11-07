# QA configuration completion report

This continuation was configuration-only. No application fixes, feature changes,
application tests, comprehensive suite, corpus generation, desktop launch, live
model call or autonomous QA loop was performed. No framework was reinstalled.

## Ready for delegation

The project-scoped `qa_engineer` definition is installed in both the development
repository (`WorldSeed-Capstone`) and the preserved QA worktree (`WorldSeed-QA`).
It uses the current standalone TOML format: name, description and
developer_instructions. Model and permissions inherit from the parent. Project
configuration enables agents with two spawned threads in addition to the parent.
Routine QA is strictly non-implementing, accepts parent assignments/follow-ups,
and returns evidence-backed findings directly to that parent.

The installed Codex CLI is 0.164.0-alpha.11.1. Its strict startup-configuration
validation succeeded for the development repository without starting an app
server or model. A configuration-only prompt render also succeeded and included
the repository's QA instructions. TOML/schema and project-file inventory checks
passed. An actual custom-agent model dispatch was intentionally not started.
Use a fresh conversation rooted in the development repository for delegation.

Custom-agent files do not independently enforce filesystem permissions; the
non-implementation policy is explicit in the agent's developer instructions.
Parent-provided sandbox controls remain authoritative. This is documented rather
than presenting an ignored sandbox_mode setting as a security boundary.

## Files created or updated

Created in both repositories:

- `.codex/config.toml`: supported project agent settings.
- `.codex/agents/qa_engineer.toml`: role, write restrictions, testing scope,
  isolation requirements and direct parent handoff.
- `qa/SUBAGENT.md`: invocation examples, parent testing brief and integration limits.
- `qa/REPORT_TEMPLATE.md`: required tested revision, exact commands/cwd/exit codes,
  counts, skipped/blocked checks, defects, reproductions, severity and corrections.
- `qa/verify_setup.py`: non-executing configuration/infrastructure inventory.
- `qa/infrastructure-location.json`: explicit sibling-harness location; no
  automatic forwarding of development tests to another revision.
- `qa/SETUP_REPORT.md`: this report.

Updated QA-worktree guidance and copied the applicable handoff into development:

- `AGENTS.md`: replaces earlier QA repair authority with report-only delegation.
- `qa/RUNBOOK.md`: parent-scoped, non-implementing execution and safe environments.
- `qa/STATUS.md`: preserves historical results and records outstanding setup gaps.
- `qa/FEATURES.md`: preserved feature inventory, accessible from development.

QA-worktree-only infrastructure changes:

- `qa/run_qa.py`: records executed command argument lists/cwd; routine run status
  and desktop evidence now live under artifacts/qa rather than editing tracked
  handoff files. Report text hands repairs to the parent. The existing functional
  runner and tests were retained.
- `qa/prepare_integration.py`: exports reviewable patches and checks applicability;
  it does not apply, merge, commit, reset or discard anything.

No production source, application configuration, tracked test definitions or
approved screenshot baselines were changed by this continuation. The earlier
application edits remain preserved in the QA worktree and are not silently
approved or merged into development.

## Minimum checks executed

Actual checks, run from the QA worktree unless a different cwd is shown:

| Command | Result |
|---|---|
| `python qa/verify_setup.py` | PASS: TOML, Python syntax, solution/project XML, existing fixture readability and isolation configuration |
| `python ../WorldSeed-Capstone/qa/verify_setup.py` | PASS: development agent configuration can access the explicitly identified preserved harness |
| `python -m unittest discover -s qa -p test_run_qa.py` | PASS: 3 reporting infrastructure self-tests; temporary files only |
| `codex --version` | Existing CLI confirmed; no install/update |
| `codex -C C:/Users/Varian/Desktop/Capstone/WorldSeed-Capstone debug prompt-input <configuration-only message>` | Exit 0: local prompt/config render; no model call |
| `codex -C C:/Users/Varian/Desktop/Capstone/WorldSeed-Capstone app-server --strict-config --print-startup-config` | Exit 0: supported configuration accepted; server not started |
| `python qa/prepare_integration.py --target ../WorldSeed-Capstone` | PASS: all three generated patches passed `git apply --check`; none applied |
| `git diff --check` in both repositories | Whitespace validation only |

Initial diagnostic attempts exposed an unsupported --strict-config option on the
debug subcommand and sandbox access limits on Codex cache files. The supported
app-server strict-validation command and authorized configuration-only execution
then succeeded. These were configuration diagnostics, not skipped application
tests. Setup evidence is under the QA worktree's artifacts/qa/setup directory.

## Isolation and preserved state

Source inspection and the preflight verified random worldseed_qa_<id> database
names, mandatory WORLDSEED_QA_MONGO with no development fallback, labelled
volume-free Mongo containers, ephemeral loopback ports, separate app settings/log
directories, isolated HTTP handlers and temporary artifacts. No database/service
was started for these checks. The single labelled `worldseed-qa-initial` container
left by the interrupted run was ownership-checked and stopped; no normal stack
was touched. No WorldSeed desktop process was running when inspected.

The QA application source fingerprint remains
`394ca5ce472c8e44852168ca47c20b59189bd4ee96f3b87016812b8a645b8d9d`, matching
the last pre-interruption run. Development source/tests remain unchanged by this
continuation. The development branch was not merged, checked out, reset or stashed.

## Integration and remaining limits

Prepared package:
`C:/Users/Varian/Desktop/Capstone/WorldSeed-QA/artifacts/qa/integration/`.
Read README.md and manifest.json there. It separates QA infrastructure, prior
application edits (test seams plus earlier functional repairs), and prior test
formatting. Agent/configuration/handoff files already present in development are
identified and omitted from those patches when identical.

The complete harness has not been merged. New headless tests require application
test seams from the earlier work; the implementing agent must review compatible
prerequisites before integration. QA must report an unavailable revision/harness
as BLOCKED instead of silently testing the sibling snapshot.

Historical evidence only: the last saved comprehensive run recorded 247/247
passing cases, no skips, and 2989/4278 instrumented source lines covered. It also
recorded REVIEW_REQUIRED because approved screenshot baselines and a finalized,
current desktop-review record were missing. Those gaps remain. This continuation
did not rerun or complete visual baseline approval, live UI assessment or broad
application testing, and makes no new application-quality claim.

Configuration is ready for parent delegation. Full harness integration and visual
approval remain separate review work. See SUBAGENT.md for the exact invocation.
