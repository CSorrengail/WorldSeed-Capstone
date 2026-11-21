# Normal Launcher MVP Smoke Check

Use an existing disposable or test project that already has a Working Rule Set,
an active schema with a content type. A game-specific balance profile is optional;
balance evaluation is the first stretch goal under decision 0016.

1. Run `RunWorldSeed.bat` and choose the test project.
2. In **Content Review**, select a local model and content type, enter a short
   request, and choose **Generate Proposal**.
3. Confirm the saved candidate is selectable and both **Mechanical Validation**
   shows an understandable result. **Balance Evaluation** may say Not Configured;
   that must not block the core workflow.
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
| Narrative rules and publication (`00eccae`; owner check deferred) | After incorporating a prose-only expansion into a project with an applied designer schema change, choose No Schema Change Required with a reason. In Schema, review the prepared update, then activate it. Create a fresh publication. | The saved designer-change citations remain valid. Separate review/activation updates the rule context without regenerating structures or losing entries; the new publication includes the added rules. |
| Nested content fields (`6f4257a`) | In Content Review, generate an entry whose type has a record field, such as a card with suit and value. | The model receives the nested field names, allowed entries, and constraints; ordinary validation still checks its result. |
| Nested reader output (`3f25d3d`) | In Publication, create an edition containing a nested record or list of records. | Player output uses field labels and category names instead of raw record JSON; technical snapshot/provenance remains available. |
| Retained schema work (`1ae8f05`) | Add missing Working Rules to a project with a saved schema. For a prose-only expansion, record No Schema Change Required, then review and activate the prepared update. | Existing schema work stays visible. Its older binding cannot be activated; the reviewed update preserves structures and entries. |
| Rule reconciliation (`299a8f9`) | In Rules, select related rules, give any resolution guidance, then Propose Rule Update. Review before Accept And Apply Rules or Reject Rule Update. | Proposing/rejecting preserves active rules. Acceptance creates one replacement with original evidence and history; stale proposals cannot apply. |
| Reconciliation clarification | If the proposal asks a question, answer in Designer Guidance and propose again. | No rules change before acceptance; the older matching proposal becomes history. |
| Reconciliation schema review | After applying a reconciliation, choose Prepare Schema Review, then inspect/review/activate in Schema. | Schema structure/data remain unchanged until normal activation. Original proposal provenance stays in reconciliation history. |
| Connected content (`9ee34f3`) | With an existing target entry and a schema-defined reference/link, request a new entry using that target. Inspect Related Canonical IDs and Proposed Relationships before approval/application. | Valid targets resolve during preview; entry and approved links apply together. Invalid targets or cardinality prevent application. |
| Connected reader output | Publish the approved connected content. | References and links use content labels; technical IDs remain in the manifest. |
| Mechanical coverage label | Inspect Mechanical Validation in Content Review. | The result identifies declared schema/reference checks and explicitly leaves prose-only conditions for designer review. |
