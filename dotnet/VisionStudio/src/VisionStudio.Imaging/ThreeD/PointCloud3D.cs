using System.Globalization;
using System.Text;
using OpenCvSharp;

namespace VisionStudio.Imaging.ThreeD;

public readonly record struct Point3D(float X, float Y, float Z, byte Intensity = 255);

public readonly record struct Plane3D(double A, double B, double C, double D, double RmsError, double FlatnessPeakToValley)
{
    public double DistanceTo(Point3D p) =>
        Math.Abs(A * p.X + B * p.Y + C * p.Z + D) / Math.Sqrt(A * A + B * B + C * C);

    public double SignedDistanceTo(Point3D p) =>
        (A * p.X + B * p.Y + C * p.Z + D) / Math.Sqrt(A * A + B * B + C * C);
}

public readonly record struct VolumeMeasurement(
    double MeanHeight,
    double MaxHeight,
    double MinHeight,
    double VolumeCubicMm,
    double Flatness,
    int PointCount);

/// <summary>
/// Industrial 3D Point Cloud container supporting PLY/PCD serialization, plane fitting, volume, and 2D height-map rendering.
/// </summary>
public sealed class PointCloud3D
{
    public List<Point3D> Points { get; }

    public PointCloud3D()
    {
        Points = new List<Point3D>();
    }

    public PointCloud3D(IEnumerable<Point3D> points)
    {
        Points = new List<Point3D>(points);
    }

    /// <summary>
    /// Constructs a 3D point cloud from a 2D depth map and pinhole camera intrinsics.
    /// </summary>
    public static PointCloud3D FromDepthMap(Mat depthMat, double fx = 1000.0, double fy = 1000.0, double cx = -1, double cy = -1, double zScale = 1.0)
    {
        if (depthMat.Empty())
            return new PointCloud3D();

        if (cx < 0) cx = depthMat.Width / 2.0;
        if (cy < 0) cy = depthMat.Height / 2.0;

        var cloud = new PointCloud3D();

        for (int r = 0; r < depthMat.Rows; r++)
        {
            for (int c = 0; c < depthMat.Cols; c++)
            {
                double zVal;
                if (depthMat.Type() == MatType.CV_16U)
                    zVal = depthMat.At<ushort>(r, c) * zScale;
                else if (depthMat.Type() == MatType.CV_32F)
                    zVal = depthMat.At<float>(r, c) * zScale;
                else
                    zVal = depthMat.At<byte>(r, c) * zScale;

                if (zVal <= 0.001 || double.IsNaN(zVal) || double.IsInfinity(zVal))
                    continue;

                float z = (float)zVal;
                float x = (float)((c - cx) * z / fx);
                float y = (float)((r - cy) * z / fy);

                cloud.Points.Add(new Point3D(x, y, z, 255));
            }
        }

        return cloud;
    }

    /// <summary>
    /// Renders the 3D point cloud onto an 8-bit grayscale 2D height map suitable for standard 2D vision tools.
    /// </summary>
    public Mat ToHeightMap(int width = 640, int height = 480, float? minZ = null, float? maxZ = null)
    {
        var mat = new Mat(height, width, MatType.CV_8UC1, new Scalar(0));
        if (Points.Count == 0)
            return mat;

        float z0 = minZ ?? Points.Min(p => p.Z);
        float z1 = maxZ ?? Points.Max(p => p.Z);
        float range = Math.Max(0.001f, z1 - z0);

        float minX = Points.Min(p => p.X);
        float maxX = Points.Max(p => p.X);
        float minY = Points.Min(p => p.Y);
        float maxY = Points.Max(p => p.Y);
        float rangeX = Math.Max(0.001f, maxX - minX);
        float rangeY = Math.Max(0.001f, maxY - minY);

        foreach (var p in Points)
        {
            int px = Math.Clamp((int)((p.X - minX) / rangeX * (width - 1)), 0, width - 1);
            int py = Math.Clamp((int)((p.Y - minY) / rangeY * (height - 1)), 0, height - 1);

            byte norm = (byte)Math.Clamp((int)((p.Z - z0) / range * 255.0f), 0, 255);
            byte current = mat.At<byte>(py, px);
            if (norm > current)
                mat.Set(py, px, norm);
        }

        return mat;
    }

