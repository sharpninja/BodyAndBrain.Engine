# Functional Requirements (MCP Server)

## FR-ACTIONS-001 YAML action execution results

Every executable game action must return a YAML result document containing actor, target, inputs, rolls, modifiers, outcome, state changes, diagnostics, and adjudication flags.
**Acceptance Criteria:**
- [x] Incomplete mechanics return requiresAdjudication true with a stable reason code. (evidence: src/BodyAndBrain.Engine/ActionExecutor.cs)
- [x] Every defined action, spell, race, profession, weapon, armor, and item is covered in unit tests. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)

## FR-ADJUDICATION-001 Adjudication for underspecified mechanics

The engine must return successful YAML action results with requiresAdjudication true and stable reason codes when canonical sources do not define deterministic mechanics.
**Acceptance Criteria:**
- [x] Underspecified spell actions return requiresAdjudication true with adjudication-spell. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] Adjudicated actions avoid unintended state mutation. (evidence: src/BodyAndBrain.Engine/ActionExecutor.cs)

## FR-CHARACTER-001 PC and NPC lifecycle

The library must create player characters, apply level-up actions, generate NPCs by race/profession/level, render NPC sheets as markdown, and persist PC/NPC structured data in LiteDB.
**Acceptance Criteria:**
- [x] PC and NPC records round-trip through LiteDB. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] NPC generation supports all valid race/profession combinations at levels 1 through 50. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)

## FR-CI-001 Repository validation and CI gate

The repository must carry a repeatable validation gate and Azure DevOps CI definition that executes the solution test suite.
**Acceptance Criteria:**
- [x] Azure Pipelines runs dotnet test BodyAndBrain.Engine.slnx on the self-hosted Default pool. (evidence: azure-pipelines.yml)
- [x] The local solution gate passed with 9 tests, 0 failures, and 0 skipped. (evidence: docs/Parity-Report.md)

## FR-DATA-001 Embedded canonical game data

The library must load embedded YAML data using manual text/effects and workbook numeric overlays from BodyAndBrain.xlsm.
**Acceptance Criteria:**
- [x] Embedded YAML loads into an immutable catalog. (evidence: src/BodyAndBrain.Engine/GameDataCatalog.cs)
- [x] Coverage validation reports all supported races, professions, spells, weapons, armor, items, maneuvers, actions, NPC baselines, and monsters. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)

## FR-ENGINE-001 CQRS-backed game engine operations

BodyAndBrain.Engine must expose game operations as SharpNinja.FeatureFlags.Cqrs commands and queries, with all state-changing behavior performed by command handlers.
**Acceptance Criteria:**
- [x] Public operations dispatch through IDispatcher.SendAsync or QueryAsync in tests. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] No separate action dispatcher exists outside the FeatureFlags CQRS system. (evidence: src/BodyAndBrain.Engine/Handlers.cs)

## FR-MONSTERS-001 Monster baseline catalog and overdrive

The canonical catalog must include traditional monsters with logical professions and exceptional monsters with overdriven governed skill effects.
**Acceptance Criteria:**
- [x] Monster definitions are stored in embedded YAML and enumerated by tests. (evidence: src/BodyAndBrain.Engine/Data/bodyandbrain.game.yaml)
- [x] Lich, Demon, Dragon, and Vampire entries include governed skill effect x1.5 overdrive. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)

## FR-NPC-001 NPC level scaling and markdown sheets

The engine must generate NPCs for every valid race/profession combination on a 1-50 level scale, applying age and level-significance scaling, and render NPC sheets in markdown.
**Acceptance Criteria:**
- [x] NPC generation supports all valid race/profession combinations at levels 1, 5, and 50. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] RenderNpcMarkdownQuery returns a markdown sheet for persisted NPCs. (evidence: src/BodyAndBrain.Engine/NpcGenerator.cs)

