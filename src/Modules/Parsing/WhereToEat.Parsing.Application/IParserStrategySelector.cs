using WhereToEat.Parsing.Domain;
using WhereToEat.SharedKernel.Results;

namespace WhereToEat.Parsing.Application;

/// <summary>
/// Resolves a <see cref="SourceDescriptor.StrategyKey"/> to the matching <see cref="IRestaurantParser"/>
/// (invariant #4 — strategies are registered by key and resolved at run time, so a new strategy
/// self-registers without the pipeline changing). The infrastructure implementation builds a key →
/// parser map from the DI-registered strategies.
/// </summary>
public interface IParserStrategySelector
{
    /// <summary>
    /// Returns the parser registered under <paramref name="strategyKey"/>, or a failure
    /// <see cref="Result{T}"/> when no strategy is registered for that key (a misconfigured source).
    /// </summary>
    Result<IRestaurantParser> Resolve(string strategyKey);
}
