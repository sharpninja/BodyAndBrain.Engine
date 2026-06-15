# Status Effects

Status effects are ongoing conditions on a combatant: damage over time or a
control penalty. They are applied by spells, by critical bleeds, or directly, and
they are resolved one round at a time by a tick command.

## The canonical statuses

There are six, and only six, canonical statuses. The engine normalizes any input
to one of these (case-insensitive) and ignores anything else as non-canonical.

| Status | Kind | Resistance stat | Default magnitude | Default rounds |
| --- | --- | --- | ---: | ---: |
| Bleed | Damaging | Constitution | 2 | 3 |
| Poison | Damaging | Constitution | 3 | 3 |
| Disease | Damaging | Constitution | 2 | 3 |
| Stun | Control | Constitution | 0 | 1 |
| Move | Control | Agility | 0 | 1 |
| Curse | Control | Piety | 0 | 3 |

- **Damaging** statuses (Bleed, Poison, Disease) deal their magnitude in hits each
  time they tick.
- **Control** statuses (Stun, Move, Curse) carry no per-round damage; they
  represent a condition (stunned, movement-impaired, cursed) that your game
  interprets. The engine tracks their presence and duration.

## Applying a status

Three paths apply a status:

1. **Directly, via the `apply-condition` action:**

   ```csharp
   await engine.ExecuteActionAsync(
       "apply-condition", caster.Id, target.Id,
       parameters: new Dictionary<string, string>
       {
           ["condition"] = "Poison",   // any canonical status
           ["magnitude"] = "4",        // optional; default per table above
           ["duration"]  = "3",        // optional; default per table above
       });
   ```

   A non-canonical condition string is recorded as a note ("Condition noted.")
   without creating a tracked status.

2. **From a spell** that resolves to ApplyStatus (Cause Bleeding, Infection,
   Nauseate). See [Magic](05-magic.md#status-and-cleanse-spells).

3. **From a bleeding critical** in melee or missile combat: a Slash, Puncture, or
   Claw critical with a positive per-round value imposes a **Bleed** automatically.
   See [Combat](04-combat.md#4-critical-resolution).

When applied, a status is stored on the combatant with its type, magnitude,
remaining rounds, and the resistance stat for that type. Statuses persist on the
record between actions.

## Ticking statuses

Statuses do not resolve on their own. Your turn loop advances them by sending a
tick command, typically once per round per afflicted combatant:

```csharp
var tick = await engine.TickStatusEffectsAsync(target.Id);
```

For each active status, one tick does the following:

1. **Apply damage.** If the status is damaging, its magnitude is subtracted from
   the target's hits (accumulated across all damaging statuses, applied once,
   clamped at 0).
2. **Roll resistance.** Roll a d100 (or use `rollOverride`), add
   `StatBonus(resistanceStat) + resistanceBonus`, and read the outcome on the
   standard [roll-outcome table](01-concepts.md#dice-and-roll-outcomes).
3. **Resolve.**
   - If the outcome is a **Hit or better** (the target *resists*), the status is
     **cleared** immediately.
   - Otherwise the status's remaining rounds decrease by one; at zero rounds it is
     removed.

So a successful resistance shrugs the status off entirely, while a failed
resistance lets it run down its clock (and, for damaging types, deal another tick
next round).

> **Natural 1 and 100.** Because resistance reuses the attack-outcome table, a
> natural 1 always fails (the status persists) and a natural 100 always succeeds
> (the status clears), regardless of the resistance bonus. This is handy for
> deterministic tests: `rollOverride: 1` forces persistence and damage;
> `rollOverride: 100` forces a clear.

## The tick result

A tick returns a YAML result whose `outcome.statusTicks` lists, per status, the
damage applied, the resistance stat, the roll and total, whether it resisted, the
rounds remaining, and whether it is still active. The target's surviving statuses
are echoed under a `status.active` block, and any hit loss appears in
`stateChanges`. The schema is in
[Actions and Results](07-actions-and-results.md#status-tick-result).

## A worked example

A target is hit by `Cause Bleeding`, applying Bleed(magnitude 2, 3 rounds),
resisted by Constitution.

- **Round 1 tick, roll 30** (low Constitution, total in the Hit/2 band, not a
  Hit): the target loses 2 hits; Bleed persists with 2 rounds left.
- **Round 2 tick, roll 60** (Hit band): the target *resists*; Bleed is cleared,
  no further damage.

Had every roll stayed low, Bleed would have dealt 2 hits per round for 3 rounds
and then expired.

## Designing with statuses

- The engine never auto-ticks. Decide your cadence (usually one tick per
  combatant at the start or end of its round) and drive it from your scheduler.
- Control statuses (Stun, Move, Curse) are signals: read `NpcRecord.Statuses` (or
  `PlayerCharacterRecord.Statuses`) to know a combatant is stunned or impaired and
  apply that in your turn logic.
- `resistanceBonus` on a stored status defaults to 0. If you want gear or traits to
  aid resistance, set it when you construct the status (or model it in your own
  layer); the tick adds it to the resistance roll.
