# Creature MVP Demonstration Results

**Verified revision:** `03e8ba1`  
**Execution:** deterministic fixture run; no model, API, database, Docker, or
user project data was used.

The deterministic two-system evidence is supplemented by a production-shaped
connected Emberbound run at tested revision `6887ab9` (current equivalent
`c569cbc` after local history consolidation). That QA run used the actual
Content Review controls and production generation path with local
`gemma3:12b`, isolated Docker API/Mongo persistence and reload, validation and
balance presentation, designer revision/approval/rejection, and approved export
provenance. See `artifacts/qa/connected-desktop-6887ab9/report.md`.

Run the evidence generator with:

```powershell
dotnet run --project tools/WorldSeed.EndToEnd/WorldSeed.EndToEnd.csproj -c Release --no-build -- --creature-mvp --output artifacts/mvp-demonstration/creature-mvp-results.json
```

The generated result is intentionally ignored by Git because it contains
run-specific proposal IDs and timestamps.

## Observable Results

| System | Valid Creature | Invalid Creature | Balance-Concern Creature | Mechanical Difference |
|---|---|---|---|---|
| Emberbound | 0 violations; 0 concerns; approved and exported | 1 structural violation; rejected | 0 violations; 2 advisory concerns; revision requested | Numeric tactical vitality, guard, technique impact, and technique count |
| Quiet Horizons | 0 violations; 0 concerns; approved and exported | 1 structural violation; rejected | 0 violations; 2 advisory concerns; revision requested | Narrative tags, scene prompt, response, and escalation-clock pressure |

For each fixture the runner creates a proposal tied to the fixture's working
rule set and active schema revision. It evaluates that proposal with the
fixture's own profile, records the appropriate designer decision, and exports
only the approved version. The export includes candidate content, game-schema
ID/version, Working Rule Set ID/revision, proposal ID/version, and decision
provenance.

## What This Demonstrates

- Source passages and Working Rule IDs remain the declared evidence for each
  game-specific creature profile.
- Schema constraints cause hard mechanical violations; advisory reference bands
  remain non-fatal balance concerns.
- The same generic services support a tactical numerical game and a narrative
  non-dice game without a universal creature, challenge-rating, or balance
  formula.
- Approval, rejection, revision request, and approved export are distinct
  actions; no proposal action writes canonical game data.

## Limits

This is not a claim that live LLM output is always correct. The connected run is
headless and isolated; it does not claim manual launcher use, rendered visual
review, real user data, or the interactive save picker. Broader retrieval,
generated system design, applying approved proposals to canonical Game Data,
and expanded human-readable export remain future work outside this MVP.
