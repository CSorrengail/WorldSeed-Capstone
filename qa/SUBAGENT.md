# Invoke qa_engineer from another Codex conversation

`qa_engineer` is the QA specialist in a three-agent workflow: the parent plans and
coordinates, the implementing agent changes code, and QA independently tests and
reports. QA never implements corrections or modifies tracked tests or baselines.

## Project discovery and invocation

Open the actual development repository `WorldSeed-Capstone` in a fresh Codex
conversation, or launch Codex with `-C` pointing to that directory. Starting in the
parent `Capstone` directory does not make a nested project's agent configuration
active. The project has:

- `.codex/agents/qa_engineer.toml`: standalone custom-agent definition.
- `.codex/config.toml`: agents enabled, up to two spawned threads plus the parent.
- `AGENTS.md`: delegation boundary and links to this handoff.

Ask the parent conversation:

> Delegate to the project custom agent qa_engineer. Test revision <commit> in
> <prepared worktree>, focusing on <feature and acceptance criteria>. Run only
> <specified tests/workflows>, within <time budget>. Store evidence under
> artifacts/qa/<task-id>. Return findings directly. Do not implement changes.

For a configuration-only check:

> Ask qa_engineer to read its role and report its allowed writes and report
> requirements. Do not build, run tests, generate data or launch the app.

No separate API key, agent SDK application, global installation or global agent
registration is needed. Codex discovers the project TOML files. A parent runtime
that exposes a named custom-agent selector should spawn `qa_engineer` through that
selector, then collect the result and send follow-up testing instructions normally.
Do not confuse the subagent name with a model name, skill or slash command. A
session started before configuration was added may need a fresh conversation.

The current supported format requires top-level `name`, `description` and
`developer_instructions`. Model settings are omitted to inherit the parent's
selection. Custom agents inherit permissions, provider and MCP servers. In
particular, adding sandbox_mode="read-only" to this file would not create an
enforced read-only role. The no-implementation boundary is explicit agent policy;
an OS-enforced boundary requires parent-provided sandbox/filesystem controls.
Test/build/temp outputs still need a writable isolated directory. See the
[official subagent documentation](https://learn.chatgpt.com/docs/agent-configuration/subagents).

## Testing handoff contract

Supply the requested revision, actual prepared checkout, changed features,
acceptance criteria, bounded commands/workflows and permitted services. QA returns
the actual tested revision/dirty state, commands/cwd/exit codes, counts, skipped or
blocked checks, evidence, defects with reproduction/severity/impact and recommended
corrections. [REPORT_TEMPLATE.md](REPORT_TEMPLATE.md) defines the full result.
The parent sends repairs to the implementing agent and may explicitly assign a
new bounded verification after implementation. QA does not start that next cycle
or delegate more agents on its own.

## Where the existing harness lives

The interrupted setup created the full harness in the sibling `WorldSeed-QA`
worktree on branch `qa/autonomous-system`. It contains the runner, ten .NET test
projects, headless/Skia tests, isolated MongoDB integration, fixtures and reports.
The development repository receives the agent/configuration/handoff files now;
the earlier application edits and full harness are preserved separately for
integration review. `qa/infrastructure-location.json` identifies the sibling.

Run `python qa/verify_setup.py` for a configuration-only inventory. It explicitly
reports which checkout holds the harness. It never runs tests against another
checkout on behalf of development code. Until integration is reviewed, existing
development tests can be assigned normally; tests needing new headless hooks must
run on a parent-prepared compatible snapshot or be reported BLOCKED.

The integration directory named in SETUP_REPORT.md contains reviewable patches
and a file manifest. Do not blindly apply or cherry-pick all earlier changes:
the new tests depend on application isolation/test seams, and the earlier run also
made functional fixes. Those earlier application edits are not newly approved or
applied to the development branch by this configuration task.

## Safe execution and limits

Follow [RUNBOOK.md](RUNBOOK.md) in the harness for isolated execution. Routine QA
may write only temporary inputs, probes, build products, reports, screenshots,
logs and coverage under artifacts/qa/<task-id> or unique temporary directories.
It cannot change production source, application configuration, tracked fixtures
or tests, approved baselines, repository instructions or agent configuration.

Never use normal app launchers, port 5080, real user folders or development Mongo
volumes. Parent-prepared resources or the harness create random, owned QA
databases/containers, ephemeral loopback ports and isolated app data. Missing
permissions/prerequisites are reported, not bypassed. Do not reinstall functioning
components. Do not claim visual review unless screens were rendered and inspected.

Baseline approval and a current, finalized live-desktop report were not completed
before interruption. Preserve the candidates and report that gap; routine QA must
not promote images or rewrite expected outcomes to pass.
