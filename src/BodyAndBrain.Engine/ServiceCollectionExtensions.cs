using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharpNinja.FeatureFlags.Cqrs;

namespace BodyAndBrain.Engine;

public sealed class BodyAndBrainEngineOptions
{
    public string StorePath { get; set; } = Path.Combine(AppContext.BaseDirectory, "bodyandbrain.engine.db");
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
        services.AddSingleton<IGameDataCatalog>(_ => GameDataCatalog.LoadEmbedded());
        services.AddSingleton<IGameStore>(_ => new LiteDbGameStore(options.StorePath));
        services.AddSingleton<IDiceRoller, RandomDiceRoller>();
        services.AddSingleton<NpcGenerator>();
        services.AddSingleton<NpcMarkdownRenderer>();
        services.AddSingleton<ActionExecutor>();
        services.AddSingleton<IBodyAndBrainEngine, BodyAndBrainEngineClient>();
        services.AddCqrs(typeof(ServiceCollectionExtensions).Assembly);
        return services;
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

    Task<YamlActionResult> ExecuteActionAsync(
        string actionId,
        string actorId,
        string? targetId = null,
        Dictionary<string, string>? parameters = null,
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

    public async Task<YamlActionResult> ExecuteActionAsync(
        string actionId,
        string actorId,
        string? targetId = null,
        Dictionary<string, string>? parameters = null,
        CancellationToken ct = default)
    {
        var result = await dispatcher.SendAsync(
            new ExecuteGameActionCommand(actionId, actorId, targetId, Parameters: parameters),
            ct).ConfigureAwait(false);
        return result.IsSuccess ? result.Value! : throw new InvalidOperationException(result.Error, result.Exception);
    }
}
