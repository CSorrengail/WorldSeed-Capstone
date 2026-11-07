# WorldSeed QA execution runbook

Routine QA is strictly non-implementing. The parent assigns a tested revision and scope; qa_engineer runs checks and returns findings. The implementing agent owns repairs, tracked test changes, application configuration and baseline approval. The earlier autonomous repair loop is superseded. Read [SUBAGENT.md](SUBAGENT.md), [STATUS.md](STATUS.md), [FEATURES.md](FEATURES.md), and [REPORT_TEMPLATE.md](REPORT_TEMPLATE.md).

## Verify setup without application QA

    python qa/verify_setup.py
    python -m unittest discover -s qa -p test_run_qa.py

The verifier parses configuration and the focused in-repository test project. It does not install, build, run application tests, generate a corpus, start Docker, launch a desktop or call a model. Stop after this setup check unless the parent assigns further testing.

Prerequisites already used: .NET SDK 10 (slnx support), .NET 8 runtime, Python 3.10+, Docker Desktop Linux engine with mongo:7.0. Avalonia 12.1.2 headless uses xUnit v3; older suites retain v2. Preserve installed versions.

## Execute only the assigned scope

First record actual cwd, git rev-parse HEAD, git branch --show-current, git status --short and relevant dirty files/source fingerprint. A sibling QA checkout with earlier application edits is a different tested snapshot even when HEAD matches. The parent prepares the intended snapshot with compatible infrastructure; never silently substitute another checkout.

For a focused Avalonia assignment, run the selected headless test against the exact development checkout with an isolated results directory and data path:

    $env:WORLDSEED_DATA_DIRECTORY = (Join-Path (Resolve-Path artifacts/qa/<task-id>) 'data')
    $env:WORLDSEED_QA_SCREENSHOTS = (Join-Path (Resolve-Path artifacts/qa/<task-id>) 'screenshots')
    dotnet test tests/WorldSeed.Qa.Tests/WorldSeed.Qa.Tests.csproj --no-restore -m:1 --filter FullyQualifiedName~SchemaChangeClarificationWindowTests --logger trx --results-directory artifacts/qa/<task-id>/tests

Use appropriate process-local WORLDSEED_* overrides. If binaries are missing/stale, build only the assigned snapshot or report BLOCKED. Do not install packages or alter source/configuration to overcome failures. Formatting is verification-only (--verify-no-changes).

The broader runner in the sibling `WorldSeed-QA` worktree is not a substitute for this checkout and must not be used to verify a development revision until its separate application dependencies are reviewed and integrated. Its `Invoke-Qa.ps1` command is intentionally not installed here.

This bounded runner generates temporary cases, restores/builds, enables .NET analysis with warnings as errors, verifies whitespace, starts its own MongoDB, runs existing tests with coverage, captures Skia PNGs, compares approved baselines and cleans up its container. It writes artifacts/qa/<run-id>/report.md and results.json. Current status is artifacts/qa/STATUS.md; tracked qa/STATUS.md remains the setup handoff. Exact command argument lists/cwd are recorded. The agent supplements the report with unexecuted requested checks, reproduction steps, severity and recommended corrections.

Exit 0 means every configured gate passed; 1 means failure/block; 2 means review is required. Missing baselines or live-review evidence remain visible. Do not change assertions, outcomes, thresholds or approved images to obtain green. Return defects to the parent; do not begin repairs or another cycle.

DOTNET_PROCESSOR_COUNT=2, -m:1 and disabled build servers bound worker creation. -p:UsedAvaloniaProducts= suppresses package build-statistics writes while retaining XAML compilation and analyzers. Docker/formatting may require inherited permissions outside a restricted sandbox. Report denied checks as blocked.

## Isolation

- Never use RunWorldSeed.bat, the normal compose stack, port 5080, development volumes, real project folders or real preferences.
- The runner starts a uniquely named, worldseed.qa=true-labelled MongoDB container with no volume and a random loopback port. API tests require WORLDSEED_QA_MONGO and choose random worldseed_qa_<id> databases; no normal-database fallback exists. Remove only generated owned resources. Never globally prune Docker or drop a caller-supplied database.
- Headless views receive rejecting/isolated HTTP handlers. Data/log/preferences use WORLDSEED_DATA_DIRECTORY or unique worldseed-qa temporary directories. WORLDSEED_API_URL and WORLDSEED_OLLAMA_URL select QA services. Never set overrides globally. Isolation prevents legacy preferences redirecting imports outside QA data.
- Temporary sources, probes, screenshots, logs and reports go under artifacts/qa/<task-id> or unique temporary directories. Tracked fixtures, test definitions, application configuration and production code stay unchanged.
- Observe protected files before/after. Report concurrent drift rather than restoring, stashing or discarding another agent's work.

## Visual and live checks

Avalonia.Headless uses the actual theme/fonts and Skia with UseHeadlessDrawing=false. Comparison checks dimensions without resizing/alignment: a channel delta >16 marks a changed pixel; >0.1% changed pixels fails. Approved images require matching SHA256 and approval metadata. QA captures candidates but cannot promote/approve them or modify metadata. Initial baseline approval is still outstanding.

For parent-requested desktop checks, read the Computer Use skill and use python qa/desktop_session.py with its isolated API, database and app data. Closing that owned desktop cleans up its API/container. Actually inspect rendered screens before making visual claims. Record accessibility, clipping, navigation, error handling and limitations. The runner cannot invoke Computer Use itself. New live-review evidence belongs in artifacts/qa/desktop-review.json and the task report, including source fingerprint, timestamp, performed checks and screenshot paths. No tracked file needs editing. Screenshots do not establish screen-reader or high-DPI behavior.

Live LLM testing is a separate assignment. The existing WorldSeed.EndToEnd tool uses three small fictional scenarios (procedural, ambiguous, narrative), without saving user data. Expected actions/citations test protocol behavior, not semantic correctness. Preserve raw outputs and report meaning changes, variability and timeouts separately. Another model's agreement is never the oracle.

Return the English report directly to the parent and stop after the assigned scope.
