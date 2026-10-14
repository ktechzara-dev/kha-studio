using OpenCvSharp;

namespace VisionStudio.Imaging.Stitching;

public readonly record struct StitchResult(Mat StitchedMat, double OffsetX, double OffsetY, int InlierCount, bool Success);

/// <summary>
/// Industrial image stitching and line-scan frame assembler engine using ORB feature matching and homography blending.
/// </summary>
public static class ImageStitcher
{
    /// <summary>
    /// Stitches two overlapping images horizontally or vertically using ORB feature matching and RANSAC homography.
    /// </summary>
    public static StitchResult StitchPair(Mat imgA, Mat imgB, int maxFeatures = 500)
    {
        if (imgA.Empty() || imgB.Empty())
            return new StitchResult(new Mat(), 0, 0, 0, false);

        using var grayA = new Mat();
        using var grayB = new Mat();

        if (imgA.Channels() > 1) Cv2.CvtColor(imgA, grayA, ColorConversionCodes.BGR2GRAY);
        else imgA.CopyTo(grayA);

        if (imgB.Channels() > 1) Cv2.CvtColor(imgB, grayB, ColorConversionCodes.BGR2GRAY);
        else imgB.CopyTo(grayB);

        using var orb = ORB.Create(maxFeatures);
        using var descA = new Mat();
        using var descB = new Mat();

        orb.DetectAndCompute(grayA, null, out KeyPoint[] kpsA, descA);
        orb.DetectAndCompute(grayB, null, out KeyPoint[] kpsB, descB);

        if (kpsA.Length < 4 || kpsB.Length < 4 || descA.Empty() || descB.Empty())
        {
            // Fallback: horizontal concatenation if no features found
            var concat = new Mat();
            Cv2.HConcat(new[] { imgA, imgB }, concat);
            return new StitchResult(concat, imgA.Width, 0, 0, false);
        }

        using var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: true);
        var matches = matcher.Match(descB, descA);

        if (matches.Length < 4)
        {
            var concat = new Mat();
            Cv2.HConcat(new[] { imgA, imgB }, concat);
            return new StitchResult(concat, imgA.Width, 0, 0, false);
        }

        // Sort matches by distance
        var goodMatches = matches.OrderBy(m => m.Distance).Take(Math.Min(50, matches.Length)).ToArray();

        var ptsB = goodMatches.Select(m => new Point2f(kpsB[m.QueryIdx].Pt.X, kpsB[m.QueryIdx].Pt.Y)).ToArray();
        var ptsA = goodMatches.Select(m => new Point2f(kpsA[m.TrainIdx].Pt.X, kpsA[m.TrainIdx].Pt.Y)).ToArray();

        using var inlierMask = new Mat();
        using var H = Cv2.FindHomography(InputArray.Create(ptsB), InputArray.Create(ptsA), HomographyMethods.Ransac, 3.0, inlierMask);

        if (H.Empty())
        {
            var concat = new Mat();
            Cv2.HConcat(new[] { imgA, imgB }, concat);
            return new StitchResult(concat, imgA.Width, 0, 0, false);
        }

        int inliers = Cv2.CountNonZero(inlierMask);

        // Approximate horizontal translation offset
        double avgDx = 0;
        double avgDy = 0;
        int count = 0;
        for (int i = 0; i < goodMatches.Length; i++)
        {
            if (inlierMask.At<byte>(i, 0) != 0)
            {
                avgDx += ptsA[i].X - ptsB[i].X;
                avgDy += ptsA[i].Y - ptsB[i].Y;
                count++;
            }
        }

        if (count > 0)
        {
            avgDx /= count;
            avgDy /= count;
        }

        // Warp Image B onto Image A coordinate space
        int outWidth = Math.Max(imgA.Width, (int)(imgA.Width + Math.Abs(avgDx)));
        int outHeight = Math.Max(imgA.Height, (int)(imgA.Height + Math.Abs(avgDy)));

        var stitched = new Mat(outHeight, outWidth, imgA.Type(), new Scalar(0));
        Cv2.WarpPerspective(imgB, stitched, H, new Size(outWidth, outHeight));

        // Copy Image A onto the canvas
        var roiA = new Rect(0, 0, imgA.Width, imgA.Height);
        using var subMat = new Mat(stitched, roiA);
        imgA.CopyTo(subMat);

        return new StitchResult(stitched, avgDx, avgDy, inliers, true);
    }

    /// <summary>
    /// Line-scan strip accumulator: appends continuous sensor scan lines into a full frame.
    /// </summary>
    public static Mat AccumulateLines(IReadOnlyList<Mat> lines, int targetWidth, int targetHeight)
    {
        var result = new Mat(targetHeight, targetWidth, MatType.CV_8UC1, new Scalar(0));
        int currentY = 0;

        foreach (var line in lines)
        {
            if (currentY + line.Rows > targetHeight)
                break;

            var destRoi = new Rect(0, currentY, Math.Min(targetWidth, line.Cols), line.Rows);
            using var sub = new Mat(result, destRoi);
            line.CopyTo(sub);
            currentY += line.Rows;
        }

        return result;
    }
}
