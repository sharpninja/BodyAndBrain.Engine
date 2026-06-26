using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Cqrs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace BodyAndBrain.Engine;

public sealed class BodyAndBrainEngineOptions
{
    public string StorePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "bodyandbrain.engine.db");
    public int? RandomSeed { get; set; } = null; // for determinism (in addition to per-roll overrides)
}

/// <summary>
/// Canonical magic numbers loaded from embedded YAML and exposed for IConfiguration binding.
/// </summary>
public sealed class MagicNumbers
{
    public int MaxLevel { get; set; } = 50;
    public int BaseHits { get; set; } = 35;
    public int SkillMax { get; set; } = 10;
    public int BaseStat { get; set; } = 50;
    public int PrimaryStatBonus { get; set; } = 20;
    public int ManeuverDefaultRoll { get; set; } = 50;
    public int DefenseBonus { get; set; } = 10;
    public List<string> StatNames { get; set; } = new() { "Strength", "Agility", "Constitution", "Intelligence", "Presence", "Piety" };
}

public static class ServiceCollectionExtensions
{
    [RequiresUnreferencedCode("FeatureFlags CQRS registration scans engine handler types by reflection.")]
    public static IServiceCollection AddBodyAndBrainEngine(
        this IServiceCollection services,
        Action<BodyAndBrainEngineOptions>? configure = null)
    {
        var options = new BodyAndBrainEngineOptions();
        configure?.Invoke(options);

        services.AddLogging();
        services.AddSingleton(options);

        // Load magic numbers YAML as IConfiguration (embedded resource) + bind to POCO
        var magicConfig = BuildMagicNumbersConfiguration();
        services.AddSingleton<IConfiguration>(magicConfig);
        var magicNumbers = new MagicNumbers();
        magicConfig.GetSection("game").Bind(magicNumbers);
        services.AddSingleton(magicNumbers);

        services.AddSingleton<IGameDataCatalog>(_ => GameDataCatalog.LoadEmbedded());
        services.AddSingleton<IGameStore>(_ => new LiteDbGameStore(options.StorePath));
        services.AddSingleton<IDiceRoller>(_ => new RandomDiceRoller(options.RandomSeed));
        services.AddSingleton<NpcGenerator>();
        services.AddSingleton<MonsterGenerator>();
        services.AddSingleton<NpcMarkdownRenderer>();
        services.AddSingleton<ActionExecutor>();
        services.AddSingleton<IBodyAndBrainEngine, BodyAndBrainEngineClient>();
        services.AddCqrs(typeof(ServiceCollectionExtensions).Assembly);
        return services;
    }

    private static IConfiguration BuildMagicNumbersConfiguration()
    {
        const string ResourceName = "BodyAndBrain.Engine.Data.magic-numbers.yaml";
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded magic numbers resource '{ResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var yaml = reader.ReadToEnd();

        // Convert YAML to flat key/value for InMemoryConfiguration (IConfiguration compatible)
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var raw = deserializer.Deserialize<Dictionary<string, object>>(yaml);

        var data = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        FlattenYaml(raw, "game", data);  // root under "game:" to match section

        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    private static void FlattenYaml(Dictionary<string, object> source, string prefix, Dictionary<string, string?> target)
    {
        foreach (var (key, value) in source)
        {
            var fullKey = string.IsNullOrEmpty(prefix) ? key : $"{prefix}:{key}";
            if (value is Dictionary<object, object> dict)
            {
                var strDict = dict.ToDictionary(k => k.Key.ToString()!, v => v.Value, StringComparer.OrdinalIgnoreCase);
                FlattenYaml(strDict, fullKey, target);
            }
            else if (value is List<object> list)
            {
                // For lists like statNames, join or index - for simplicity we store as comma for scalar use; consumers use POCO bind
                target[fullKey] = string.Join(",", list);
            }
            else
            {
                target[fullKey] = value?.ToString();
            }
        }
    }
}

public interface IBodyAndBrainEngine
{
    Task<PlayerCharacterRecord> CreatePlayerCharacterAsync(
        string name,
        string race,
        string profession,
        string apprenticeship,
        CancellationToken ct = default);

    Task<PlayerCharacterRecord> ApplyLevelUpAsync(
        string characterId,
        Dictionary<string, int>? statIncrements = null,
        Dictionary<string, int>? skillIncrements = null,
        CancellationToken ct = default);

    Task<NpcRecord> GenerateNpcAsync(
        string race,
        string profession,
        int level,
        string? name = null,
        CancellationToken ct = default);

    Task<NpcRecord> GenerateMonsterAsync(
        string monsterId,
        int level,
        string? name = null,
        CancellationToken ct = default);

    Task<YamlActionResult> ExecuteActionAsync(
        string actionId,
        string actorId,
        string? targetId = null,
        int? rollOverride = null,
        Dictionary<string, string>? parameters = null,
        CancellationToken ct = default);

    Task<YamlActionResult> TickStatusEffectsAsync(
        string targetId,
        int? rollOverride = null,
        CancellationToken ct = default);
}

public sealed class BodyAndBrainEngineClient(IDispatcher dispatcher) : IBodyAndBrainEngine
{
    public async Task<PlayerCharacterRecord> CreatePlayerCharacterAsync(
        string name,
        string race,
        string profession,
        string apprenticeship,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new CreatePlayerCharacterCommand(name, race, profession, apprenticeship),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }

    public async Task<PlayerCharacterRecord> ApplyLevelUpAsync(
        string characterId,
        Dictionary<string, int>? statIncrements = null,
        Dictionary<string, int>? skillIncrements = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new ApplyLevelUpCommand(characterId, statIncrements, skillIncrements),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }

    public async Task<NpcRecord> GenerateNpcAsync(
        string race,
        string profession,
        int level,
        string? name = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new GenerateNpcCommand(race, profession, level, name),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }

    public async Task<NpcRecord> GenerateMonsterAsync(
        string monsterId,
        int level,
        string? name = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new GenerateMonsterCommand(monsterId, level, name),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }

    public async Task<YamlActionResult> ExecuteActionAsync(
        string actionId,
        string actorId,
        string? targetId = null,
        int? rollOverride = null,
        Dictionary<string, string>? parameters = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new ExecuteGameActionCommand(actionId, actorId, targetId, rollOverride, parameters),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }

    public async Task<YamlActionResult> TickStatusEffectsAsync(
        string targetId,
        int? rollOverride = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new TickStatusEffectsCommand(targetId, rollOverride),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }
}
