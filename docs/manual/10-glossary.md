# Glossary

Terms used throughout this manual, in the precise sense the engine gives them.

**Action.** An executable verb in the catalog (attack, spell, maneuver, equip,
use item, or system op). Run with `ExecuteGameActionCommand`. 142 ship. See
[Actions and Results](07-actions-and-results.md).

**Action log.** The append-only `action_logs` collection recording every executed
action with its YAML result. A replayable audit trail.

**Adjudication.** The engine's signal that an action is defined but has no
deterministic rule in the canonical sources. Returns success with
`requiresAdjudication: true` and a stable reason code; your game resolves it. Not
an error. See [Core Concepts](01-concepts.md#adjudication).

**Age modifier.** A level-driven adjustment that pushes Body and Brain stats in
opposite directions during NPC/monster generation. See
[Core Concepts](01-concepts.md#the-age-modifier).

**Apprenticeship.** A sub-specialization tied to one profession, required at
character creation.

**Array.** The role grouping of an NPC baseline (Physical, Skirmisher, Scholar,
Devout, Leader). Monsters use the array `monster`; derived fallbacks use `derived`.

**Base Attack Bonus (BaB).** The accuracy modifier from the canonical table indexed
by weapon attack profile against the defender's armor column. The way BaB models
armor: as accuracy mitigation. See [Combat](04-combat.md#the-base-attack-bonus-table).

**Body stats.** Strength, Agility, Constitution. Contrast Brain stats.

**Brain stats.** Intelligence, Presence, Piety. Contrast Body stats.

**Catalog.** The embedded, read-only YAML game data
(`Data/bodyandbrain.game.yaml`): races, professions, spells, weapons, monsters,
actions, and more. Loaded by `IGameDataCatalog`.

**Class bonus.** A narrative special ability on a profession. Stored as text; the
engine does not enforce it. Your game applies it.

**Combatant.** The engine's internal wrapper over a player character or NPC/monster
so combat can read stats and persist hits uniformly.

**Command.** A CQRS operation that mutates state (create, level up, generate,
execute, tick). Dispatched with `SendAsync`, returns `Result<T>`.

**Control status.** A non-damaging status (Stun, Move, Curse) representing a
condition your game interprets. Contrast damaging status.

**CQRS.** Command/Query Responsibility Segregation. All engine operations flow
through `IDispatcher` as commands or queries.

**Critical.** Extra effect on a Hit + Critical (or 2x) outcome: immediate bonus
damage and, for Slash/Puncture/Claw types, an ongoing Bleed. See
[Combat](04-combat.md#4-critical-resolution).

**Critical score.** `attackerDie(1D4) + critModifier - defenderDie(1D4)`, clamped to
the resolution table, selecting the critical's immediate and per-round values.

**Damaging status.** Bleed, Poison, or Disease: deals its magnitude in hits each
tick. Contrast control status.

**Derived baseline.** A runtime-generated NPC stat block for a race/profession
combination that has no canonical baseline. Flagged `Derived = true`.

**Determinism.** The property that identical inputs yield identical outputs. Every
randomized command accepts a `RollOverride`; the only randomness source is
`IDiceRoller`. See [Core Concepts](01-concepts.md#determinism-and-roll-overrides).

**Discipline.** The casting tradition of a spell list (Wizardry, Conjuring,
Prayers, Paladin, Ranger, Bard).

**Hits.** The health pool, `CurrentHits` of `MaxHits`. Clamped to `[0, MaxHits]`.

**Level significance.** A signed triangular measure of a level's distance from the
baseline level 5; scales NPC/monster stats and hits. See
[Core Concepts](01-concepts.md#levels-and-level-significance).

**Maneuver.** A skill action (Climb, Hide, Locks, etc.) that succeeds on a Hit
outcome and reports a tactical effect without directly changing hits.

**Modifier (spell).** The catalog text (`1D10`, `1D6 per 5 Levels`, `+5 per 5
Levels`) that `RollScaledModifier` turns into a level-scaled magnitude.

**Overdrive.** An exceptional monster trait that multiplies the effect of one
governing stat by 1.5 when an action's governing stat matches it. See
[NPCs and Monsters](03-npcs-and-monsters.md#overdrive-exceptional-monsters).

**Outcome band.** The result tier (Fumble, Miss, Hit/2, Hit, Hit+Critical, 2x
Hit+Critical) chosen by the natural roll and the bonus-adjusted total.

**Profile (armor).** An armor's mechanical class (None, Robes, Soft, Rigid, Chain,
Plate, Helm, Shield), mapping to an attack-table column and a protection percent.

**Profile (attack).** A weapon's mechanical class (1H Edge, 2H Conc, Longbow,
Claws, etc.) used to index the combat tables.

**Protection percent.** An engine-added damage-reduction overlay by armor profile.
Not canonical BaB; provided for consumers that want armor to soak damage.

**Query.** A read-only CQRS operation. Dispatched with `QueryAsync`, returns
`Result<T>`.

**Resolution kind.** How the spell resolver classifies a spell: Damage, Heal,
LifeDrain, ApplyStatus, Cleanse, Effect, or Adjudicate. See
[Magic](05-magic.md#resolution-kinds).

**Resistance roll.** A d100 plus `StatBonus(resistanceStat) + resistanceBonus`,
read on the standard outcome table, that a status target makes each tick; a Hit or
better clears the status. See [Status Effects](06-status-effects.md#ticking-statuses).

**Roll override.** A supplied natural d100 (`RollOverride`) that fixes an action's
randomness, including derived critical dice. The basis of determinism.

**Signature.** An NPC's or monster's headline skill rating (for example
`Primary Melee 5`); combat reads its digits when no explicit weapon skill is given.

**Stat bonus.** The non-linear bonus derived from a stat score (0 to +10) that
feeds rolls. See [Core Concepts](01-concepts.md#the-stat-bonus-table).

**Status effect.** An ongoing condition (Bleed, Poison, Disease, Stun, Move, Curse)
tracked on a combatant and advanced by `TickStatusEffectsCommand`.

**YAML action result.** The structured document returned by every executed action,
with a fixed top-level shape, both machine- and human-readable. See
[Actions and Results](07-actions-and-results.md#the-result-schema).
