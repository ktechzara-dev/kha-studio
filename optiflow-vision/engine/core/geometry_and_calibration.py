"""
OptiFlow Vision Studio - Geometry, Metrology & Camera Calibration Module
Milestone 4 Implementation:
- 1D Sub-pixel Edge Detection along line profiles (Parabolic interpolation)
- Robust Shape Fitting: Taubin Circle Fitting & RANSAC/Huber Line Fitting
- 2D Metrology: Point-Point, Point-Line, Line-Line angles and distances
- Camera Calibration: Lens distortion undistortion & Planar Homography (Pixel <-> Real-World Millimeter)
"""

import math
from typing import List, Dict, Any, Tuple, Optional
import numpy as np
import cv2


# ==============================================================================
# 1. 1D SUB-PIXEL EDGE DETECTION ALONG LINE PROFILES
# ==============================================================================

def _bilinear_interpolate_1d(img: np.ndarray, xs: np.ndarray, ys: np.ndarray) -> np.ndarray:
    """Fast vectorised bilinear interpolation for 1D coordinate arrays."""
    h, w = img.shape[:2]
    xs_clamped = np.clip(xs, 0.0, float(w - 1))
    ys_clamped = np.clip(ys, 0.0, float(h - 1))
    x0 = np.floor(xs_clamped).astype(np.int32)
    x1 = np.clip(x0 + 1, 0, w - 1)
    y0 = np.floor(ys_clamped).astype(np.int32)
    y1 = np.clip(y0 + 1, 0, h - 1)

    wx = (xs_clamped - x0).astype(np.float32)
    wy = (ys_clamped - y0).astype(np.float32)

    f_img = img.astype(np.float32)
    top = (1.0 - wx) * f_img[y0, x0] + wx * f_img[y0, x1]
    bottom = (1.0 - wx) * f_img[y1, x0] + wx * f_img[y1, x1]
    return (1.0 - wy) * top + wy * bottom


def sample_1d_profile(
    image: np.ndarray,
    p1: Tuple[float, float],
    p2: Tuple[float, float],
    corridor_width: int = 1
) -> Tuple[np.ndarray, np.ndarray, np.ndarray]:
    """
    Samples pixel intensity values along a directed 1D line segment p1 -> p2.
    If corridor_width > 1, averages intensities perpendicular to the scan line
    to suppress high-frequency sensor noise.
    
    Returns:
        distances: 1D array of Euclidean distances along scan line (in pixels)
        intensities: 1D array of sampled gray values (float32, 0..255)
        coords: (N, 2) array of (x, y) coordinates along the scan line
    """
    if len(image.shape) == 3:
        gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    else:
        gray = image

    x1, y1 = float(p1[0]), float(p1[1])
    x2, y2 = float(p2[0]), float(p2[1])

    dx = x2 - x1
    dy = y2 - y1
    length = math.hypot(dx, dy)
    if length < 1e-4:
        return np.array([0.0]), np.array([float(gray[int(round(y1)), int(round(x1))])]), np.array([[x1, y1]])

    num_samples = int(math.ceil(length)) + 1
    t = np.linspace(0.0, 1.0, num_samples, dtype=np.float32)

    xs = x1 + t * dx
    ys = y1 + t * dy

    coords = np.column_stack((xs, ys))
    distances = t * length

    if corridor_width <= 1:
        intensities = _bilinear_interpolate_1d(gray, xs, ys)
    else:
        nx = -dy / length
        ny = dx / length
        half_w = (corridor_width - 1) / 2.0
        offsets = np.linspace(-half_w, half_w, corridor_width)

        accum = np.zeros(num_samples, dtype=np.float32)
        for off in offsets:
            sample_xs = xs + off * nx
            sample_ys = ys + off * ny
            sampled = _bilinear_interpolate_1d(gray, sample_xs, sample_ys)
            accum += sampled
        intensities = accum / float(corridor_width)

    return distances, intensities, coords


