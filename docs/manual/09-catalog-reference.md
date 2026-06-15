# Catalog Reference

Every piece of game data shipped in the embedded catalog
(`Data/bodyandbrain.game.yaml`). All ids are case-insensitive; most lookups accept
either the id or the display name. The catalog is read-only at runtime; edit the
YAML and rebuild to change content, then run `ValidateGameDataQuery`.

- [Source manifest](#source-manifest)
- [Races](#races)
- [Professions](#professions)
- [Apprenticeships](#apprenticeships)
- [Skills](#skills)
- [Spell lists](#spell-lists)
- [Spells](#spells)
- [Weapons](#weapons)
- [Armors](#armors)
- [Items](#items)
- [Maneuvers](#maneuvers)
- [NPC baselines](#npc-baselines)
- [Monsters](#monsters)
- [Action ids](#action-ids)

## Source manifest

| Field | Value |
| --- | --- |
| Manual repo | `F:\GitHub\BodyAndBrain` |
| Workbook | `BodyAndBrain.xlsm` |
| Policy | Manual text/effects with workbook numeric overlays |

The manual and workbook are the canonical sources. Where the workbook supplies a
number and the manual supplies intent, the manual's labels and effects win on
conflict.

## Races

Stat bonuses applied to the baseline of 50 at creation:

| Race | Str | Agi | Con | Int | Pre | Pie |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Human | +2 | +1 | +1 | 0 | 0 | 0 |
| Half-Elf | +1 | +2 | +1 | +1 | +1 | -2 |
| Elf | 0 | +3 | 0 | +2 | +2 | -3 |
| Dwarf | +2 | -1 | +2 | 0 | 0 | +1 |
| Halfling | -1 | +2 | 0 | +1 | +2 | 0 |
| Reptilian | +2 | -1 | +1 | 0 | 0 | +2 |
| Orc | +3 | 0 | +2 | -3 | -1 | 0 |
| Feline | -1 | +3 | 0 | 0 | +3 | -1 |

Resistances (stored for downstream use):

| Race | Poison | Disease | Wizardry | Conjuring | Curses |
| --- | ---: | ---: | ---: | ---: | ---: |
| Human | 0 | 1 | 0 | 0 | 0 |
| Half-Elf | 1 | 1 | 0 | 0 | -1 |
| Elf | 1 | 2 | 0 | 0 | -2 |
| Dwarf | 2 | 1 | -1 | 0 | -1 |
| Halfling | 0 | 0 | 0 | 0 | 1 |
| Reptilian | 1 | 2 | -2 | 0 | 0 |
| Orc | 2 | 2 | -2 | -1 | 0 |
| Feline | 0 | 0 | 1 | 0 | 0 |

Valid professions per race:

| Race | Valid professions |
| --- | --- |
| Human | Bard, Berserker, Cleric, Conjuror, Fighter, Paladin, Ranger, Rogue, Wizard (all) |
| Half-Elf | Bard, Conjuror, Fighter, Ranger, Rogue, Wizard |
| Elf | Bard, Conjuror, Ranger, Rogue, Wizard |
| Dwarf | Berserker, Cleric, Conjuror, Fighter, Paladin, Wizard |
| Halfling | Bard, Cleric, Conjuror, Ranger, Rogue, Wizard |
| Reptilian | Berserker, Cleric, Conjuror, Fighter, Paladin, Wizard |
| Orc | Fighter, Berserker, Cleric |
| Feline | Bard, Conjuror, Ranger, Rogue, Wizard |

## Professions

| Id | Name | Primary stat |
| --- | --- | --- |
| fighter | Fighter | Strength |
| berserker | Berserker | Strength |
| paladin | Paladin | Piety |
| ranger | Ranger | Agility |
| rogue | Rogue | Agility |
| bard | Bard | Presence |
| wizard | Wizard | Intelligence |
| conjuror | Conjuror | Intelligence |
| cleric | Cleric | Piety |

Each profession also carries a narrative `classBonus` (see
[Character Creation](02-character-creation.md#professions)); the engine stores the
text but does not enforce it.

## Apprenticeships

| Apprenticeship | Profession |
| --- | --- |
| Actor | Bard |
| Fencing | Bard |
| Politician | Bard |
| Lycanthropy | Berserker |
| Wrestling | Berserker |
| Monk | Cleric |
| Priesthood | Cleric |
| Undead Hunter | Cleric |
| Illusions | Conjuror |
| Necromancy | Conjuror |
| Soldier | Fighter |
| Squire | Fighter |
| Knight | Paladin |
| Animist | Ranger |
| Tracker | Ranger |
| Burglar | Rogue |
| Pick Pocket | Rogue |
| Alchemy | Wizard |
| Scholarship | Wizard |

## Skills

Skills are rated 0 to 10. The combat-relevant ones are `Primary Melee` and
`Primary Missile`; the rest support maneuvers and your own systems. Governing stat
shown where defined (`-` means none assigned).

| Skill | Stat | Skill | Stat |
| --- | --- | --- | --- |
| Primary Melee | Strength | Primary Missile | Agility |
| Secondary Melee | Strength | Secondary Missile | Agility |
| Tertiary Melee | Strength | Tertiary Missile | Agility |
| Perception | Presence | Body Development | Constitution |
| Sprint | Agility | Run | Constitution |
| Swim | Agility | Climb | Strength |
| Traps | Presence | Locks | Intelligence |
| Item Lore | Intelligence | Read Runes | Intelligence |
| Use Item | Presence | Persuasion | - |
| Enchantment | Intelligence | Conjuring | Presence |
| Prayer | Piety | Leadership | Piety |
| Naturalist | Intelligence | Songs | Presence |
| Stealing | - | Tracking | - |
| Hiding | - | Balancing | - |

Armor-proficiency skills (None/Light Clothing, Robes, Soft Leather, Rigid Leather,
Chain, Plate, Helm, Shield) are also present for systems that model armor training.

## Spell lists

| List | Discipline | Stat | Restrictions |
| --- | --- | --- | --- |
| Body | Wizardry | Intelligence | No Armor, No Helms, No Shields, Staff |
| Mind | Wizardry | Presence | No Armor, No Helms, No Shields, Staff |
| Summoning | Conjuring | Intelligence | No Metal, No Helms, No Shields, Totem |
| Destroy | Conjuring | Presence | No Metal, No Helms, No Shields, Totem |
| Blessings | Prayers | Piety | No Helms, Sigil |
| Curses | Prayers | Piety | No Helms, Sigil |
| Leadership | Paladin | Piety | Sigil |
| Naturalist | Ranger | Intelligence | No Metal, No Helms, No Shields, Totem |
| Songs | Bard | Presence | No Helms, No Shields, Instrument |
| Necromancer | Conjuring | Intelligence | No Metal, No Helms, No Shields, Totem, Necromancy Apprenticeship |

## Spells

All 70 spells. The action id for each is `cast-spell-<id>`. "Mod" is the modifier
fed to level scaling; "Resolves as" is the [resolution kind](05-magic.md#resolution-kinds).

### Body (Wizardry, Intelligence)

| Id | Name | Lvl | Type | Mod | Duration | Resolves as |
| --- | --- | ---: | --- | --- | --- | --- |
| body-nauseate | Nauseate | 1 | Directed | 1D4/target | 30 seconds | ApplyStatus (Poison) |
| body-bolts | Bolts | 3 | Directed | 1D10 | Immediate | Damage |
| body-hasten | Hasten | 5 | Self | - | 5 minutes | Effect |
| body-blasts | Blasts | 7 | Directed | 1D8/target | Immediate | Damage |
| body-crush | Crush | 9 | Directed | 2D10 | Immediate | Damage |
| body-displace | Displace | 12 | Directed | - | Immediate | Adjudicate |
| body-break-bonds | Break Bonds | 20 | Self | - | Immediate | Adjudicate |
| body-dissentegration | Dissentegration | 50 | Directed | - | Immediate | Effect (see note) |

> Note: the level-50 instant-kill spell is spelled "Dissentegration" in the
> catalog, so it does not match the resolver's "Disintegration" adjudication entry
> and currently resolves as a non-mutating Effect. Treat it as adjudicated in your
> game until the catalog spelling is reconciled.

### Mind (Wizardry, Presence)

| Id | Name | Lvl | Type | Mod | Duration | Resolves as |
| --- | --- | ---: | --- | --- | --- | --- |
| mind-sleep | Sleep | 2 | Directed | - | 10s/Level | Effect |
| mind-fear | Fear | 4 | Directed | - | 10s/Level | Effect |
| mind-focus | Focus | 6 | Directed | - | 10s/Level | Effect |
| mind-confuse | Confuse | 8 | Directed | - | 10s/Level | Effect |
| mind-meditate | Meditate | 10 | Self | - | Immediate | Adjudicate |
| mind-read-mind | Read Mind | 15 | Directed | - | Immediate | Adjudicate |
| mind-mind-control | Mind Control | 30 | Directed | - | 1s/Level | Adjudicate |

### Summoning (Conjuring, Intelligence)

All Summoning spells conjure a creature and are adjudicated.

| Id | Name | Lvl | Conjures |
| --- | --- | ---: | --- |
| summoning-hawk | Hawk | 1 | Level 1 Hawk |
| summoning-dog | Dog | 3 | Level 3 Dog |
| summoning-cougar | Cougar | 5 | Level 5 Cougar |
| summoning-wolf | Wolf | 7 | Level 7 Wolf |
| summoning-bear | Bear | 9 | Level 9 Bear |
| summoning-pegasus | Pegasus | 12 | Level 12 Pegasus |
| summoning-golem | Golem | 20 | Level 20 Golem |
| summoning-dragon | Dragon | 50 | Level 50 Dragon |

### Destroy (Conjuring, Presence)

| Id | Name | Lvl | Mod | Resolves as |
| --- | --- | ---: | --- | --- |
| destroy-will | Will | 2 | 1D4 | Effect (reduces all activities) |
| destroy-mind | Mind | 4 | 1D10 | Effect (reduces mental activities) |
| destroy-body | Body | 6 | 1D10 | Effect (reduces strength bonus) |
| destroy-soul | Soul | 8 | 1D4 | Damage |
| destroy-will-2 | Will 2 | 10 | 1D10 | Effect (area) |
| destroy-mind-2 | Mind 2 | 15 | 1D10 | Effect (area) |
| destroy-body-2 | Body 2 | 30 | 1D4 | Damage |

> Note: Soul and Body 2 are described as per-round damage, but the resolver has no
> spell-driven DoT and applies them as immediate Damage. Model the per-round intent
> yourself if you want it.

### Blessings (Prayers, Piety)

| Id | Name | Lvl | Mod | Resolves as |
| --- | --- | ---: | --- | --- |
| blessings-stop-bleeding | Stop Bleeding | 1 | - | Cleanse (Bleed) |
| blessings-turn-undead | Turn Undead | 3 | 1D6 per 5 Levels | Damage |
| blessings-stop-poison | Stop Poison | 5 | - | Cleanse (Poison) |
| blessings-heal | Heal | 7 | 1D6 per 5 Levels | Heal |
| blessings-stop-disease | Stop Disease | 9 | - | Cleanse (Disease) |
| blessings-bless | Bless | 12 | - | Effect (+15 attacks) |
| blessings-remove-curse | Remove Curse | 20 | - | Cleanse (Curse) |
| blessings-revive-dead | Revive Dead | 50 | - | Adjudicate |

### Curses (Prayers, Piety)

| Id | Name | Lvl | Mod | Resolves as |
| --- | --- | ---: | --- | --- |
| curses-weaken | Weaken | 2 | 1D4 per 5 Levels | Effect (reduces Body bonuses) |
| curses-cause-fear | Cause Fear | 4 | - | Effect (cannot attack) |
| curses-cause-bleeding | Cause Bleeding | 6 | 1D8 per 10 Levels | ApplyStatus (Bleed) |
| curses-remove-blessing | Remove Blessing | 8 | - | Effect (removes blessings) |
| curses-harm | Harm | 10 | 1D10 per 5 Levels | Damage (immediate) |
| curses-curse | Curse | 15 | - | Effect (-15 attacks) |
| curses-infection | Infection | 30 | 1D10 per 10 Levels | ApplyStatus (Disease) |

### Leadership (Paladin, Piety)

| Id | Name | Lvl | Mod | Resolves as |
| --- | --- | ---: | --- | --- |
| leadership-embolden | Embolden | 1 | 1D10 | Effect |
| leadership-restore | Restore | 4 | 1D4 per 4 Levels | Heal |
| leadership-bless | Bless | 7 | - | Effect |
| leadership-antidote | Antidote | 10 | - | Cleanse (Poison) |
| leadership-cure | Cure | 20 | - | Cleanse (Disease) |

### Naturalist (Ranger, Intelligence)

| Id | Name | Lvl | Mod | Resolves as |
| --- | --- | ---: | --- | --- |
| naturalist-track | Track | 1 | 1D10 per 10 Levels | Adjudicate |
| naturalist-sure-shot | Sure Shot | 4 | 1D10 per 10 Levels | Adjudicate |
| naturalist-unseen | Unseen | 7 | 1D10 per 10 Levels | Adjudicate |
| naturalist-bandage | Bandage | 10 | 1D4 per 4 levels | Heal |
| naturalist-discover | Discover | 20 | - | Adjudicate |

### Songs (Bard, Presence)

Songs are deterministic buffs/debuffs (Effect), except Heal (resolves as Heal) and
Reveal (whose name matches the adjudication set, so it resolves as Adjudicate).

| Id | Name | Lvl | Mod | Effect |
| --- | --- | ---: | --- | --- |
| songs-strengthen | Strengthen | 1 | +5 per 5 Levels | Strength bonus to friends |
| songs-quicken | Quicken | 3 | +5 per 5 Levels | Agility bonus to friends |
| songs-fortify | Fortify | 5 | +5 per 5 Levels | Constitution bonus to friends |
| songs-reveal | Reveal | 7 | - | Foes cannot hide (resolves as **Adjudicate**) |
| songs-calm | Calm | 9 | - | Friends +10 to all |
| songs-anxiety | Anxiety | 12 | - | Foes -15 to all |
| songs-heal | Heal | 30 | +5 per 5 Levels | Resolves as **Heal** (named "Heal"; targets the caster) |

### Necromancer (Conjuring, Intelligence; requires Necromancy apprenticeship)

| Id | Name | Lvl | Mod | Resolves as |
| --- | --- | ---: | --- | --- |
| necromancer-grave-sense | Grave Sense | 1 | - | Adjudicate |
| necromancer-death-whisper | Death Whisper | 3 | - | Adjudicate |
| necromancer-bone-servant | Bone Servant | 5 | - | Adjudicate |
| necromancer-command-undead | Command Undead | 7 | - | Adjudicate |
| necromancer-life-drain | Life Drain | 9 | 1D8 per 5 Levels | LifeDrain |
| necromancer-animate-dead | Animate Dead | 12 | - | Adjudicate |
| necromancer-bind-spirit | Bind Spirit | 20 | - | Adjudicate |
| necromancer-lichdom | Lichdom | 50 | - | Adjudicate |

## Weapons

| Id | Name | Attack profile | Damage | Critical | Governing stat |
| --- | --- | --- | --- | --- | --- |
| 1h-edge | 1H Edge | 1H Edge | 1D6 | 5,6 | Strength |
| 2h-edge | 2H Edge | 2H Edge | 2D6 | 10,11,12 | Strength |
| 1h-conc | 1H Conc | 1H Conc | 1D6 | 5,6 | Strength |
| 2h-conc | 2H Conc | 2H Conc | 2D6 | 10,11,12 | Strength |
| thrown | Thrown | Thrown | 1D4 | 4 | Agility |
| crossbow | Crossbow | Crossbow | 1D8 | 7,8 | Agility |
| shortbow | Shortbow | Shortbow | 1D6 | 6 | Agility |
| longbow | Longbow | Longbow | 1D10 | 9,10 | Agility |
| fist | Fist | Fist | 1D4 | None | Strength |
| claws | Claws | Claws | 1D6 | 6 | Agility |

Attack action id is `physical-attack-<profile-slug>` (for example
`physical-attack-2h-conc`). See [Combat](04-combat.md#weapons).

## Armors

| Id | Name | Profile | Metal | Protection overlay |
| --- | --- | --- | --- | ---: |
| none | None | None | no | 0% |
| robes | Robes | Robes | no | 5% |
| cloth | Cloth | Robes | no | 5% |
| soft-leather | Soft Leather | Soft | no | 10% |
| rigid-leather | Rigid Leather | Rigid | no | 15% |
| chain | Chain | Chain | yes | 25% |
| scale | Scale | Chain | yes | 25% |
| plate | Plate | Plate | yes | 40% |
| helm | Helm | Helm | yes | 5% |
| shield | Shield | Shield | yes | 10% |

Equip action id is `equip-armor-<id>`. The protection overlay is an engine-added
percent, not canonical BaB; see [Combat](04-combat.md#6-armor-protection-damage-reduction).

## Items

35 items across three kinds.

- **weapon:** Short Sword, Broadsword, Feline Claws
- **armor:** None, Robes, Cloth, Soft Leather, Rigid Leather, Chain, Scale, Plate,
  Helm, Shield
- **travel:** Backpack, Bedroll, Blanket, Cloak (Hooded), Waterskin, Trail Rations,
  Tinderbox, Torch, Lantern, Oil Flask, Rope (50 feet), Grappling Hook, Tent
  (Small), Cooking Pot, Mess Kit, Whetstone, Sewing Kit, Healer's Kit, Map Case,
  Chalk, Crowbar, Shovel

Use action id is `use-item-<id>`. Most item actions are descriptive (the Torch
action reports the torch is lit/readied); the engine does not model encumbrance,
charges, or consumption: layer those on top if your game needs them.

## Maneuvers

| Id | Name |
| --- | --- |
| running | Running |
| swimming | Swimming |
| climbing | Climbing |
| traps | Traps |
| locks | Locks |
| persuasion | Persuasion |
| item-lore | Item Lore |
| stealing | Stealing |
| tracking | Tracking |
| hiding | Hiding |
| balancing | Balancing |
| general | General |

Maneuver action id is `maneuver-<id>`. A maneuver succeeds on a Hit outcome (roll
override defaults to 50, a Hit) and reports a positional/tactical effect; it does
not by itself mutate hits. See [Actions and Results](07-actions-and-results.md).

## NPC baselines

36 canonical race/profession stat blocks (a level-5 reference each). They are
grouped into **arrays** by role:

| Array | Used by | Profile |
| --- | --- | --- |
| Physical | Fighter, Berserker | High Strength/Constitution; signature Primary Melee 5 |
| Skirmisher | Ranger, Rogue | High Agility; signature Tracking/Missile or Stealing/Hiding 5 |
| Scholar | Wizard, Conjuror | High Intelligence; signature Wizardry/Conjuring 5 |
| Devout | Cleric, Paladin | High Piety; signature Prayer/Leadership 5 |
| Leader | Bard | High Presence; signature Songs/Persuasion 5 |

Combinations with a canonical baseline: every race paired with each of its valid
professions has one (for example `human-fighter`, `elf-wizard`, `orc-cleric`). Any
other rule-legal combination is generated from a **derived** baseline at runtime
(see [NPCs and Monsters](03-npcs-and-monsters.md#baselines-and-the-derived-fallback)).
Inspect the exact stat blocks through `GetGameDataQuery().Value.NpcBaselines`.

## Monsters

36 monster variants (18 creatures, most in two profession variants). Generate with
`GenerateMonsterCommand(id, level)`. Overdriven creatures multiply their governed
stat's effect by 1.5.

| Id | Creature | Profession | Role | Overdriven |
| --- | --- | --- | --- | --- |
| goblin-rogue | Goblin | Rogue | ambusher | - |
| goblin-ranger | Goblin | Ranger | skirmisher | - |
| hobgoblin-fighter | Hobgoblin | Fighter | soldier | - |
| hobgoblin-paladin | Hobgoblin | Paladin | champion | - |
| kobold-rogue | Kobold | Rogue | trapper | - |
| kobold-conjuror | Kobold | Conjuror | hex worker | - |
| orc-berserker | Orc | Berserker | raider | - |
| orc-fighter | Orc | Fighter | warrior | - |
| ogre-berserker | Ogre | Berserker | - | - |
| ogre-fighter | Ogre | Fighter | - | - |
| troll-berserker | Troll | Berserker | - | - |
| troll-ranger | Troll | Ranger | - | - |
| skeleton-fighter | Skeleton | Fighter | - | - |
| skeleton-ranger | Skeleton | Ranger | - | - |
| zombie-fighter | Zombie | Fighter | - | - |
| zombie-berserker | Zombie | Berserker | - | - |
| ghoul-rogue | Ghoul | Rogue | - | - |
| ghoul-berserker | Ghoul | Berserker | - | - |
| vampire-rogue | Vampire | Rogue | - | **Agility** |
| vampire-conjuror | Vampire | Conjuror | - | **Intelligence** |
| werewolf-berserker | Werewolf | Berserker | - | - |
| werewolf-ranger | Werewolf | Ranger | - | - |
| giant-fighter | Giant | Fighter | - | - |
| giant-berserker | Giant | Berserker | - | - |
| minotaur-fighter | Minotaur | Fighter | - | - |
| minotaur-berserker | Minotaur | Berserker | - | - |
| harpy-bard | Harpy | Bard | - | - |
| harpy-rogue | Harpy | Rogue | - | - |
| gargoyle-fighter | Gargoyle | Fighter | - | - |
| gargoyle-rogue | Gargoyle | Rogue | - | - |
| dragon-fighter | Dragon | Fighter | - | **Strength** |
| dragon-conjuror | Dragon | Conjuror | - | **Intelligence** |
| lich-wizard | Lich | Wizard | - | **Intelligence** |
| lich-conjuror | Lich | Conjuror | - | **Intelligence** |
| demon-berserker | Demon | Berserker | - | **Strength** |
| demon-conjuror | Demon | Conjuror | - | **Intelligence** |

The eight overdriven variants belong to the four exceptional creatures (Vampire,
Dragon, Lich, Demon). Each carries the "Governed skill effect x1.5" trait.

## Action ids

142 actions, by kind. The id patterns:

| Kind | Count | Id pattern | Example |
| --- | ---: | --- | --- |
| spell | 70 | `cast-spell-<spell-id>` | `cast-spell-body-bolts` |
| item | 35 | `use-item-<item-id>` | `use-item-torch` |
| maneuver | 12 | `maneuver-<maneuver-id>` | `maneuver-climbing` |
| physicalAttack | 10 | `physical-attack-<profile>` | `physical-attack-longbow` |
| equipArmor | 10 | `equip-armor-<armor-id>` | `equip-armor-plate` |
| system | 4 | (fixed ids) | `damage-target`, `heal-target`, `apply-condition`, `defend` |

Enumerate the authoritative list at runtime with `EnumerateActionsQuery`.
