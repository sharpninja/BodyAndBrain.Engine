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
| NPC baselines | 46 |
| Monsters | 36 |
| Actions | 141 |
| Known gaps | 1 |

Coverage status:

| Area | Status |
| --- | --- |
| CQRS dispatch | All engine tests use `IDispatcher.SendAsync` or `IDispatcher.QueryAsync`. |
| Character persistence | PC create, load, level-up, NPC persist/load, and action-log writes are covered. |
| NPC generation | All valid race/profession pairs generate through CQRS at levels 1, 5, and 50. |
| Actions | Every catalog action dispatches through CQRS and emits the required YAML result shape. |
| Defined inventory | Race, profession, apprenticeship, spell, weapon, armor, item, maneuver, NPC baseline, and monster definitions are enumerated by tests. |
| Underspecified mechanics | Spell/item effects without deterministic canonical rules return `requiresAdjudication: true` with stable reason codes. |

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