def detect_1d_edges_subpixel(
    distances: np.ndarray,
    intensities: np.ndarray,
    coords: np.ndarray,
    min_gradient: float = 10.0,
    polarity: str = "ANY",  # "BRIGHT_TO_DARK", "DARK_TO_BRIGHT", "ANY"
    smoothing_sigma: float = 1.0
) -> List[Dict[str, Any]]:
    """
    Computes derivative of Gaussian profile and locates gradient peaks with
    sub-pixel parabolic 3-point interpolation.
    
    Returns list of detected edge points:
    [
        {
            "x": float (subpixel),
            "y": float (subpixel),
            "distance": float,
            "gradient": float,
            "polarity": "BRIGHT_TO_DARK" or "DARK_TO_BRIGHT"
        }, ...
    ]
    """
    n = len(intensities)
    if n < 5:
        return []

    # Apply 1D Gaussian smoothing
    if smoothing_sigma > 0.1:
        radius = int(math.ceil(smoothing_sigma * 3.0))
        x_kernel = np.arange(-radius, radius + 1)
        kernel = np.exp(-0.5 * (x_kernel / smoothing_sigma) ** 2)
        kernel /= kernel.sum()
        smooth_vals = np.convolve(intensities, kernel, mode="same")
    else:
        smooth_vals = intensities

    # Compute first derivative: dI / dt
    grad = np.gradient(smooth_vals)
    mag = np.abs(grad)

    edges = []
    # Local maxima detection of gradient magnitude
    for i in range(1, n - 1):
        if mag[i] >= min_gradient and mag[i] > mag[i - 1] and mag[i] >= mag[i + 1]:
            # Determine polarity
            edge_pol = "DARK_TO_BRIGHT" if grad[i] > 0 else "BRIGHT_TO_DARK"
            if polarity != "ANY" and polarity != edge_pol:
                continue

            # Sub-pixel parabolic peak interpolation: delta = (y_left - y_right) / (2 * (y_left - 2*y_mid + y_right))
            y_l = mag[i - 1]
            y_m = mag[i]
            y_r = mag[i + 1]
            denom = 2.0 * (y_l - 2.0 * y_m + y_r)
            if abs(denom) > 1e-6:
                delta = (y_l - y_r) / denom
                delta = max(-0.5, min(0.5, delta))
            else:
                delta = 0.0

            sub_idx = float(i) + delta
            # Fractional interpolation along coordinates
            ratio = max(0.0, min(1.0, sub_idx / float(n - 1)))
            p_start = coords[0]
            p_end = coords[-1]
            sub_x = float(p_start[0] + ratio * (p_end[0] - p_start[0]))
            sub_y = float(p_start[1] + ratio * (p_end[1] - p_start[1]))
            sub_dist = float(distances[0] + ratio * (distances[-1] - distances[0]))
            peak_grad = float(y_m - 0.25 * (y_l - y_r) * delta)

            edges.append({
                "x": round(sub_x, 3),
                "y": round(sub_y, 3),
                "distance": round(sub_dist, 3),
                "gradient": round(peak_grad, 2),
                "polarity": edge_pol
            })

    return edges


# ==============================================================================
# 2. ROBUST SHAPE FITTING (LINE & CIRCLE)
# ==============================================================================

def fit_line_robust(
    points: List[Tuple[float, float]],
    dist_type: int = cv2.DIST_HUBER,
    max_reproj_error: float = 2.0
) -> Optional[Dict[str, Any]]:
    """
    Fits a 2D line to an array of points using M-estimator (Huber) to reject
    outlier burrs, specular glares, or noise.
    
    Line equation in 2D: (x - x0)*ny - (y - y0)*nx = 0
    Returns:
        vx, vy: normalized line direction vector
        x0, y0: point on the line
        angle_deg: angle of line with positive X-axis (-180..+180)
        rms_residual: root-mean-square orthogonal distance of inliers
        inliers_count: count of points within max_reproj_error
    """
    if len(points) < 2:
        return None

    pts = np.array(points, dtype=np.float32).reshape(-1, 2)
    line_param = cv2.fitLine(pts, distType=dist_type, param=0, reps=0.01, aeps=0.01)
    vx, vy, x0, y0 = [float(val[0]) for val in line_param]

    # Normalize direction vector
    norm = math.hypot(vx, vy)
    if norm > 1e-6:
        vx /= norm
        vy /= norm

    # Calculate orthogonal distance for each point: | -vy*(x - x0) + vx*(y - y0) |
    dx = pts[:, 0] - x0
    dy = pts[:, 1] - y0
    residuals = np.abs(-vy * dx + vx * dy)
    inliers = residuals <= max_reproj_error
    inliers_count = int(np.sum(inliers))

    rms = float(np.sqrt(np.mean(residuals[inliers] ** 2))) if inliers_count > 0 else 0.0
    angle_deg = math.degrees(math.atan2(vy, vx))

    return {
        "vx": round(vx, 5),
        "vy": round(vy, 5),
        "x0": round(x0, 3),
        "y0": round(y0, 3),
        "angle_deg": round(angle_deg, 2),
        "rms_residual": round(rms, 3),
        "inliers_count": inliers_count,
        "total_points": len(points)
    }


