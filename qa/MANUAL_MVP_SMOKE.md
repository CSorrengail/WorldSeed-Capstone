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
