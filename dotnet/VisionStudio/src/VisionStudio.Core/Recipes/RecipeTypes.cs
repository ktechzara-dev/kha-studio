namespace VisionStudio.Core.Recipes;

/// <summary>
/// Acceptable tolerance limits for a physical measurement or inspection metric.
/// </summary>
public sealed record MeasurementTolerance(
    string Name,
    double Nominal,
    double LowerLimit,
    double UpperLimit)
{
    public bool IsInRange(double value) => value >= LowerLimit && value <= UpperLimit;
}

/// <summary>
/// Represents a single parameter difference between two recipes.
/// </summary>
public sealed record RecipeDiffRow(
    string NodeId,
    string ParameterName,
    object? ValueA,
    object? ValueB);

/// <summary>
/// Validation result of a recipe against a target pipeline graph.
/// </summary>
public sealed record RecipeValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings);