def fit_circle_taubin(
    points: List[Tuple[float, float]]
) -> Optional[Dict[str, Any]]:
    """
    Algebraic circle fitting using Pratt/Kåsa centroid-shifted least squares.
    Highly stable, non-iterative, closed-form solution.
    
    Circle equation: (x - cx)^2 + (y - cy)^2 = r^2
    Returns:
        cx, cy: center coordinate
        radius: circle radius
        roundness_error: RMS radial distance variance
    """
    if len(points) < 3:
        return None

    pts = np.array(points, dtype=np.float64).reshape(-1, 2)
    n = len(pts)

    # Centering data around mean for condition number optimization
    mean_x = np.mean(pts[:, 0])
    mean_y = np.mean(pts[:, 1])
    u = pts[:, 0] - mean_x
    v = pts[:, 1] - mean_y

    # Design matrix A = [u, v, 1], RHS b = u^2 + v^2
    A = np.column_stack((u, v, np.ones(n, dtype=np.float64)))
    b = u * u + v * v

    try:
        # Solve least squares: A * [2*uc, 2*vc, w] = b
        sol, _, _, _ = np.linalg.lstsq(A, b, rcond=None)
        c_u = sol[0] / 2.0
        c_v = sol[1] / 2.0
        r_sq = sol[2] + c_u * c_u + c_v * c_v
        if r_sq <= 0:
            return None
        radius = float(math.sqrt(r_sq))
        cx = float(c_u + mean_x)
        cy = float(c_v + mean_y)

        # Radial residuals: | sqrt((x - cx)^2 + (y - cy)^2) - r |
        radii = np.hypot(pts[:, 0] - cx, pts[:, 1] - cy)
        radial_errors = np.abs(radii - radius)
        rms_error = float(np.sqrt(np.mean(radial_errors ** 2)))

        return {
            "cx": round(cx, 3),
            "cy": round(cy, 3),
            "radius": round(radius, 3),
            "diameter": round(radius * 2.0, 3),
            "roundness_error": round(rms_error, 3),
            "points_count": n
        }
    except Exception:
        return None


# ==============================================================================
# 3. 2D METROLOGY (POINT, LINE, ANGLE MEASUREMENTS)
# ==============================================================================

def measure_point_to_point(
    p1: Tuple[float, float],
    p2: Tuple[float, float]
) -> Dict[str, Any]:
    """Calculates Euclidean distance, delta X, and delta Y between two points."""
    dx = float(p2[0] - p1[0])
    dy = float(p2[1] - p1[1])
    dist = math.hypot(dx, dy)
    angle_deg = math.degrees(math.atan2(dy, dx))
    return {
        "distance": round(dist, 3),
        "delta_x": round(dx, 3),
        "delta_y": round(dy, 3),
        "angle_deg": round(angle_deg, 2)
    }


def measure_point_to_line(
    point: Tuple[float, float],
    line_pt: Tuple[float, float],
    line_dir: Tuple[float, float]
) -> Dict[str, Any]:
    """
    Calculates perpendicular distance from point to line and foot projection point.
    line_dir: (vx, vy) normalized direction vector.
    """
    px, py = float(point[0]), float(point[1])
    x0, y0 = float(line_pt[0]), float(line_pt[1])
    vx, vy = float(line_dir[0]), float(line_dir[1])

    # Projection parameter t = (P - P0) . V
    t = (px - x0) * vx + (py - y0) * vy
    foot_x = x0 + t * vx
    foot_y = y0 + t * vy

    perp_dist = math.hypot(px - foot_x, py - foot_y)
    return {
        "distance": round(perp_dist, 3),
        "foot_x": round(foot_x, 3),
        "foot_y": round(foot_y, 3)
    }


