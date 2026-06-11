# Technical Requirements (MCP Server)

## TR-CI-AZURE-001

**Azure DevOps CI** — The repo must include an Azure Pipelines definition that restores and tests the engine using the self-hosted Default pool and sibling FeatureFlags checkout.
**Acceptance Criteria:**
- [x] azure-pipelines.yml uses pool Default and runs dotnet test BodyAndBrain.Engine.slnx. (evidence: azure-pipelines.yml)

## TR-COMBAT-TABLES-001

**Encode BaB combat tables** — Encode the BaB Base Attack Bonus (weapon by armor), Critical Type (weapon by armor to crit type plus modifier), and Critical/Failure resolution tables (Slash, Crush, Puncture, Claw) from 7-Combat.md in engine mechanics.
**Acceptance Criteria:**
- [ ] Base Attack Bonus, Critical Type, and Critical resolution tables are represented and unit-tested.

## TR-DATA-LITEDB-001

**Structured LiteDB persistence** — PC, NPC, and action-log data must be persisted in LiteDB as structured records.
**Acceptance Criteria:**
- [x] LiteDB collections persist players, NPCs, and action logs with indexed IDs. (evidence: src/BodyAndBrain.Engine/GameStore.cs)

## TR-DATA-YAML-001

**Embedded YAML schema and diagnostics** — Canonical data must be embedded as YAML with a source manifest, schema models, loader, and validation diagnostics.
**Acceptance Criteria:**
- [x] Embedded resource loads into GameDataCatalog and validates references. (evidence: src/BodyAndBrain.Engine/GameDataCatalog.cs)

## TR-ENGINE-CLIENT-001

**Convenience client roll injection** — IBodyAndBrainEngine.ExecuteActionAsync exposes an optional rollOverride and forwards it to ExecuteGameActionCommand.RollOverride so the convenience client can inject deterministic rolls.
**Acceptance Criteria:**
- [x] ExecuteActionAsync rollOverride is forwarded to the dispatched command and appears in the YAML rolls block. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)

## TR-ENGINE-DI-001

**Engine dependency injection registration** — Engine services must register catalog, LiteDB store, dice, markdown renderer, action executor, convenience client, and CQRS handlers through Microsoft.Extensions.DependencyInjection.
**Acceptance Criteria:**
- [x] AddBodyAndBrainEngine registers services and calls AddCqrs with the engine handler assembly. (evidence: src/BodyAndBrain.Engine/ServiceCollectionExtensions.cs)

## TR-ENGINE-NET-001

**Pure net10 library and package metadata** — BodyAndBrain.Engine must be a pure net10.0 class library with package metadata and no application host requirement.
**Acceptance Criteria:**
- [x] Library target framework is net10.0. (evidence: src/BodyAndBrain.Engine/BodyAndBrain.Engine.csproj)

## TR-MONSTER-DATA-001

**Enrich monster catalog data** — Enrich MonsterDefinition and embedded YAML with the canonical monster stat block, overdriven stat name, and hits ported from the 8-Non-Player_Characters monster table.
**Acceptance Criteria:**
- [ ] Each monster carries six stat scores, hits, and an overdriven stat name where applicable.

## TR-SPELL-RESOLVE-001

**Spell modifier resolution** — Parse spell modifier dice with per-level scaling to resolve damage, heal, and effect outcomes for catalog spell actions.
**Acceptance Criteria:**
- [ ] Modifier dice including per-level scaling are parsed and rolled to produce spell damage or heal amounts.

## TR-STATUS-MODEL-001

**Status model persistence** — Persist status-effect state on PC and NPC LiteDB records with per-round tick application and resistance-roll resolution.
**Acceptance Criteria:**
- [ ] Status state round-trips through LiteDB and ticks resolve resistance rolls.