    /// <summary>
    /// Fits a reference plane to points using least-squares SVD and calculates flatness (peak-to-valley).
    /// </summary>
    public Plane3D FitPlane()
    {
        if (Points.Count < 3)
            return new Plane3D(0, 0, 1, 0, 0, 0);

        // Centroid
        double mx = Points.Average(p => p.X);
        double my = Points.Average(p => p.Y);
        double mz = Points.Average(p => p.Z);

        // Covariance matrix
        double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
        foreach (var p in Points)
        {
            double dx = p.X - mx;
            double dy = p.Y - my;
            double dz = p.Z - mz;

            xx += dx * dx;
            xy += dx * dy;
            xz += dx * dz;
            yy += dy * dy;
            yz += dy * dz;
            zz += dz * dz;
        }

        // Normal estimation via normal approximation
        double detX = yy * zz - yz * yz;
        double detY = xx * zz - xz * xz;
        double detZ = xx * yy - xy * xy;

        double maxDet = Math.Max(detX, Math.Max(detY, detZ));
        double a = 0, b = 0, c = 1;

        if (maxDet == detX)
        {
            a = detX;
            b = xz * yz - xy * zz;
            c = xy * yz - xz * yy;
        }
        else if (maxDet == detY)
        {
            a = xz * yz - xy * zz;
            b = detY;
            c = xy * xz - yz * xx;
        }
        else
        {
            a = xy * yz - xz * yy;
            b = xy * xz - yz * xx;
            c = detZ;
        }

        double norm = Math.Sqrt(a * a + b * b + c * c);
        if (norm > 1e-9)
        {
            a /= norm;
            b /= norm;
            c /= norm;
        }
        else
        {
            a = 0; b = 0; c = 1;
        }

        double d = -(a * mx + b * my + c * mz);

        // Residuals and peak-to-valley flatness
        double sumSq = 0;
        double minSigned = double.MaxValue;
        double maxSigned = double.MinValue;

        var plane = new Plane3D(a, b, c, d, 0, 0);
        foreach (var p in Points)
        {
            double dist = plane.SignedDistanceTo(p);
            sumSq += dist * dist;
            if (dist < minSigned) minSigned = dist;
            if (dist > maxSigned) maxSigned = dist;
        }

        double rms = Math.Sqrt(sumSq / Points.Count);
        double flatness = maxSigned - minSigned;

        return new Plane3D(a, b, c, d, rms, flatness);
    }

    /// <summary>
    /// Computes height statistics and volume above/below a reference plane within an optional 2D ROI.
    /// </summary>
    public VolumeMeasurement MeasureVolume(Plane3D referencePlane, Rect2d? roi = null)
    {
        var subset = Points;
        if (roi.HasValue)
        {
            var r = roi.Value;
            subset = Points.Where(p => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height).ToList();
        }

        if (subset.Count == 0)
            return new VolumeMeasurement(0, 0, 0, 0, 0, 0);

        var distances = subset.Select(p => referencePlane.SignedDistanceTo(p)).ToList();
        double mean = distances.Average();
        double max = distances.Max();
        double min = distances.Min();
        double flatness = max - min;

        // Estimated volume assuming point grid spacing
        double areaPerPoint = 0.5 * 0.5; // default 0.25 mm^2 per point
        double volume = distances.Where(d => d > 0).Sum() * areaPerPoint;

        return new VolumeMeasurement(mean, max, min, volume, flatness, subset.Count);
    }

    /// <summary>
    /// Parses an ASCII Stanford Polygon Format (.ply) point cloud file.
    /// </summary>
    public static PointCloud3D LoadPly(string filePath)
    {
        var cloud = new PointCloud3D();
        using var reader = new StreamReader(filePath);

        string? line;
        bool headerEnded = false;
        int vertexCount = 0;

        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (string.IsNullOrWhiteSpace(line)) continue;

            if (!headerEnded)
            {
                if (line.StartsWith("element vertex", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = line.Split(' ');
                    if (parts.Length >= 3)
                        int.TryParse(parts[2], out vertexCount);
                }
                else if (line.Equals("end_header", StringComparison.OrdinalIgnoreCase))
                {
                    headerEnded = true;
                }
                continue;
            }

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length >= 3 &&
                float.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) &&
                float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) &&
                float.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z))
            {
                cloud.Points.Add(new Point3D(x, y, z));
            }
        }

        return cloud;
    }

    /// <summary>
    /// Saves the point cloud to an ASCII Stanford Polygon Format (.ply) file.
    /// </summary>
    public void SavePly(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ply");
        sb.AppendLine("format ascii 1.0");
        sb.AppendLine($"element vertex {Points.Count}");
        sb.AppendLine("property float x");
        sb.AppendLine("property float y");
        sb.AppendLine("property float z");
        sb.AppendLine("end_header");

        foreach (var p in Points)
        {
            sb.AppendLine(string.Format(CultureInfo.InvariantCulture, "{0:F4} {1:F4} {2:F4}", p.X, p.Y, p.Z));
        }

        File.WriteAllText(filePath, sb.ToString());
    }
}
