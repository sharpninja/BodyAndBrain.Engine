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

## FR-ENGINE-ROLL-001 Client roll override

IBodyAndBrainEngine.ExecuteActionAsync accepts an optional rollOverride parameter and forwards it to ExecuteGameActionCommand.RollOverride.
**Acceptance Criteria:**
- [ ] ExecuteActionAsync exposes rollOverride and forwards it to the dispatched command.

## FR-MONSTERS-001 Monster baseline catalog and overdrive

The canonical catalog must include traditional monsters with logical professions and exceptional monsters with overdriven governed skill effects.
**Acceptance Criteria:**
- [x] Monster definitions are stored in embedded YAML and enumerated by tests. (evidence: src/BodyAndBrain.Engine/Data/bodyandbrain.game.yaml)
- [x] Lich, Demon, Dragon, and Vampire entries include governed skill effect x1.5 overdrive. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)

## FR-MONSTERSPAWN-001 Dispatched monster generation

A dispatched GenerateMonsterCommand(monsterId, level) ICommand of NpcRecord produces an NpcRecord with IsMonster true from the monster catalog, scaling canonical monster stats by level.
**Acceptance Criteria:**
- [ ] GenerateMonsterCommand resolves every catalog monster id and returns IsMonster true with the Monster name set.
- [ ] Monster stats scale from the canonical level-5 baseline by significance and clamp 1-100.

## FR-NPC-001 NPC level scaling and markdown sheets

The engine must generate NPCs for every valid race/profession combination on a 1-50 level scale, applying age and level-significance scaling, and render NPC sheets in markdown.
**Acceptance Criteria:**
- [x] NPC generation supports all valid race/profession combinations at levels 1, 5, and 50. (evidence: tests/BodyAndBrain.Engine.Tests/EngineTests.cs)
- [x] RenderNpcMarkdownQuery returns a markdown sheet for persisted NPCs. (evidence: src/BodyAndBrain.Engine/NpcGenerator.cs)

## FR-NPC-FALLBACK-001 NPC baseline derivation fallback

NPC generation produces a result for any canonical race/profession combination using a documented derivation fallback (profession baseline array plus race stat bonuses) when no exact baseline exists.
**Acceptance Criteria:**
- [ ] Every race by profession combination generates an NPC at levels 1, 5, and 50.
- [ ] Derived combinations are flagged as derived and clamp stats 1-100.

## FR-OVERDRIVE-001 Monster overdrive x1.5

During action resolution, monsters whose overdriven stat is set (Lich, Demon, Dragon, Vampire) apply a governed-stat x1.5 multiplier to the effect of skills governed by that stat, changing the effect not the roll, stat, skill, or hit total.
**Acceptance Criteria:**
- [ ] An overdriven monster governed-stat action effect such as damage is multiplied by 1.5 after the ordinary result.
- [ ] Non-governed actions and non-overdriven monsters are unaffected; YAML surfaces an overdrive block.

## FR-PHYSATTACK-001 Canonical physical attack resolution

ExecutePhysicalAttack computes BaB-canonical accuracy (weapon skill bonus plus governing-stat bonus added to the roll), critical resolution per the Slash/Crush/Puncture/Claw tables by weapon attack profile and defender armor, and armor protection (canonical Base Attack Bonus accuracy mitigation plus a documented engine-derived protectionPercent), surfacing accuracy, critical, and protection in the YAML result.
**Acceptance Criteria:**
- [ ] YAML outcome includes accuracy with roll, weaponSkillBonus, governingStatBonus, baseAttackBonus, and total.
- [ ] Critical hits resolve a crit type (Slash/Crush/Puncture/Claw) with extra damage and per-round bleed from the BaB tables.
- [ ] Protection block reports armorProfile, accuracyMitigation, protectionPercent, and model, and damage is reduced by protectionPercent.

## FR-SPELL-001 Spell action resolution

Catalog spell actions (kind spell) resolve damage, heal, or effect per BaB spell definitions through the existing hit-application path instead of always adjudicating; genuinely underspecified spells keep requiresAdjudication.
**Acceptance Criteria:**
- [ ] Damage spells roll the modifier dice with per-level scaling and apply damage to the target.
- [ ] Heal and restore spells apply healing; Life Drain damages target and heals caster half.
- [ ] Underspecified spells (summon, animate, control) keep requiresAdjudication true with a stable reason.

## FR-STATUS-001 Status-effect state and resolution

apply-condition creates mechanically effective status state for Bleed, Poison, Disease, Stun, Move, and Curse, with per-round tick application and resistance-roll resolution persisted on the target record.
**Acceptance Criteria:**
- [ ] apply-condition records structured status state on the target with magnitude and duration.
- [ ] A per-round tick applies status damage or effect and resolves a resistance roll that can end the status.
- [ ] Statuses persist on PC and NPC records in LiteDB and surface in the YAML status block.

