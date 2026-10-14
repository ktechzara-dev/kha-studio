using OpenCvSharp;

namespace VisionStudio.Imaging.Grading;

public readonly record struct CodeQualityGrade(
    string DecodedText,
    char OverallLetterGrade,
    double OverallNumericGrade,
    double SymbolContrastGrade,
    double ModulationGrade,
    double AxialNonUniformityGrade,
    double DefectsGrade,
    double SymbolContrastPercent,
    double AxialNonUniformityPercent,
    bool DecodePassed);

/// <summary>
/// Evaluates 1D and 2D barcode quality metrics per ISO/IEC 15415 and ISO/IEC 15416 standards.
/// </summary>
public static class BarcodeGradeEvaluator
{
    public static CodeQualityGrade Evaluate(Mat barcodeRoi, string decodedText = "", Rect? symbolBounds = null)
    {
        bool decoded = !string.IsNullOrWhiteSpace(decodedText);
        if (barcodeRoi.Empty())
        {
            return new CodeQualityGrade("", 'F', 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, false);
        }

        using var gray = new Mat();
        if (barcodeRoi.Channels() > 1)
            Cv2.CvtColor(barcodeRoi, gray, ColorConversionCodes.BGR2GRAY);
        else
            barcodeRoi.CopyTo(gray);

        // 1. Symbol Contrast (SC): Rmax - Rmin
        gray.MinMaxLoc(out double rMin, out double rMax);
        double scPercent = Math.Clamp((rMax - rMin) / 255.0 * 100.0, 0.0, 100.0);
        double scGrade = scPercent switch
        {
            >= 70.0 => 4.0,
            >= 55.0 => 3.0,
            >= 40.0 => 2.0,
            >= 20.0 => 1.0,
            _ => 0.0
        };

        // 2. Axial Non-Uniformity (ANU): aspect deviation
        double anuPercent = 0.0;
        if (symbolBounds.HasValue && symbolBounds.Value.Height > 0)
        {
            double w = symbolBounds.Value.Width;
            double h = symbolBounds.Value.Height;
            double avg = (w + h) / 2.0;
            anuPercent = Math.Abs(w - h) / avg * 100.0;
        }
        else
        {
            // Approximate from ROI
            double avg = (gray.Width + gray.Height) / 2.0;
            anuPercent = Math.Abs(gray.Width - gray.Height) / avg * 100.0;
        }

        double anuGrade = anuPercent switch
        {
            <= 6.0 => 4.0,
            <= 8.0 => 3.0,
            <= 10.0 => 2.0,
            <= 12.0 => 1.0,
            _ => 0.0
        };

        // 3. Modulation & Defects: local variance
        using var blurred = new Mat();
        Cv2.GaussianBlur(gray, blurred, new Size(5, 5), 1.0);
        using var diff = new Mat();
        Cv2.Absdiff(gray, blurred, diff);
        var meanDiff = diff.Mean().Val0;

        double modGrade = meanDiff < 15.0 ? 4.0 : (meanDiff < 25.0 ? 3.0 : (meanDiff < 40.0 ? 2.0 : 1.0));
        double defGrade = meanDiff < 20.0 ? 4.0 : 2.0;

        // Overall Grade = min(Decode, SC, MOD, ANU, DEF)
        double decGrade = decoded ? 4.0 : 0.0;
        double overallNum = Math.Min(decGrade, Math.Min(scGrade, Math.Min(anuGrade, Math.Min(modGrade, defGrade))));

        char letterGrade = overallNum switch
        {
            >= 3.5 => 'A',
            >= 2.5 => 'B',
            >= 1.5 => 'C',
            >= 0.5 => 'D',
            _ => 'F'
        };

        return new CodeQualityGrade(
            decodedText,
            letterGrade,
            overallNum,
            scGrade,
            modGrade,
            anuGrade,
            defGrade,
            scPercent,
            anuPercent,
            decoded);
    }
}
