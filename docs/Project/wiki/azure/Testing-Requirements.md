# Testing Requirements (MCP Server)

## TEST-ACTION

### TEST-ACTION-001

Tests must dispatch every catalog action through IDispatcher and assert the required YAML result shape.

**Acceptance Criteria:**
- [x] Every catalog action dispatches through CQRS and returns actionId, actor, target, inputs, rolls, modifiers, outcome, stateChanges, diagnostics, and requiresAdjudication. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)


## TEST-CI

### TEST-CI-001

The solution test gate must pass with zero failed tests and zero skipped tests.

**Acceptance Criteria:**
- [x] dotnet test BodyAndBrain.Engine.slnx passes with 9 tests, 0 failures, and 0 skipped. (evidence: docs/Parity-Report.md)


## TEST-COVERAGE

### TEST-COVERAGE-001

Unit tests must enumerate the embedded catalog and fail if any race, profession, apprenticeship, spell, weapon, armor, item, maneuver, NPC baseline, monster, or action lacks coverage.

**Acceptance Criteria:**
- [x] Unit tests enumerate races, professions, apprenticeships, spells, weapons, armor, items, maneuvers, NPC baselines, monsters, and actions. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] Catalog validation fails on missing or inconsistent action references. (evidence: src/BodyAndBrain.Engine/GameDataCatalog.cs)


## TEST-CQRS

### TEST-CQRS-001

Unit tests must dispatch all command/query behavior through IDispatcher, including sample command failure, read-only query behavior, and action YAML results.

**Acceptance Criteria:**
- [x] Command and query behavior dispatches through IDispatcher.SendAsync or QueryAsync. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] CQRS command failure, query reads, action YAML, and persistence behaviors are covered. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)


## TEST-NPC

### TEST-NPC-001

Tests must cover valid race/profession combinations, boundary levels, markdown rendering, monster catalog, and exceptional overdrive.

**Acceptance Criteria:**
- [x] Every valid race/profession combination generates at levels 1, 5, and 50. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)


## TEST-PERSISTENCE

### TEST-PERSISTENCE-001

Tests must verify create, load, update, NPC persistence, and action-log behavior through CQRS-dispatched operations.

**Acceptance Criteria:**
- [x] PC create/load/level-up and NPC action mutation round-trip through LiteDB. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
