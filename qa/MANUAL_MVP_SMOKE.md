# Normal Launcher MVP Smoke Check

Use an existing disposable or test project that already has a Working Rule Set,
an active schema with a content type, and (for balance results) a matching
game-specific balance profile.

1. Run `RunWorldSeed.bat` and choose the test project.
2. In **Content Review**, select a local model and content type, enter a short
   request, and choose **Generate Proposal**.
3. Confirm the saved candidate is selectable and both **Mechanical Validation**
   and **Balance Evaluation** show an understandable result.
4. Request a revision with a note, generate its replacement, and approve the
   replacement. Generate a separate proposal and reject it.
5. Export the approved proposal. Confirm the JSON contains the candidate,
   schema and Working Rule Set revisions, proposal version, and approved
   decision.
6. Close WorldSeed normally, run `RunWorldSeed.bat` again, reopen the same
   project, and confirm the proposals and decisions reload. Closing the first
   window must not cause the launcher to report that WorldSeed is still running.

Report the first step that differs from the expected result and include the
visible status text. The application log is under
`%LOCALAPPDATA%\WorldSeed\logs`.

## November 19 Changes — Compact Checks

Owner checks remain on the backlog until the demonstration workflow is ready.
Each row is independent; API/infrastructure checks belong to QA.

| Change | Check | Expected |
|---|---|---|
| Draft uncertainty (`339137f`) | Add missing draft rules, then refresh Project Inventory. | Source-draft questions survive as unresolved observations; existing rules stay intact. |
| Focused structures (`6213cf2`) | In Schema, build discovered game structures. | Each content-type plan builds one type; rejected model output is retried with its actual errors. |
| Reader headings (`6c073fa`) | In Publication, create a fresh snapshot/edition containing an entry without a name/title. | Numbered entry heading, no technical entry ID; named entries keep their labels and manifests retain IDs. |
| Evaluation context (`117d333`) | In Validation And Balance, request coverage for a type whose rules were added late. | Relevant later rules appear in citations; the proposal still requires review. |
| Proposal identity (`1002594`) | QA: submit an update with a changed proposal ID, then the original ID. | Changed ID gets 409 without mutation; stable-ID update succeeds. |
| Bounded SDK commands (`61e9a48`) | QA: `python -m unittest discover -s qa -p test_bounded_dotnet.py -v`. | Four mocked checks pass without launching .NET; restore/build limits cannot be overridden. |
| Proposed expansion mechanics (`07d97c7`) | In Planning, start an accepted rule-system expansion; choose or revise a proposed mechanic in its clarification. | Options are clearly proposals. Drafting uses the recorded choice and cites the saved Q&A, excluding rejected alternatives. Ordinary source intake stays faithful. |
| Planner evidence (`3aef256`) | In Planning, request a plan focused on a specific unresolved question in a large inventory. | The question remains eligible despite many source-coverage rows; recommendation categories match their cited evidence. |
| Structured expansion choices | Start a fresh accepted rule-system expansion. Choose A, choose B, revise an option, or reject both in the conversation. | Each initial question shows two distinct proposed mechanics and their tradeoffs. No mechanic becomes a draft before a saved answer; ordinary uploaded notes keep their existing behavior. |
| Narrative rules and publication (owner check deferred) | After incorporating a prose-only expansion, choose No Schema Change Required with a reason. In Schema, review the prepared update, then activate it. Create a fresh publication. | Intended result: new rules without schema regeneration or lost entries, using separate review. Current corpus blocker: the preparation guard still rejects legitimate applied schema-change citations; fix before owner testing. |
