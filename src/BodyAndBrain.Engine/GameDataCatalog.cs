using System.Reflection;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BodyAndBrain.Engine;

public interface IGameDataCatalog
{
    GameData Data { get; }
    GameDataValidationResult Validate();
    RaceDefinition GetRace(string nameOrId);
    ProfessionDefinition GetProfession(string nameOrId);
    ActionDefinition GetAction(string actionId);
}

public sealed class GameDataCatalog : IGameDataCatalog
{
    private const string ResourceName = "BodyAndBrain.Engine.Data.bodyandbrain.game.yaml";

    private GameDataCatalog(GameData data)
    {
        Data = data;
    }

    public GameData Data { get; }

    public static GameDataCatalog LoadEmbedded()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded game data resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return new GameDataCatalog(deserializer.Deserialize<GameData>(reader));
    }

    public RaceDefinition GetRace(string nameOrId)
        => Data.Races.Single(x => Matches(x.Id, x.Name, nameOrId));

    public ProfessionDefinition GetProfession(string nameOrId)
        => Data.Professions.Single(x => Matches(x.Id, x.Name, nameOrId));

    public ActionDefinition GetAction(string actionId)
        => Data.Actions.Single(x => string.Equals(x.Id, actionId, StringComparison.OrdinalIgnoreCase));

    public GameDataValidationResult Validate()
    {
        var result = new GameDataValidationResult();
        CheckDuplicateIds(result, "race", Data.Races.Select(x => x.Id));
        CheckDuplicateIds(result, "profession", Data.Professions.Select(x => x.Id));
        CheckDuplicateIds(result, "apprenticeship", Data.Apprenticeships.Select(x => x.Id));
        CheckDuplicateIds(result, "spell", Data.Spells.Select(x => x.Id));
        CheckDuplicateIds(result, "weapon", Data.Weapons.Select(x => x.Id));
        CheckDuplicateIds(result, "armor", Data.Armors.Select(x => x.Id));
        CheckDuplicateIds(result, "item", Data.Items.Select(x => x.Id));
        CheckDuplicateIds(result, "maneuver", Data.Maneuvers.Select(x => x.Id));
        CheckDuplicateIds(result, "npc baseline", Data.NpcBaselines.Select(x => x.Id));
        CheckDuplicateIds(result, "monster", Data.Monsters.Select(x => x.Id));
        CheckDuplicateIds(result, "action", Data.Actions.Select(x => x.Id));

        var professionNames = Data.Professions.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var race in Data.Races)
        {
            foreach (var profession in race.ValidProfessions.Where(x => !professionNames.Contains(x)))
                result.Diagnostics.Add($"Race '{race.Name}' references unknown profession '{profession}'.");
        }

        var raceNames = Data.Races.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var baseline in Data.NpcBaselines)
        {
            if (!raceNames.Contains(baseline.Race))
                result.Diagnostics.Add($"NPC baseline '{baseline.Id}' references unknown race '{baseline.Race}'.");
            if (!professionNames.Contains(baseline.Profession))
                result.Diagnostics.Add($"NPC baseline '{baseline.Id}' references unknown profession '{baseline.Profession}'.");
        }

        var spellListNames = Data.SpellLists.Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var spell in Data.Spells.Where(x => !spellListNames.Contains(x.List)))
            result.Diagnostics.Add($"Spell '{spell.Id}' references unknown spell list '{spell.List}'.");

        foreach (var action in Data.Actions)
        {
            if (string.IsNullOrWhiteSpace(action.Kind))
                result.Diagnostics.Add($"Action '{action.Id}' has no kind.");

            if (string.IsNullOrWhiteSpace(action.ReferenceId))
                continue;

            var referenceExists = action.Kind switch
            {
                "physicalAttack" => Data.Weapons.Any(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase)),
                "spell" => Data.Spells.Any(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase)),
                "item" => Data.Items.Any(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase)),
                "maneuver" => Data.Maneuvers.Any(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase)),
                "equipArmor" => Data.Armors.Any(x => string.Equals(x.Id, action.ReferenceId, StringComparison.OrdinalIgnoreCase)),
                _ => true,
            };

            if (!referenceExists)
                result.Diagnostics.Add($"Action '{action.Id}' references missing {action.Kind} resource '{action.ReferenceId}'.");
        }

        return result;
    }

    private static bool Matches(string id, string name, string nameOrId)
        => string.Equals(id, Slug.Normalize(nameOrId), StringComparison.OrdinalIgnoreCase)
           || string.Equals(name, nameOrId, StringComparison.OrdinalIgnoreCase);

    private static void CheckDuplicateIds(GameDataValidationResult result, string label, IEnumerable<string> ids)
    {
        foreach (var duplicate in ids.GroupBy(x => x, StringComparer.OrdinalIgnoreCase).Where(x => x.Count() > 1))
            result.Diagnostics.Add($"Duplicate {label} id '{duplicate.Key}'.");
    }
}

public static class Slug
{
    public static string Normalize(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();
        return string.Join('-', new string(chars).Split('-', StringSplitOptions.RemoveEmptyEntries));
    }
}
