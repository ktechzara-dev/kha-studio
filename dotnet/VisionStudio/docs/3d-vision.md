# 3D Point Cloud & Depth Vision Architecture

## 1. Overview
The `VisionStudio.Imaging.ThreeD` module provides tools for analyzing structured-light and time-of-flight (ToF) depth cameras and point cloud data:
- **`PointCloud3D`**: Container supporting PLY/PCD import and export.
- **`PlaneFitTool`**: Least-squares plane fitting, peak-to-valley flatness, and volume measurement.
- **`DepthToImageTool`**: Normalizes depth maps into 8-bit 2D grayscale or pseudo-color images (Jet, Turbo, Viridis).

---

## 2. Flatness & Volume Inspection
- **Plane Fitting**: Calculates centroid $\mu$ and covariance matrix to derive the surface normal $(A, B, C)$ and offset $D$.
- **Flatness (Peak-to-Valley)**: Computes maximum positive and negative deviation of surface points from the fitted plane:
  $$\text{Flatness} = d_{\max} - d_{\min}$$
- **Volume**: Integrates signed distance above the reference plane across the defined region of interest.
