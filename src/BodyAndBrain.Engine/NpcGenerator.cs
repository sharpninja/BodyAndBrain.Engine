namespace BodyAndBrain.Engine;

public sealed class NpcGenerator(IGameDataCatalog catalog)
{
    public NpcRecord Generate(string raceName, string professionName, int level, string? name = null)
    {
        if (level is < 1 or > 50)
            throw new ArgumentOutOfRangeException(nameof(level), "NPC level must be between 1 and 50.");

        var race = catalog.GetRace(raceName);
        var profession = catalog.GetProfession(professionName);
        if (!race.ValidProfessions.Contains(profession.Name, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{race.Name} cannot be a {profession.Name}.");

        var baseline = catalog.Data.NpcBaselines.Single(x =>
            string.Equals(x.Race, race.Name, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Profession, profession.Name, StringComparison.OrdinalIgnoreCase));

        var significance = Mechanics.LevelSignificance(level);
        var (bodyAge, brainAge) = Mechanics.AgeModifier(level);
        var stats = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var (stat, value) in baseline.Stats)
        {
            var multiplier = string.Equals(stat, profession.PrimaryStat, StringComparison.OrdinalIgnoreCase)
                ? 2.0
                : stat is "Constitution" or "Agility" or "Presence" or "Piety"
                    ? 1.5
                    : 1.0;
            var age = Mechanics.IsBodyStat(stat) ? bodyAge : brainAge;
            stats[stat] = Mechanics.ClampStat(value.Score + (int)Math.Round(significance * multiplier) + age);
        }

        var hits = Math.Max(1, 35 + significance * 3 + Mechanics.StatBonus(stats.GetValueOrDefault("Constitution")) * 5);
        return new NpcRecord
        {
            Id = Guid.NewGuid().ToString("n"),
            Name = name ?? $"{LevelLabel(level)} {race.Name} {profession.Name}",
            Race = race.Name,
            Profession = profession.Name,
            Level = level,
            Array = baseline.Array,
            Stats = stats,
            MaxHits = hits,
            CurrentHits = hits,
            Signature = baseline.Signature,
        };
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