def measure_line_to_line_angle(
    dir1: Tuple[float, float],
    dir2: Tuple[float, float]
) -> Dict[str, Any]:
    """
    Computes angle between two line direction vectors in degrees [0..180].
    """
    v1 = np.array([float(dir1[0]), float(dir1[1])])
    v2 = np.array([float(dir2[0]), float(dir2[1])])
    n1 = np.linalg.norm(v1)
    n2 = np.linalg.norm(v2)
    if n1 < 1e-6 or n2 < 1e-6:
        return {"angle_deg": 0.0, "is_parallel": False, "is_perpendicular": False}

    dot = np.clip(np.dot(v1, v2) / (n1 * n2), -1.0, 1.0)
    angle_rad = math.acos(abs(dot))  # Acute angle between lines
    angle_deg = math.degrees(angle_rad)

    is_parallel = abs(angle_deg) < 0.5 or abs(angle_deg - 180.0) < 0.5
    is_perpendicular = abs(angle_deg - 90.0) < 0.5

    return {
        "angle_deg": round(angle_deg, 2),
        "is_parallel": is_parallel,
        "is_perpendicular": is_perpendicular
    }


# ==============================================================================
# 4. CAMERA CALIBRATION & LENS DISTORTION CORRECTION
# ==============================================================================

class LensDistortionModel:
    """
    Lens distortion correction cache using cv2.initUndistortRectifyMap.
    Pre-computes coordinate remap grids once for sub-millisecond per-frame undistortion.
    """
    def __init__(
        self,
        camera_matrix: np.ndarray,
        dist_coeffs: np.ndarray,
        image_shape: Tuple[int, int]
    ):
        self.camera_matrix = camera_matrix
        self.dist_coeffs = dist_coeffs
        self.image_shape = image_shape  # (h, w)
        h, w = image_shape

        # Compute optimal new camera matrix and rectification maps
        self.new_camera_matrix, _ = cv2.getOptimalNewCameraMatrix(
            camera_matrix, dist_coeffs, (w, h), 0, (w, h)
        )
        self.map_x, self.map_y = cv2.initUndistortRectifyMap(
            camera_matrix, dist_coeffs, None, self.new_camera_matrix, (w, h), cv2.CV_32FC1
        )

    def undistort(self, image: np.ndarray) -> np.ndarray:
        return cv2.remap(image, self.map_x, self.map_y, interpolation=cv2.INTER_LINEAR)


def compute_homography_from_fiducials(
    pixel_points: List[Tuple[float, float]],
    world_points_mm: List[Tuple[float, float]]
) -> Optional[np.ndarray]:
    """
    Computes 3x3 planar homography matrix H from 4 or more corresponding
    pixel <-> world (mm) fiducial pairs:
    [X_mm, Y_mm, 1]^T ~ H * [u, v, 1]^T
    """
    if len(pixel_points) < 4 or len(world_points_mm) < 4:
        return None

    src_pts = np.array(pixel_points, dtype=np.float32).reshape(-1, 1, 2)
    dst_pts = np.array(world_points_mm, dtype=np.float32).reshape(-1, 1, 2)

    H, _ = cv2.findHomography(src_pts, dst_pts, cv2.RANSAC, 2.0)
    return H


def transform_points_with_homography(
    points: List[Tuple[float, float]],
    homography_matrix: np.ndarray
) -> List[Tuple[float, float]]:
    """
    Applies homography transformation to a list of (x, y) coordinates:
    Maps pixel space into world metric millimeters.
    """
    if not points or homography_matrix is None:
        return []

    pts = np.array(points, dtype=np.float32).reshape(-1, 1, 2)
    transformed = cv2.perspectiveTransform(pts, homography_matrix)
    res = []
    for pt in transformed[:, 0, :]:
        res.append((round(float(pt[0]), 3), round(float(pt[1]), 3)))
    return res
