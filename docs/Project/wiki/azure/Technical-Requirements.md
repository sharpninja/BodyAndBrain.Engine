# Technical Requirements (MCP Server)

## TR-CI-AZURE-001

**Azure DevOps CI** — The repo must include an Azure Pipelines definition that restores and tests the engine using the self-hosted Default pool and sibling FeatureFlags checkout.
**Acceptance Criteria:**
- [x] azure-pipelines.yml uses pool Default and runs dotnet test BodyAndBrain.Engine.slnx. (evidence: azure-pipelines.yml)

## TR-DATA-LITEDB-001

**Structured LiteDB persistence** — PC, NPC, and action-log data must be persisted in LiteDB as structured records.
**Acceptance Criteria:**
- [x] LiteDB collections persist players, NPCs, and action logs with indexed IDs. (evidence: src/BodyAndBrain.Engine/GameStore.cs)

## TR-DATA-YAML-001

**Embedded YAML schema and diagnostics** — Canonical data must be embedded as YAML with a source manifest, schema models, loader, and validation diagnostics.
**Acceptance Criteria:**
- [x] Embedded resource loads into GameDataCatalog and validates references. (evidence: src/BodyAndBrain.Engine/GameDataCatalog.cs)

## TR-ENGINE-DI-001

**Engine dependency injection registration** — Engine services must register catalog, LiteDB store, dice, markdown renderer, action executor, convenience client, and CQRS handlers through Microsoft.Extensions.DependencyInjection.
**Acceptance Criteria:**
- [x] AddBodyAndBrainEngine registers services and calls AddCqrs with the engine handler assembly. (evidence: src/BodyAndBrain.Engine/ServiceCollectionExtensions.cs)

## TR-ENGINE-NET-001

**Pure net10 library and package metadata** — BodyAndBrain.Engine must be a pure net10.0 class library with package metadata and no application host requirement.
**Acceptance Criteria:**
- [x] Library target framework is net10.0. (evidence: src/BodyAndBrain.Engine/BodyAndBrain.Engine.csproj)

