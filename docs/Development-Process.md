# BodyAndBrain.Engine Development Process

This repo follows Byrd Dev Process v4 for the Body and Brain rules engine.

## Traceability

Initial MCP records were created for:

- `FR-ENGINE-001`: CQRS rules engine operations
- `FR-DATA-001`: Embedded canonical game data
- `FR-CHARACTER-001`: PC/NPC lifecycle and LiteDB persistence
- `FR-ACTIONS-001`: Action execution returns YAML result documents
- `TR-ENGINE-001`: Local FeatureFlags CQRS project reference
- `TR-DATA-001`: YAML schema and source manifest
- `TEST-CQRS-001`: Dispatch-level CQRS tests
- `TEST-COVERAGE-001`: Catalog inventory coverage tests

## Gates

1. Foundation Gate: Git, solution, process docs, MCP traceability, and restore.
2. CQRS Integration Gate: FeatureFlags project reference, service registration, and dispatch-level command/query tests.
3. Data Contract Gate: Embedded YAML schema, loader, manifest, and diagnostics.
4. Canonical Data Gate: Inventory coverage for canonical definitions.
5. Character/Persistence Gate: LiteDB PC creation, load, level-up, and action-log records.
6. NPC Gate: race/profession/level generation and markdown rendering.
7. Action Engine Gate: dispatch-level execution for defined actions with deterministic YAML result shape.
8. Completion Gate: zero failed tests and zero skipped tests.

## Current Scope Boundaries

The engine executes deterministic mechanics where the canonical manual/workbook provides enough detail. Mechanics that remain underspecified are intentionally represented as successful CQRS results with `requiresAdjudication: true`; handlers must not mutate state for those cases.

The convenience `IBodyAndBrainEngine` client exists for consumers, but it delegates to `IDispatcher` and does not bypass CQRS.
