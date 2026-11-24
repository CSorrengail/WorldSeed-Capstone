# Normal Launcher MVP Smoke Check

For the latest manual bug-hunting pass, close WorldSeed normally and run
`RunWorldSeed.bat --rebuild` once to update both the desktop and Docker API. Later
launches can use the normal command. Use an existing project or a disposable
project you create in the app; ignored QA state JSON is not a GUI import format.
Manual checks are bug-hunting evidence, not a claim that the complete MVP or
presentation has already been accepted.

## Project Rules And Hover Help — 2026-11-24

Close WorldSeed and run `RunWorldSeed.bat --rebuild` once. Keep existing Sources;
they do not need to be processed again for this change.

- **Missing-rule recovery:** In Rules, use **Add New Source Rules** (formerly
  Add Missing Draft Rules). Expect omitted model citations to be retried
  automatically. Saved project rules must account for every new source rule;
  existing rules should remain intact. A persistent model failure names the
  input rule and leaves saved project rules unchanged.
- **Terminology help:** Hover the `ⓘ` icons beside Working Rule Set,
  Source-Draft Rules, Proposal Setup, Active Schema, and Content Types. Expect
  short explanations of what that workspace uses and what its artifacts mean.
  Rules also offers **Rebuild Project Rules** and **Reload Saved Rules**; rebuilding
  replaces from sources and is distinct from adding only new source rules.

## Fresh Source Intake Repair — 2026-11-23

Close the app and run `RunWorldSeed.bat --rebuild` once before these checks.

### Saved Review Actions — 2026-11-24

- **Answer a saved review:** Select Skills/Tools/Checks or Gear/Weapons/Magic.
  Review items should appear above the source text with answer boxes. Enter
  clarifications and use **Save Answers And Retry Source**. Expect saved answers
  and a regenerated draft; unresolved material can still be flagged honestly.
- **License material:** On Travel/Exploration/Environment, use **Mark As Non-Game
  Material** for the license notice. Expect that notice to leave the review list
  immediately, with original text and valid rules preserved. Reopen the project
  and retry: its classification should remain saved.
- **Retry:** **Retry Selected Source** should process an existing saved draft
  using its answers, without the old “already has a session” refusal. A pending
  interview instead shows **Resume Selected Source** and its answer controls.

- **Missing passages:** Retry Experience And Levels and Fights And Turns. Expect
  valid rules to survive an omitted passage. Any omission remaining after the
  focused retry must appear with original text in Conversation as needing review,
  rather than a fatal coverage error or a claim of complete extraction.
- **Source status:** Hover the fixed icon left of a filename. Expect status in
  the tooltip: `○` not started, `✓` draft ready, `!` answers or review needed.
  The filename no longer has a status suffix.
- **Bounded extraction:** Retry Ancestry and Advancement from a fresh source
  session. Expect passage progress followed by a combined draft, without the
  previous whole-file truncation. Compare the rules against the source facts;
  passage coverage alone does not prove semantic completeness. Larger files now
  require several model calls, so processing may take longer.
- **Task questions:** Retry Classes And Training. Expect extracted rules or
  questions about specific missing mechanics, never what to do with the text or
  which aspect interests you. Answer any genuine clarification and continue.
- **Source evidence:** Select rules from the beginning and end of a processed
  file. Expect original excerpts and permanent source references, with no
  temporary `n1`/`p1` aliases in saved rule evidence.

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
| Reference/link schema translation (`2abe3ad`) | Request an optional reference to an existing content type and an explicitly named relationship in Schema. | Entity references use actual entity types; the relationship uses declared endpoints. Existing entries survive a compatible update. |
| Omitted-link refusal (`268a985`) | In Content Review, explicitly request a declared relationship by name. Inspect proposed links and the item effect before approval. | A link omitted without a reported gap is refused; the app must not silently save it as a complete proposal. Reject or revise unrequested mechanics such as invented usage limits. |
