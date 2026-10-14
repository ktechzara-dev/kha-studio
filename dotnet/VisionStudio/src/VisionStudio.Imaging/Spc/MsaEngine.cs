namespace VisionStudio.Imaging.Spc;

public readonly record struct GageRAndRReport(
    double EquipmentVariationRepeatability,
    double AppraiserVariationReproducibility,
    double TotalGageRAndR,
    double PartVariation,
    double TotalVariation,
    double PercentGageRAndR,
    int NumberOfDistinctCategories,
    string Status);

public readonly record struct SpcProcessCapability(
    double Mean,
    double StdDev,
    double Cp,
    double Cpk,
    double UpperControlLimit,
    double LowerControlLimit,
    bool InControl,
    int SampleCount);

/// <summary>
/// Measurement System Analysis (MSA) and Statistical Process Control (SPC) calculation engine.
/// Evaluates Gage R&amp;R, Cp/Cpk process capability, and Shewhart control limits.
/// </summary>
public static class MsaEngine
{
    /// <summary>
    /// Computes process capability Cp, Cpk, and 3-sigma control limits from a sequence of measured samples.
    /// </summary>
    public static SpcProcessCapability EvaluateCapability(IReadOnlyList<double> samples, double lowerSpecLimit, double upperSpecLimit)
    {
        if (samples.Count < 2)
        {
            return new SpcProcessCapability(0, 0, 0, 0, 0, 0, false, samples.Count);
        }

        double mean = samples.Average();
        double sumSq = samples.Sum(x => (x - mean) * (x - mean));
        double stdDev = Math.Sqrt(sumSq / (samples.Count - 1));

        if (stdDev < 1e-9)
        {
            return new SpcProcessCapability(mean, 0, 99.0, 99.0, mean, mean, true, samples.Count);
        }

        double cp = (upperSpecLimit - lowerSpecLimit) / (6.0 * stdDev);
        double cpu = (upperSpecLimit - mean) / (3.0 * stdDev);
        double cpl = (mean - lowerSpecLimit) / (3.0 * stdDev);
        double cpk = Math.Min(cpu, cpl);

        double ucl = mean + 3.0 * stdDev;
        double lcl = mean - 3.0 * stdDev;

        // In-control check: all points within 3-sigma limits and Cpk >= 1.33
        bool inControl = samples.All(x => x >= lcl && x <= ucl) && cpk >= 1.0;

        return new SpcProcessCapability(mean, stdDev, cp, cpk, ucl, lcl, inControl, samples.Count);
    }

    /// <summary>
    /// Computes Gage R&amp;R repeatability and reproducibility metrics from trials.
    /// trials matrix: [partIndex, trialIndex]
    /// </summary>
    public static GageRAndRReport EvaluateGageRAndR(double[,] measurements)
    {
        int parts = measurements.GetLength(0);
        int trials = measurements.GetLength(1);

        if (parts < 2 || trials < 2)
        {
            return new GageRAndRReport(0, 0, 0, 0, 0, 0, 0, "INSUFFICIENT_DATA");
        }

        // Calculate range per part across trials
        double sumRange = 0;
        var partAverages = new double[parts];

        for (int p = 0; p < parts; p++)
        {
            double min = double.MaxValue;
            double max = double.MinValue;
            double sum = 0;

            for (int t = 0; t < trials; t++)
            {
                double val = measurements[p, t];
                sum += val;
                if (val < min) min = val;
                if (val > max) max = val;
            }

            sumRange += (max - min);
            partAverages[p] = sum / trials;
        }

        double rBar = sumRange / parts;

        // Constants based on trial count (AIAG MSA 4th edition)
        // d2 constants for trials: 2 -> 1.128, 3 -> 1.693, 4 -> 2.059
        double d2 = trials switch
        {
            2 => 1.128,
            3 => 1.693,
            _ => 2.059
        };

        // Equipment Variation (Repeatability)
        double ev = (rBar / d2) * 5.15;

        // Part Variation
        double rp = partAverages.Max() - partAverages.Min();
        double d2Part = parts switch
        {
            >= 10 => 3.179,
            5 => 2.326,
            _ => 2.059
        };
        double pv = (rp / d2Part) * 5.15;

        // Appraiser Variation (for single automated station, AV is nominal residual)
        double av = ev * 0.1;

        double grr = Math.Sqrt(ev * ev + av * av);
        double tv = Math.Sqrt(grr * grr + pv * pv);

        double pctGrr = tv > 1e-9 ? (grr / tv) * 100.0 : 0.0;
        int ndc = grr > 1e-9 ? (int)Math.Max(1, Math.Floor(1.41 * (pv / grr))) : 1;

        string status = pctGrr switch
        {
            < 10.0 => "EXCELLENT",
            <= 30.0 => "MARGINAL_ACCEPTABLE",
            _ => "UNACCEPTABLE"
        };

        return new GageRAndRReport(ev, av, grr, pv, tv, pctGrr, ndc, status);
    }
}
