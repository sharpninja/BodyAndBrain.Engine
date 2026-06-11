namespace BodyAndBrain.Engine;

public sealed class NpcGenerator(IGameDataCatalog catalog)
{
    private static readonly string[] StatOrder =
        ["Strength", "Agility", "Constitution", "Intelligence", "Presence", "Piety"];

    public NpcRecord Generate(string raceName, string professionName, int level, string? name = null)
    {
        if (level is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(level), "NPC level must be between 1 and 50.");

        var race = catalog.GetRace(raceName);
        var profession = catalog.GetProfession(professionName);

        var baseline = catalog.Data.NpcBaselines.FirstOrDefault(x =>
            string.Equals(x.Race, race.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Profession, profession.Name, StringComparison.OrdinalIgnoreCase));

        // FR-NPC-FALLBACK-001: any canonical race/profession combination must generate. When no exact
        // baseline exists, derive a documented baseline from the profession primary stat and race bonuses.
        var derived = baseline is null;
        var baseStats = derived
            ? DeriveBaseStats(race, profession)
            : baseline!.Stats.ToDictionary(x => x.Key, x => x.Value.Score, StringComparer.OrdinalIgnoreCase);
        var array = derived ? "derived" : baseline!.Array;
        var signature = derived ? $"Derived {race.Name} {profession.Name} baseline" : baseline!.Signature;

        var stats = ScaleStats(baseStats, profession, level);
        var hits = Math.Max(1, 35 + Mechanics.LevelSignificance(level) * 3 + Mechanics.StatBonus(stats.GetValueOrDefault("Constitution")) * 5);
        return new NpcRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = name ?? $"{LevelLabel(level)} {race.Name} {profession.Name}",
            Race = race.Name,
            Profession = profession.Name,
            Level = level,
            Array = array,
            Stats = stats,
            MaxHits = hits,
            CurrentHits = hits,
            Signature = signature,
            Derived = derived,
        };
    }

    private static Dictionary<string, int> DeriveBaseStats(RaceDefinition race, ProfessionDefinition profession)
    {
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var stat in StatOrder)
        {
            var racial = race.StatBonuses.GetValueOrDefault(stat);
            var primary = string.Equals(stat, profession.PrimaryStat, StringComparison.OrdinalIgnoreCase) ? 20 : 0;
            stats[stat] = Mechanics.ClampStat(50 + racial * 5 + primary);
        }

        return stats;
    }

    private static Dictionary<string, int> ScaleStats(Dictionary<string, int> baseStats, ProfessionDefinition profession, int level)
    {
        var significance = Mechanics.LevelSignificance(level);
        var (bodyAge, brainAge) = Mechanics.AgeModifier(level);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, score) in baseStats)
        {
            var multiplier = string.Equals(stat, profession.PrimaryStat, StringComparison.OrdinalIgnoreCase)
                ? 2.0
                : stat is "Constitution" or "Agility" or "Presence" or "Piety"
                    ? 1.5
                    : 1.0;
            var age = Mechanics.IsBodyStat(stat) ? bodyAge : brainAge;
            stats[stat] = Mechanics.ClampStat(score + (int)Math.Round(significance * multiplier) + age);
        }

        return stats;
    }

    private static string LevelLabel(int level)
        => level switch
        {
            1 => "Adolescent",
            >= 2 and <= 4 => "Young Adult",
            >= 5 and <= 7 => "Adult",
            >= 8 and <= 10 => "Experienced",
            >= 11 and <= 20 => "Veteran",
            >= 21 and <= 30 => "Master",
            >= 31 and <= 40 => "Legendary",
            _ => "Mythic",
        };
}

/// <summary>
/// Generates monster combatants (<see cref="NpcRecord.IsMonster"/> = true) from the embedded monster
/// catalog. Each monster carries the canonical level-5 baseline stat block; generation scales the
/// block by level significance like ordinary NPCs and carries the overdriven governing stat forward.
/// </summary>
public sealed class MonsterGenerator(IGameDataCatalog catalog)
{
    public NpcRecord Generate(string monsterId, int level, string? name = null)
    {
        if (level is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(level), "Monster level must be between 1 and 50.");

        var monster = catalog.Data.Monsters.SingleOrDefault(x =>
            string.Equals(x.Id, monsterId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(x.Id, Slug.Normalize(monsterId), StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Monster '{monsterId}' was not found in the catalog.");

        if (monster.Stats.Count == 0)
            throw new InvalidOperationException($"Monster '{monster.Id}' has no canonical stat block.");

        var profession = catalog.GetProfession(monster.Profession);
        var significance = Mechanics.LevelSignificance(level);
        var (bodyAge, brainAge) = Mechanics.AgeModifier(level);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, score) in monster.Stats)
        {
            var multiplier = string.Equals(stat, profession.PrimaryStat, StringComparison.OrdinalIgnoreCase)
                ? 2.0
                : stat is "Constitution" or "Agility" or "Presence" or "Piety"
                    ? 1.5
                    : 1.0;
            var age = Mechanics.IsBodyStat(stat) ? bodyAge : brainAge;
            stats[stat] = Mechanics.ClampStat(score + (int)Math.Round(significance * multiplier) + age);
        }

        var hits = Math.Max(1, monster.Hits + significance * 3);
        return new NpcRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = name ?? $"{monster.Name} ({monster.Role})",
            Race = monster.Name,
            Profession = monster.Profession,
            Level = level,
            Array = "monster",
            Stats = stats,
            MaxHits = hits,
            CurrentHits = hits,
            Signature = monster.Signature,
            IsMonster = true,
            Monster = monster.Name,
            OverdrivenStat = string.IsNullOrWhiteSpace(monster.OverdrivenStat) ? null : monster.OverdrivenStat,
        };
    }
}

public sealed class NpcMarkdownRenderer
{
    public string Render(NpcRecord npc)
    {
        var lines = new List<string>
        {
            $"# {npc.Name}",
            "",
            $"*Race:* {npc.Race}",
            $"*Profession:* {npc.Profession}",
            $"*Level:* {npc.Level}",
            $"*Hits:* {npc.CurrentHits}/{npc.MaxHits}",
            $"*Signature:* {npc.Signature}",
            "",
            "| Stat | Score | Bonus |",
            "| --- | ---: | ---: |",
        };

        foreach (var stat in npc.Stats.OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase))
            lines.Add($"| {stat.Key} | {stat.Value} | {Mechanics.StatBonus(stat.Value)} |");

        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
