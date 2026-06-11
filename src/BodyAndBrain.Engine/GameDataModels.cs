namespace BodyAndBrain.Engine;

public sealed class GameData
{
    public SourceManifest Source { get; set; } = new();
    public List<RaceDefinition> Races { get; set; } = [];
    public List<ProfessionDefinition> Professions { get; set; } = [];
    public List<ApprenticeshipDefinition> Apprenticeships { get; set; } = [];
    public List<SkillDefinition> Skills { get; set; } = [];
    public List<SpellListDefinition> SpellLists { get; set; } = [];
    public List<SpellDefinition> Spells { get; set; } = [];
    public List<WeaponDefinition> Weapons { get; set; } = [];
    public List<ArmorDefinition> Armors { get; set; } = [];
    public List<ItemDefinition> Items { get; set; } = [];
    public List<ManeuverDefinition> Maneuvers { get; set; } = [];
    public List<NpcBaselineDefinition> NpcBaselines { get; set; } = [];
    public List<MonsterDefinition> Monsters { get; set; } = [];
    public List<ActionDefinition> Actions { get; set; } = [];
    public List<KnownGapDefinition> KnownGaps { get; set; } = [];
}

public sealed class SourceManifest
{
    public string ManualRepo { get; set; } = "";
    public string ManualCommit { get; set; } = "";
    public string Workbook { get; set; } = "";
    public string Policy { get; set; } = "";
}

public sealed class RaceDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public Dictionary<string, int> StatBonuses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Resistances { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> ValidProfessions { get; set; } = [];
}

public sealed class ProfessionDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string PrimaryStat { get; set; } = "";
    public string ClassBonus { get; set; } = "";
}

public sealed class ApprenticeshipDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Profession { get; set; } = "";
}

public sealed class SkillDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Stat { get; set; }
}

public sealed class SpellListDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Discipline { get; set; } = "";
    public string Stat { get; set; } = "";
    public List<string> Restrictions { get; set; } = [];
}

public sealed class SpellDefinition
{
    public string Id { get; set; } = "";
    public string List { get; set; } = "";
    public int Level { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Targets { get; set; } = "";
    public string Range { get; set; } = "";
    public string Area { get; set; } = "";
    public string Modifier { get; set; } = "";
    public string Duration { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class WeaponDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string AttackProfile { get; set; } = "";
    public string Damage { get; set; } = "";
    public string Critical { get; set; } = "";
    public string GoverningStat { get; set; } = "";
}

public sealed class ArmorDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Profile { get; set; } = "";
    public bool Metal { get; set; }

    /// <summary>
    /// Engine-derived damage-reduction percent overlay (NOT canonical BaB; BaB models armor as
    /// accuracy mitigation via the Base Attack Bonus table). Surfaced for downstream consumers
    /// that expect a protection percent. Optional; derived from <see cref="Profile"/> when omitted.
    /// </summary>
    public int? ProtectionPercent { get; set; }
}

public sealed class ItemDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
}

public sealed class ManeuverDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
}

public sealed class NpcBaselineDefinition
{
    public string Id { get; set; } = "";
    public string Race { get; set; } = "";
    public string Profession { get; set; } = "";
    public string Array { get; set; } = "";
    public Dictionary<string, StatValue> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int Hits { get; set; }
    public string Signature { get; set; } = "";
}

public sealed class StatValue
{
    public int Score { get; set; }
    public int Bonus { get; set; }
}

public sealed class MonsterDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Profession { get; set; } = "";
    public string Role { get; set; } = "";

    /// <summary>
    /// Name of the overdriven governing stat (e.g. Strength, Agility, Intelligence) for exceptional
    /// monsters, or null. Governed-skill effects of this stat are increased x1.5 during resolution.
    /// </summary>
    public string? OverdrivenStat { get; set; }

    /// <summary>Human-readable overdrive effect label from the manual ("Governed skill effect x1.5"); empty for ordinary monsters.</summary>
    public string OverdriveEffect { get; set; } = "";

    /// <summary>Canonical level-5 baseline stat scores from the 8-Non-Player_Characters monster table.</summary>
    public Dictionary<string, int> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Canonical level-5 baseline hits from the monster table.</summary>
    public int Hits { get; set; }

    public string Signature { get; set; } = "";
}

public sealed class ActionDefinition
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";
    public string? ReferenceId { get; set; }
}

public sealed class KnownGapDefinition
{
    public string Id { get; set; } = "";
    public string Description { get; set; } = "";
}

public sealed class GameDataValidationResult
{
    public List<string> Diagnostics { get; set; } = [];
    public bool IsValid => Diagnostics.Count == 0;
}
