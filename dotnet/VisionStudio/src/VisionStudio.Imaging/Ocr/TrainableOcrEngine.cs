using OpenCvSharp;

namespace VisionStudio.Imaging.Ocr;

public readonly record struct OcrCharacterResult(char Character, float Confidence, Rect BoundingBox);

public readonly record struct OcrInspectionResult(
    string ReadText,
    float OverallConfidence,
    bool IsVerified,
    IReadOnlyList<OcrCharacterResult> Characters);

/// <summary>
/// Industrial trainable OCR/OCV engine for character segmentation, font template matching, and string verification.
/// </summary>
public sealed class TrainableOcrEngine
{
    private const int GlyphWidth = 20;
    private const int GlyphHeight = 28;

    private readonly Dictionary<char, List<float[,]>> _fontTemplates = new();

    public TrainableOcrEngine()
    {
        // Seed default font glyph models
        TrainDefaultGlyphs();
    }

    /// <summary>
    /// Trains a character template from a cropped binary/grayscale character image.
    /// </summary>
    public void TrainCharacter(char character, Mat characterMat)
    {
        var grid = ExtractNormalizedGrid(characterMat);
        if (!_fontTemplates.TryGetValue(character, out var list))
        {
            list = new List<float[,]>();
            _fontTemplates[character] = list;
        }
        list.Add(grid);
    }

    /// <summary>
    /// Segments text lines into discrete character blobs and recognizes each character.
    /// </summary>
    public OcrInspectionResult RecognizeAndVerify(Mat srcMat, string expectedString = "", float minConfidence = 0.6f)
    {
        if (srcMat.Empty())
            return new OcrInspectionResult("", 0, false, Array.Empty<OcrCharacterResult>());

        using var gray = new Mat();
        if (srcMat.Channels() > 1)
            Cv2.CvtColor(srcMat, gray, ColorConversionCodes.BGR2GRAY);
        else
            srcMat.CopyTo(gray);

        // Binarize (Otsu)
        using var bin = new Mat();
        Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        // Find connected components (character candidates)
        using var contoursMat = new Mat();
        Cv2.FindContours(bin, out Point[][] contours, out HierarchyIndex[] hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var boxes = new List<Rect>();
        foreach (var c in contours)
        {
            var r = Cv2.BoundingRect(c);
            // Filter noise by height and aspect ratio
            if (r.Height >= 8 && r.Width >= 3 && r.Height <= gray.Height * 0.95 && r.Width <= gray.Width * 0.8)
            {
                boxes.Add(r);
            }
        }

        // Sort left-to-right
        boxes = boxes.OrderBy(b => b.X).ToList();

        var charResults = new List<OcrCharacterResult>();
        foreach (var box in boxes)
        {
            using var charRoi = new Mat(bin, box);
            var grid = ExtractNormalizedGrid(charRoi);

            // Match against trained templates
            char bestChar = '?';
            float bestScore = -1f;

            foreach (var (ch, templates) in _fontTemplates)
            {
                foreach (var tmpl in templates)
                {
                    float score = ComputeSimilarity(grid, tmpl);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestChar = ch;
                    }
                }
            }

            charResults.Add(new OcrCharacterResult(bestChar, Math.Max(0f, bestScore), box));
        }

        var readText = new string(charResults.Select(c => c.Character).ToArray());
        float avgConf = charResults.Count > 0 ? charResults.Average(c => c.Confidence) : 0f;

        bool isVerified = true;
        if (!string.IsNullOrEmpty(expectedString))
        {
            isVerified = string.Equals(readText.Trim(), expectedString.Trim(), StringComparison.Ordinal)
                         && charResults.All(c => c.Confidence >= minConfidence);
        }

        return new OcrInspectionResult(readText, avgConf, isVerified, charResults);
    }

    private static float[,] ExtractNormalizedGrid(Mat charBin)
    {
        using var resized = new Mat();
        Cv2.Resize(charBin, resized, new Size(GlyphWidth, GlyphHeight), 0, 0, InterpolationFlags.Area);

        var grid = new float[GlyphHeight, GlyphWidth];
        for (int r = 0; r < GlyphHeight; r++)
        {
            for (int c = 0; c < GlyphWidth; c++)
            {
                grid[r, c] = resized.At<byte>(r, c) / 255.0f;
            }
        }
        return grid;
    }

    private static float ComputeSimilarity(float[,] a, float[,] b)
    {
        float dot = 0;
        float normA = 0;
        float normB = 0;

        for (int r = 0; r < GlyphHeight; r++)
        {
            for (int c = 0; c < GlyphWidth; c++)
            {
                float va = a[r, c];
                float vb = b[r, c];
                dot += va * vb;
                normA += va * va;
                normB += vb * vb;
            }
        }

        if (normA <= 1e-6f || normB <= 1e-6f)
            return 0f;

        return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    private void TrainDefaultGlyphs()
    {
        // Synthesize standard clean font glyphs (0-9, A-Z) using OpenCV rendering
        string alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ-";
        foreach (char ch in alphabet)
        {
            using var canvas = new Mat(50, 40, MatType.CV_8UC1, new Scalar(0));
            Cv2.PutText(canvas, ch.ToString(), new Point(5, 38), HersheyFonts.HersheySimplex, 1.0, new Scalar(255), 2);
            Cv2.FindContours(canvas, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            if (contours.Length > 0)
            {
                int minX = contours.Min(c => c.Min(p => p.X));
                int maxX = contours.Max(c => c.Max(p => p.X));
                int minY = contours.Min(c => c.Min(p => p.Y));
                int maxY = contours.Max(c => c.Max(p => p.Y));
                var tightRect = new Rect(minX, minY, Math.Max(1, maxX - minX + 1), Math.Max(1, maxY - minY + 1));

                using var cropped = new Mat(canvas, tightRect);
                TrainCharacter(ch, cropped);
            }
            else
            {
                TrainCharacter(ch, canvas);
            }
        }
    }
}
