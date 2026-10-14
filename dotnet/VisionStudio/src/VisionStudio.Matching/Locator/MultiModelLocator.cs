using OpenCvSharp;
using VisionStudio.Matching.Engine;
using VisionStudio.Matching.Models;

namespace VisionStudio.Matching.Locator;

/// <summary>
/// Execution mode for multi-model search.
/// </summary>
public enum MultiModelSearchMode
{
    First,
    Best,
    All
}

/// <summary>
/// Rule defining acceptable geometric tolerance between two assembled components.
/// </summary>
public sealed class AssemblyVerificationRule
{
    public string Name { get; set; } = "RelativePoseCheck";
    public string ModelAId { get; set; } = string.Empty;
    public string ModelBId { get; set; } = string.Empty;

    public float NominalDistancePx { get; set; } = 100.0f;
    public float DistanceTolerancePx { get; set; } = 10.0f;

    public float? NominalRelativeAngleDeg { get; set; } = null;
    public float AngleToleranceDeg { get; set; } = 5.0f;

    public AssemblyVerificationRule() { }

    public AssemblyVerificationRule(
        string name,
        string modelAId,
        string modelBId,
        float nominalDistancePx,
        float distanceTolerancePx,
        float? nominalRelativeAngleDeg = null,
        float angleToleranceDeg = 5.0f)
    {
        Name = name;
        ModelAId = modelAId;
        ModelBId = modelBId;
        NominalDistancePx = nominalDistancePx;
        DistanceTolerancePx = distanceTolerancePx;
        NominalRelativeAngleDeg = nominalRelativeAngleDeg;
        AngleToleranceDeg = angleToleranceDeg;
    }
}

/// <summary>
/// Outcome of verifying an assembly against geometric rules.
/// </summary>
public sealed class AssemblyVerificationResult
{
    public bool IsValid { get; }
    public IReadOnlyList<string> Discrepancies { get; }
    public IReadOnlyDictionary<string, MatchInstance> LocatedParts { get; }

    public AssemblyVerificationResult(bool isValid, IEnumerable<string> discrepancies, IDictionary<string, MatchInstance> parts)
    {
        IsValid = isValid;
        Discrepancies = discrepancies.ToList();
        LocatedParts = parts.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }
}

/// <summary>
/// Searches for multiple models concurrently or sequentially and validates assembly placement.
/// </summary>
public sealed class MultiModelLocator
{
    private readonly GeometricMatcher _matcher = new();
    private readonly Dictionary<string, (GeometricModel Model, GeometricMatcherOptions Options)> _models = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<AssemblyVerificationRule> _rules = new();

    public void RegisterModel(GeometricModel model, GeometricMatcherOptions? options = null)
    {
        _models[model.Id] = (model, options ?? new GeometricMatcherOptions());
    }

    public void AddRule(AssemblyVerificationRule rule)
    {
        _rules.Add(rule);
    }

    public (Dictionary<string, MatchReport> Matches, AssemblyVerificationResult Verification) LocateAndVerify(
        Mat image,
        MultiModelSearchMode mode = MultiModelSearchMode.Best)
    {
        var matchResults = new Dictionary<string, MatchReport>(StringComparer.OrdinalIgnoreCase);
        var primaryInstances = new Dictionary<string, MatchInstance>(StringComparer.OrdinalIgnoreCase);

        foreach (var (id, (model, options)) in _models)
        {
            var opt = new GeometricMatcherOptions
            {
                MinScore = options.MinScore,
                MaxInstances = mode switch
                {
                    MultiModelSearchMode.First => 1,
                    MultiModelSearchMode.Best => 1,
                    MultiModelSearchMode.All => options.MaxInstances,
                    _ => 1
                },
                AngleMinDeg = options.AngleMinDeg,
                AngleMaxDeg = options.AngleMaxDeg,
                AngleStepDeg = options.AngleStepDeg,
                ScaleMin = options.ScaleMin,
                ScaleMax = options.ScaleMax,
                ScaleStep = options.ScaleStep,
                AllowContrastReversal = options.AllowContrastReversal,
                MinContrast = options.MinContrast,
                SubPixelRefinement = options.SubPixelRefinement,
                PyramidLevels = options.PyramidLevels
            };

            var report = _matcher.Match(image, model, opt);
            matchResults[id] = report;

            if (report.Success && report.BestMatch != null)
            {
                primaryInstances[id] = report.BestMatch;
            }

            if (mode == MultiModelSearchMode.First && report.Success)
            {
                break;
            }
        }

        // Verify assembly rules
        var discrepancies = new List<string>();
        foreach (var rule in _rules)
        {
            if (!primaryInstances.TryGetValue(rule.ModelAId, out var partA))
            {
                discrepancies.Add($"Rule '{rule.Name}' failed: Part '{rule.ModelAId}' was not detected in scene.");
                continue;
            }

            if (!primaryInstances.TryGetValue(rule.ModelBId, out var partB))
            {
                discrepancies.Add($"Rule '{rule.Name}' failed: Part '{rule.ModelBId}' was not detected in scene.");
                continue;
            }

            // Check distance
            float dx = partB.X - partA.X;
            float dy = partB.Y - partA.Y;
            float actualDist = MathF.Sqrt(dx * dx + dy * dy);
            float distDiff = MathF.Abs(actualDist - rule.NominalDistancePx);

            if (distDiff > rule.DistanceTolerancePx)
            {
                discrepancies.Add($"Rule '{rule.Name}' failed: Distance between parts is {actualDist:F1}px (nominal {rule.NominalDistancePx:F1}px ± {rule.DistanceTolerancePx:F1}px, error: {distDiff:F1}px).");
            }

            // Check relative angle if rule specifies nominal angle
            if (rule.NominalRelativeAngleDeg.HasValue)
            {
                float actualRelAngle = partB.AngleDeg - partA.AngleDeg;
                // Normalize to [-180..180]
                while (actualRelAngle > 180.0f) actualRelAngle -= 360.0f;
                while (actualRelAngle < -180.0f) actualRelAngle += 360.0f;

                float angleDiff = MathF.Abs(actualRelAngle - rule.NominalRelativeAngleDeg.Value);
                if (angleDiff > 180.0f) angleDiff = 360.0f - angleDiff;

                if (angleDiff > rule.AngleToleranceDeg)
                {
                    discrepancies.Add($"Rule '{rule.Name}' failed: Relative orientation is {actualRelAngle:F1}° (nominal {rule.NominalRelativeAngleDeg.Value:F1}° ± {rule.AngleToleranceDeg:F1}°, error: {angleDiff:F1}°).");
                }
            }
        }

        var verification = new AssemblyVerificationResult(
            isValid: discrepancies.Count == 0,
            discrepancies: discrepancies,
            parts: primaryInstances);

        return (matchResults, verification);
    }
}
