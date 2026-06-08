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
