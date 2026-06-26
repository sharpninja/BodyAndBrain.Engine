# BodyAndBrain.Engine Parity Report

Source policy: manual text/effects with workbook numeric overlays from `F:\GitHub\BodyAndBrain\BodyAndBrain.xlsm`.

Embedded catalog counts:

| Catalog | Count |
| --- | ---: |
| Races | 8 |
| Professions | 9 |
| Apprenticeships | 19 |
| Skills | 48 |
| Spell lists | 10 |
| Spells | 70 |
| Weapons | 10 |
| Armor definitions | 10 |
| Items | 35 |
| Maneuvers | 12 |
| NPC baselines | 36 |
| Monsters | 36 |
| Actions | 142 |
| Known gaps | 1 |

Coverage status:

| Area | Status |
| --- | --- |
| CQRS dispatch | All engine tests use `IDispatcher.SendAsync` or `IDispatcher.QueryAsync`. |
| Character persistence | PC create, load, level-up, NPC persist/load, and action-log writes are covered. |
| NPC generation | Every race/profession combination generates through CQRS at levels 1, 5, and 50, with a documented derivation fallback for non-baseline pairs (FR-NPC-FALLBACK-001). |
| Monster generation | `GenerateMonsterCommand` instantiates every catalog monster as an `IsMonster` combatant with canonical stats scaled by level (FR-MONSTERSPAWN-001). |
| Physical attacks | Accuracy (weapon skill + governing stat + base attack bonus), Slash/Crush/Puncture/Claw critical resolution, and armor protection (canonical accuracy mitigation + engine-derived percent) are surfaced in YAML (FR-PHYSATTACK-001). |
| Spell resolution | Catalog spells resolve damage/heal/life-drain/status/cleanse/effect through the hit-application path; underspecified spells keep adjudication (FR-SPELL-001). |
| Status effects | Bleed, Poison, Disease, Stun, Move, and Curse apply structured state, tick per round, and resolve resistance rolls (FR-STATUS-001). |
| Overdrive | Lich/Demon/Dragon/Vampire apply governed-stat x1.5 to action effects during resolution (FR-OVERDRIVE-001). |
| Actions | Every catalog action dispatches through CQRS and emits the required YAML result shape. |
| Defined inventory | Race, profession, apprenticeship, spell, weapon, armor, item, maneuver, NPC baseline, and monster definitions are enumerated by tests. |
| Underspecified mechanics | Spell/item effects without deterministic canonical rules return `requiresAdjudication: true` with stable reason codes. |
| CI validation | Azure Pipelines uses the self-hosted `Default` pool and runs `dotnet test BodyAndBrain.Engine.slnx`. |

Exceptional monsters:

| Monster | Entries | Overdrive |
| --- | ---: | --- |
| Lich | 2 | Governed skill effect x1.5 |
| Demon | 2 | Governed skill effect x1.5 |
| Dragon | 2 | Governed skill effect x1.5 |
| Vampire | 2 | Governed skill effect x1.5 |

Validation command:

```powershell
dotnet test BodyAndBrain.Engine.slnx
```

Latest local result: 23 passed, 0 failed, 0 skipped (includes exhaustive table-driven tests for scenarios).
