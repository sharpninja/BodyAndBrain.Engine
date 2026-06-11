namespace BodyAndBrain.Engine;

public sealed class PlayerCharacterRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string Profession { get; set; } = "";
    public string Apprenticeship { get; set; } = "";
    public int Level { get; set; }
    public Dictionary<string, int> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, int> Skills { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int MaxHits { get; set; }
    public int CurrentHits { get; set; }
    public List<string> LevelUpHistory { get; set; } = [];
    public List<StatusEffectState> Statuses { get; set; } = [];
}

public sealed class NpcRecord
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Race { get; set; } = "";
    public string Profession { get; set; } = "";
    public int Level { get; set; }
    public string Array { get; set; } = "";
    public Dictionary<string, int> Stats { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public int MaxHits { get; set; }
    public int CurrentHits { get; set; }
    public string Signature { get; set; } = "";
    public bool IsMonster { get; set; }
    public string? Monster { get; set; }

    /// <summary>
    /// Name of the stat whose governed-skill effect is overdriven x1.5 for exceptional monsters
    /// (Lich/Demon/Dragon/Vampire). Null for ordinary NPCs and monsters.
    /// </summary>
    public string? OverdrivenStat { get; set; }

    /// <summary>True when the NPC was generated from a derived (fallback) baseline rather than an exact one.</summary>
    public bool Derived { get; set; }

    public List<StatusEffectState> Statuses { get; set; } = [];
}

/// <summary>
/// Mechanically effective status effect applied to a PC or NPC. BaB-canonical statuses are
/// Bleed, Poison, Disease, Stun, Move, and Curse. Magnitude is per-round damage (for damaging
/// statuses) or penalty magnitude (for control statuses). Resistance rolls are resolved on tick.
/// </summary>
public sealed class StatusEffectState
{
    public string Type { get; set; } = "";
    public int Magnitude { get; set; }
    public int RoundsRemaining { get; set; }
    public string ResistanceStat { get; set; } = "";
    public int ResistanceBonus { get; set; }
    public bool Active { get; set; } = true;
}

public sealed class ActionLogRecord
{
    public string Id { get; set; } = "";
    public string ActionId { get; set; } = "";
    public string ActorId { get; set; } = "";
    public string? TargetId { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string YamlDocument { get; set; } = "";
}

public sealed class YamlActionResult
{
    public string ActionId { get; set; } = "";
    public string Document { get; set; } = "";
    public bool RequiresAdjudication { get; set; }
    public string? AdjudicationReason { get; set; }
}
