using WhereToEat.Parsing.Application;
using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Infrastructure.Strategies;

/// <summary>
/// Resolves a strategy key to its <see cref="IRestaurantParser"/> from the set of DI-registered
/// strategies (invariant #4). It builds a key → parser map from <b>every</b> registered
/// <see cref="IRestaurantParser"/>, so a new strategy/example parser self-registers and becomes
/// resolvable without this selector or the pipeline changing. A duplicate key is a configuration
/// error and fails fast at construction.
/// </summary>
public sealed class ParserStrategySelector : IParserStrategySelector
{
    private readonly Dictionary<string, IRestaurantParser> _byKey;

    public ParserStrategySelector(IEnumerable<IRestaurantParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        var map = new Dictionary<string, IRestaurantParser>(StringComparer.OrdinalIgnoreCase);
        foreach (var parser in parsers)
        {
            if (!map.TryAdd(parser.StrategyKey, parser))
            {
                throw new InvalidOperationException(
                    $"Two parser strategies are registered under the key '{parser.StrategyKey}'. Keys must be unique.");
            }
        }

        _byKey = map;
    }

    /// <inheritdoc />
    public Result<IRestaurantParser> Resolve(string strategyKey)
    {
        if (string.IsNullOrWhiteSpace(strategyKey) || !_byKey.TryGetValue(strategyKey, out var parser))
        {
            return Result.Failure<IRestaurantParser>(
                Error.NotFound("Parse.UnknownStrategy", $"No parser strategy is registered under the key '{strategyKey}'."));
        }

        return Result.Success(parser);
    }
}
