# BodyAndBrain.Engine Agent Instructions

Complete the user's request.

Do not fabricate information. If you made a mistake, acknowledge it. Distinguish facts from speculation.

Prioritize correctness over speed. Do not ship code you have not verified compiles and is logically sound.

## Workspace

- Repository: `F:\GitHub\BodyAndBrain.Engine`
- Source rules/manual repository: `F:\GitHub\BodyAndBrain`
- CQRS dependency repository: `F:\GitHub\FeatureFlags`
- MCP marker: `AGENTS-README-FIRST.yaml`

## Build And Test

- Restore/build/test with `dotnet` from `F:\GitHub\BodyAndBrain.Engine`.
- Main solution: `BodyAndBrain.Engine.slnx`
- Required validation gate: `dotnet test BodyAndBrain.Engine.slnx`
- Target framework: `net10.0`

## Implementation Rules

- All game operations run through `SharpNinja.FeatureFlags.Cqrs.IDispatcher`.
- Do not create a separate dispatcher or action bus.
- Engine commands mutate PC/NPC/action-log state; queries are read-only.
- Incomplete or unreconciled mechanics return successful YAML action results with `requiresAdjudication: true` and a stable reason code.
- Game data is embedded YAML under `src\BodyAndBrain.Engine\Data\bodyandbrain.game.yaml`.
- BodyAndBrain.xlsm is canonical for workbook-derived numeric overlays; manual labels/effects express intent when sheet values conflict.
- LiteDB records must remain structured, not opaque YAML blobs, except for persisted action result documents.

## Process

Use Byrd Dev Process v4:

- Capture FR/TR/TEST records before broad implementation.
- Work in gated slices.
- Keep tests dispatch-level for engine mechanics.
- Every defined action, spell, race, profession, weapon, armor, item, maneuver, NPC baseline, and monster must be covered by unit tests.
