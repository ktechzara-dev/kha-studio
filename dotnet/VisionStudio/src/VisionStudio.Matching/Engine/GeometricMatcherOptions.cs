namespace VisionStudio.Matching.Engine;

/// <summary>
/// Configuration parameters for geometric pattern matching.
/// </summary>
public sealed class GeometricMatcherOptions
{
    /// <summary>
    /// Minimum score threshold [0.0 .. 1.0] for accepting a match candidate.
    /// </summary>
    public float MinScore { get; set; } = 0.70f;

    /// <summary>
    /// Maximum number of matched instances to return.
    /// </summary>
    public int MaxInstances { get; set; } = 1;

    /// <summary>
    /// Minimum rotation angle in degrees [-180.0 .. 180.0].
    /// </summary>
    public float AngleMinDeg { get; set; } = -180.0f;

    /// <summary>
    /// Maximum rotation angle in degrees [-180.0 .. 180.0].
    /// </summary>
    public float AngleMaxDeg { get; set; } = 180.0f;

    /// <summary>
    /// Coarse rotation search step in degrees.
    /// </summary>
    public float AngleStepDeg { get; set; } = 2.0f;

    /// <summary>
    /// Minimum scale factor (e.g. 0.9 = 90% size).
    /// </summary>
    public float ScaleMin { get; set; } = 0.90f;

    /// <summary>
    /// Maximum scale factor (e.g. 1.1 = 110% size).
    /// </summary>
    public float ScaleMax { get; set; } = 1.10f;

    /// <summary>
    /// Coarse scale search step.
    /// </summary>
    public float ScaleStep { get; set; } = 0.05f;

    /// <summary>
    /// If true, detects objects where bright/dark contrast polarity is flipped (e.g. white on black vs black on white).
    /// </summary>
    public bool AllowContrastReversal { get; set; } = false;

    /// <summary>
    /// Minimum edge gradient magnitude on target scene to be considered a feature.
    /// </summary>
    public float MinContrast { get; set; } = 20.0f;

    /// <summary>
    /// Maximum allowed overlap ratio between detected instances during Non-Maximum Suppression.
    /// </summary>
    public float OverlapThreshold { get; set; } = 0.40f;

    /// <summary>
    /// Enable sub-pixel and sub-degree gradient peak refinement for &lt;0.3 px and &lt;0.3 deg accuracy.
    /// </summary>
    public bool SubPixelRefinement { get; set; } = true;

    /// <summary>
    /// Number of multi-resolution pyramid levels to search (1 to 4).
    /// </summary>
    public int PyramidLevels { get; set; } = 3;
}
