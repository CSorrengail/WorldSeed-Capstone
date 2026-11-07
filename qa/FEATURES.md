# Implemented feature inventory and QA scope

Source inspection, 2026-11-06; checked against code rather than the early README roadmap.

| Implemented behavior | Source | Automated evidence |
|---|---|---|
| Generic schemas, fragments, type expressions, rules, procedures, references | SchemaValidation, schemas/validation, schemas/examples | Existing schema tests plus fictional corpus and malformed-document regressions |
| Original UTF-8 TXT import, source identity and provenance | SourceIngestion, SourcePassages | Existing importer tests and six independent fictional documents with exact-text disk roundtrips |
| Drafting conversations and clarifications; structured extraction with literal citations | RuleDrafting, DesignSessions | Existing service/parser tests; fabricated evidence and conflicting-source contract tests |
| Working proposal batching, citations, optional schema hints | WorkingRules | Existing validator/formalization regression suites; no model agreement oracle |
| Schema discovery, translation, merging, queued changes and persisted clarification | SchemaTranslation | Existing translation/discovery/change-request suites; headless clarification input |
| Schema activation and revision-bound data, instance editing, structural constraints | GameData, GameDataValidator, MainWindow | Service, independent numeric/text oracles, and real API/Mongo lifecycle tests |
| Mongo-backed projects, rules, proposals, change requests, active snapshots and data | Persistence.Mongo, Api | In-process real API host against disposable MongoDB, read/write/rejection/cascade tests |
| Settings, startup chooser, project creation, four workspace tabs, diagnostics | App | Actual XAML controls, simulated keyboard/mouse, real Skia output, minimum/default sizes, Computer Use review |
| HTTP clients, Ollama/model discovery and timeouts | LlmIntegration, App/Http* | Existing deterministic protocol tests plus malformed catalog and literal evidence regressions |

The UI is mostly event-driven code-behind; it has no view-model data binding layer to pretend to test. UI tests exercise the controls and handlers that exist. Further end-to-end source drafting with a live model requires separate observational evaluation: preserve raw output, validate exact citations and explicit numeric facts, classify clarification/format/timeout failures, and never turn a second model's agreement into a passing assertion.

Advanced balance analysis is explicitly disabled as “Later” in MainWindow.axaml. There is no export workflow or general content-generation service. Canonical JSON instance editing and schema/rule proposal generation are implemented; tests do not claim those unimplemented features work.

Residual scope limits: the initial suite samples UI states and workflows; it does not exhaust screen readers, high-DPI/multi-monitor themes, operating-system file pickers, all concurrency interleavings, or semantic accuracy of live LLM outputs. Coverage reports make missing execution visible; they do not certify completeness.
