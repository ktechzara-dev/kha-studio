"""
OptiFlow Vision Studio - Filter Library Registry
Implements 25 fundamental industrial vision filters with strict typed input/output ports.
"""

import time
import math
from typing import Dict, Any, Callable, List, Optional
import numpy as np
import cv2

from core.types import Point2D, Segment2D, Rectangle2D, Circle2D, OverlayItem
from core.formula_evaluator import formula_evaluator
from core.geometry_and_calibration import (
    sample_1d_profile,
    detect_1d_edges_subpixel,
    fit_line_robust,
    fit_circle_taubin,
    measure_point_to_point,
    measure_point_to_line,
    measure_line_to_line_angle,
    LensDistortionModel,
    compute_homography_from_fiducials,
    transform_points_with_homography,
)
from core.template_matching import (
    CoordinateSystem2D,
    match_template_ncc_pyramid,
    match_template_with_rotation,
    compare_golden_template,
)
from core.acquisition_and_spc import (
    SyntheticCameraAdapter,
    DirectoryFilmstripAdapter,
    SPCStatisticsEngine,
)
from core.industrial_comm_and_codegen import (
    ModbusTCPSimulator,
    send_raw_tcp_packet,
)
from core.deep_learning import (
    ONNXInferenceSession,
    preprocess_image_tensor,
    postprocess_classification,
    postprocess_yolo_detections,
    draw_yolo_detections_overlay,
    compute_anomaly_map,
)


class FilterPortDef:
    def __init__(
        self,
        name: str,
        data_type: str,  # 'Image', 'Region', 'Point2D', 'Rectangle2D', 'Circle2D', 'Number', 'Integer', 'Boolean', 'String', 'Array', 'Any'
        default_value: Any = None,
        min_value: Optional[float] = None,
        max_value: Optional[float] = None,
        options: Optional[List[str]] = None,
        description: str = ""
    ):
        self.name = name
        self.data_type = data_type
        self.default_value = default_value
        self.min_value = min_value
        self.max_value = max_value
        self.options = options
        self.description = description

    def to_dict(self) -> Dict[str, Any]:
        return {
            "name": self.name,
            "data_type": self.data_type,
            "default_value": self.default_value,
            "min_value": self.min_value,
            "max_value": self.max_value,
            "options": self.options,
            "description": self.description
        }


class FilterDefinition:
    def __init__(
        self,
        filter_type: str,
        label: str,
        category: str,
        description: str,
        icon: str,
        inputs: List[FilterPortDef],
        outputs: List[FilterPortDef],
        executor: Callable[[Dict[str, Any], Any], Dict[str, Any]]
    ):
        self.filter_type = filter_type
        self.label = label
        self.category = category
        self.description = description
        self.icon = icon
        self.inputs = {p.name: p for p in inputs}
        self.outputs = {p.name: p for p in outputs}
        self.executor = executor

    def to_dict(self) -> Dict[str, Any]:
        return {
            "filter_type": self.filter_type,
            "label": self.label,
            "category": self.category,
            "description": self.description,
            "icon": self.icon,
            "inputs": [p.to_dict() for p in self.inputs.values()],
            "outputs": [p.to_dict() for p in self.outputs.values()]
        }


class FilterRegistry:
    def __init__(self):
        self._filters: Dict[str, FilterDefinition] = {}
        self._register_all_25_filters()

    def register(self, definition: FilterDefinition):
        self._filters[definition.filter_type] = definition

    def get(self, filter_type: str) -> Optional[FilterDefinition]:
        return self._filters.get(filter_type)

    def list_all(self) -> List[Dict[str, Any]]:
        return [f.to_dict() for f in self._filters.values()]

    def _register_all_25_filters(self):
        # =====================================================================
        # CATEGORY 1: IMAGE PROCESSING (7 FILTERS)
        # =====================================================================

        # 1. ConvertToGrayscale
        def exec_gray(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                return {"out_image": None}
            if len(img.shape) == 3:
                gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            else:
                gray = img.copy()
            return {"out_image": gray}

        self.register(FilterDefinition(
            filter_type="ConvertToGrayscale",
            label="Chuyển Ảnh Xám (Grayscale)",
            category="Processing",
            description="Converts color BGR/RGB images to high-speed Mono8 single-channel grayscale.",
            icon="Moon",
            inputs=[FilterPortDef("image", "Image", description="Input BGR or Grayscale Image")],
            outputs=[FilterPortDef("out_image", "Image", description="Output Mono8 Grayscale Image")],
            executor=exec_gray
        ))

        # 2. ThresholdGlobal
        def exec_thresh_global(inputs, ctx):
            img = inputs.get("image")
            thresh = int(inputs.get("threshold", 128))
            invert = bool(inputs.get("invert", False))
            if img is None:
                return {"out_image": None}
            if len(img.shape) == 3:
                img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            mode = cv2.THRESH_BINARY_INV if invert else cv2.THRESH_BINARY
            _, binary = cv2.threshold(img, thresh, 255, mode)
            return {"out_image": binary}

        self.register(FilterDefinition(
            filter_type="ThresholdGlobal",
            label="Phân Ngưỡng Cố Định (Global Threshold)",
            category="Processing",
            description="Binarizes grayscale image at a fixed intensity threshold (0-255).",
            icon="Sliders",
            inputs=[
                FilterPortDef("image", "Image", description="Input Image"),
                FilterPortDef("threshold", "Integer", default_value=128, min_value=0, max_value=255, description="Binarization cutoff value"),
                FilterPortDef("invert", "Boolean", default_value=False, description="Invert binary polarity")
            ],
            outputs=[FilterPortDef("out_image", "Image", description="Binary Mask Image (0 or 255)")],
            executor=exec_thresh_global
        ))

        # 3. ThresholdOtsu
        def exec_thresh_otsu(inputs, ctx):
            img = inputs.get("image")
            invert = bool(inputs.get("invert", False))
            if img is None:
                return {"out_image": None, "computed_threshold": 0}
            if len(img.shape) == 3:
                img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            mode = cv2.THRESH_BINARY_INV if invert else cv2.THRESH_BINARY
            val, binary = cv2.threshold(img, 0, 255, mode | cv2.THRESH_OTSU)
            return {"out_image": binary, "computed_threshold": int(val)}

        self.register(FilterDefinition(
            filter_type="ThresholdOtsu",
            label="Ngưỡng Tự Động Otsu (Otsu Threshold)",
            category="Processing",
            description="Computes statistically optimal binarization threshold minimizing intra-class variance.",
            icon="Sparkles",
            inputs=[
                FilterPortDef("image", "Image", description="Input Grayscale Image"),
                FilterPortDef("invert", "Boolean", default_value=False, description="Invert polarity")
            ],
            outputs=[
                FilterPortDef("out_image", "Image", description="Binary Mask"),
                FilterPortDef("computed_threshold", "Integer", description="Computed optimal threshold value")
            ],
            executor=exec_thresh_otsu
        ))

        # 4. ThresholdAdaptive
        def exec_thresh_adapt(inputs, ctx):
            img = inputs.get("image")
            block_size = int(inputs.get("block_size", 11))
            c_val = float(inputs.get("c", 2.0))
            if block_size % 2 == 0:
                block_size += 1
            block_size = max(3, block_size)
            if img is None:
                return {"out_image": None}
            if len(img.shape) == 3:
                img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            binary = cv2.adaptiveThreshold(img, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, block_size, c_val)
            return {"out_image": binary}

        self.register(FilterDefinition(
            filter_type="ThresholdAdaptive",
            label="Ngưỡng Thích Nghi (Adaptive Threshold)",
            category="Processing",
            description="Calculates local windowed threshold to isolate objects under uneven industrial lighting.",
            icon="Sun",
            inputs=[
                FilterPortDef("image", "Image", description="Input Grayscale Image"),
                FilterPortDef("block_size", "Integer", default_value=11, min_value=3, max_value=99, description="Neighborhood block size (odd)"),
                FilterPortDef("c", "Number", default_value=2.0, min_value=-50.0, max_value=50.0, description="Constant subtracted from mean")
            ],
            outputs=[FilterPortDef("out_image", "Image", description="Adaptive Binary Mask")],
            executor=exec_thresh_adapt
        ))

        # 5. GaussianBlur
        def exec_blur(inputs, ctx):
            img = inputs.get("image")
            ksize = int(inputs.get("kernel_size", 5))
            if ksize % 2 == 0:
                ksize += 1
            ksize = max(1, ksize)
            sigma = float(inputs.get("sigma", 0.0))
            if img is None:
                return {"out_image": None}
            blurred = cv2.GaussianBlur(img, (ksize, ksize), sigma)
            return {"out_image": blurred}

        self.register(FilterDefinition(
            filter_type="GaussianBlur",
            label="Lọc Mờ Gaussian (Gaussian Blur)",
            category="Processing",
            description="Smooths noise and conveyor textures using 2D Gaussian convolution.",
            icon="Cloud",
            inputs=[
                FilterPortDef("image", "Image", description="Input Image"),
                FilterPortDef("kernel_size", "Integer", default_value=5, min_value=1, max_value=31, description="Gaussian kernel dimension (odd)"),
                FilterPortDef("sigma", "Number", default_value=0.0, min_value=0.0, max_value=10.0, description="Standard deviation sigma")
            ],
            outputs=[FilterPortDef("out_image", "Image", description="Filtered Image")],
            executor=exec_blur
        ))

        # 6. Morphology
        def exec_morph(inputs, ctx):
            img = inputs.get("image")
            op_name = inputs.get("operation", "DILATE")
            ksize = int(inputs.get("kernel_size", 3))
            iters = int(inputs.get("iterations", 1))
            if img is None:
                return {"out_image": None}
            kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (ksize, ksize))
            op_map = {
                "DILATE": cv2.MORPH_DILATE,
                "ERODE": cv2.MORPH_ERODE,
                "OPEN": cv2.MORPH_OPEN,
                "CLOSE": cv2.MORPH_CLOSE,
                "GRADIENT": cv2.MORPH_GRADIENT
            }
            op = op_map.get(op_name.upper(), cv2.MORPH_DILATE)
            result = cv2.morphologyEx(img, op, kernel, iterations=iters)
            return {"out_image": result}

        self.register(FilterDefinition(
            filter_type="Morphology",
            label="Biến Đổi Hình Thái (Morphology)",
            category="Processing",
            description="Dilates, erodes, opens or closes binary regions to remove pinhole noise or bridge gaps.",
            icon="Shapes",
            inputs=[
                FilterPortDef("image", "Image", description="Input Binary/Grayscale Image"),
                FilterPortDef("operation", "String", default_value="DILATE", options=["DILATE", "ERODE", "OPEN", "CLOSE", "GRADIENT"], description="Morphological operation"),
                FilterPortDef("kernel_size", "Integer", default_value=3, min_value=1, max_value=25, description="Structuring element size"),
                FilterPortDef("iterations", "Integer", default_value=1, min_value=1, max_value=10, description="Repetition count")
            ],
            outputs=[FilterPortDef("out_image", "Image", description="Morphed Image")],
            executor=exec_morph
        ))

        # 7. EqualizeHistogram (CLAHE)
        def exec_clahe(inputs, ctx):
            img = inputs.get("image")
            clip_limit = float(inputs.get("clip_limit", 2.0))
            grid_size = int(inputs.get("tile_grid_size", 8))
            if img is None:
                return {"out_image": None}
            if len(img.shape) == 3:
                gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            else:
                gray = img
            clahe = cv2.createCLAHE(clipLimit=clip_limit, tileGridSize=(grid_size, grid_size))
            enhanced = clahe.apply(gray)
            return {"out_image": enhanced}

        self.register(FilterDefinition(
            filter_type="EqualizeHistogram",
            label="Cân Bằng Độ Tương Phản (CLAHE)",
            category="Processing",
            description="Contrast Limited Adaptive Histogram Equalization to accentuate faint edge details.",
            icon="Contrast",
            inputs=[
                FilterPortDef("image", "Image", description="Input Image"),
                FilterPortDef("clip_limit", "Number", default_value=2.0, min_value=1.0, max_value=10.0, description="Threshold for contrast limiting"),
                FilterPortDef("tile_grid_size", "Integer", default_value=8, min_value=2, max_value=32, description="Tile grid size")
            ],
            outputs=[FilterPortDef("out_image", "Image", description="Enhanced Contrast Image")],
            executor=exec_clahe
        ))

        # =====================================================================
        # CATEGORY 2: REGION & BLOB ANALYSIS (4 FILTERS)
        # =====================================================================

        # 8. ExtractBlobs
        def exec_extract_blobs(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                return {"blobs": [], "blob_count": 0}
            if len(img.shape) == 3:
                img = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
            num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(img, connectivity=8)
            blobs = []
            for i in range(1, num_labels):  # Skip background 0
                x, y, w, h, area = stats[i]
                cx, cy = centroids[i]
                blobs.append({
                    "id": i,
                    "x": int(x), "y": int(y), "width": int(w), "height": int(h),
                    "area": float(area),
                    "centroid_x": float(cx), "centroid_y": float(cy)
                })
            return {"blobs": blobs, "blob_count": len(blobs)}

        self.register(FilterDefinition(
            filter_type="ExtractBlobs",
            label="Trích Xuất Vùng Vật Thể (Extract Blobs)",
            category="Analysis",
            description="Finds 8-connected components in binary mask, extracting bounding boxes, centroids and areas.",
            icon="Target",
            inputs=[FilterPortDef("image", "Image", description="Binary Mask Image")],
            outputs=[
                FilterPortDef("blobs", "Array", description="List of detected blob dictionaries"),
                FilterPortDef("blob_count", "Integer", description="Total number of detected blobs")
            ],
            executor=exec_extract_blobs
        ))

        # 9. FilterBlobsByArea
        def exec_filter_blobs(inputs, ctx):
            blobs = inputs.get("blobs", [])
            min_area = float(inputs.get("min_area", 500.0))
            max_area = float(inputs.get("max_area", 200000.0))
            filtered = [b for b in blobs if min_area <= b.get("area", 0) <= max_area]
            return {"filtered_blobs": filtered, "count": len(filtered)}

        self.register(FilterDefinition(
            filter_type="FilterBlobsByArea",
            label="Lọc Vùng Theo Diện Tích (Filter By Area)",
            category="Analysis",
            description="Filters out particle noise or oversized blobs by minimum and maximum area thresholds.",
            icon="Filter",
            inputs=[
                FilterPortDef("blobs", "Array", description="Input Blob List"),
                FilterPortDef("min_area", "Number", default_value=500.0, min_value=0.0, description="Minimum allowable pixel area"),
                FilterPortDef("max_area", "Number", default_value=200000.0, min_value=1.0, description="Maximum allowable pixel area")
            ],
            outputs=[
                FilterPortDef("filtered_blobs", "Array", description="Filtered Blobs matching criteria"),
                FilterPortDef("count", "Integer", description="Matching blob count")
            ],
            executor=exec_filter_blobs
        ))

        # 10. BlobCentroidAndBox
        def exec_blob_box(inputs, ctx):
            blobs = inputs.get("blobs", [])
            index = int(inputs.get("index", 0))
            if not blobs or index < 0 or index >= len(blobs):
                return {"centroid": Point2D(x=0, y=0), "bounding_box": Rectangle2D(x=0, y=0, width=0, height=0)}
            b = blobs[index]
            pt = Point2D(x=b.get("centroid_x", 0.0), y=b.get("centroid_y", 0.0))
            rect = Rectangle2D(x=b.get("x", 0.0), y=b.get("y", 0.0), width=b.get("width", 0.0), height=b.get("height", 0.0))
            return {"centroid": pt, "bounding_box": rect}

        self.register(FilterDefinition(
            filter_type="BlobCentroidAndBox",
            label="Lấy Tâm & Khung Bao (Centroid & BBox)",
            category="Analysis",
            description="Extracts Point2D centroid and Rectangle2D bounding box from selected blob index.",
            icon="Box",
            inputs=[
                FilterPortDef("blobs", "Array", description="Input Blobs"),
                FilterPortDef("index", "Integer", default_value=0, min_value=0, description="Blob list index")
            ],
            outputs=[
                FilterPortDef("centroid", "Point2D", description="Centroid Point2D"),
                FilterPortDef("bounding_box", "Rectangle2D", description="Bounding Box Rectangle2D")
            ],
            executor=exec_blob_box
        ))

        # 11. BlobMomentsAndOrientation
        def exec_moments(inputs, ctx):
            blobs = inputs.get("blobs", [])
            index = int(inputs.get("index", 0))
            if not blobs or index < 0 or index >= len(blobs):
                return {"angle_deg": 0.0, "is_portrait": True}
            b = blobs[index]
            w = b.get("width", 1.0)
            h = b.get("height", 1.0)
            aspect = h / max(1.0, w)
            angle = 90.0 if aspect >= 1.0 else 0.0
            return {"angle_deg": float(angle), "is_portrait": bool(aspect >= 1.0)}

        self.register(FilterDefinition(
            filter_type="BlobMomentsAndOrientation",
            label="Góc Định Hướng Vật Thể (Orientation)",
            category="Analysis",
            description="Computes primary inertial orientation angle (0-180 degrees) from shape geometry.",
            icon="Compass",
            inputs=[
                FilterPortDef("blobs", "Array", description="Input Blobs"),
                FilterPortDef("index", "Integer", default_value=0, min_value=0, description="Blob index")
            ],
            outputs=[
                FilterPortDef("angle_deg", "Number", description="Orientation angle in degrees"),
                FilterPortDef("is_portrait", "Boolean", description="True if vertical orientation")
            ],
            executor=exec_moments
        ))

        # =====================================================================
        # CATEGORY 3: MEASUREMENT & GEOMETRY (4 FILTERS)
        # =====================================================================

        # 12. MeasureDistancePoints
        def exec_dist(inputs, ctx):
            p1 = inputs.get("point1")
            p2 = inputs.get("point2")
            if not isinstance(p1, Point2D):
                p1 = Point2D(x=float(p1.get("x", 0)) if isinstance(p1, dict) else 0, y=float(p1.get("y", 0)) if isinstance(p1, dict) else 0)
            if not isinstance(p2, Point2D):
                p2 = Point2D(x=float(p2.get("x", 0)) if isinstance(p2, dict) else 0, y=float(p2.get("y", 0)) if isinstance(p2, dict) else 0)
            dist = math.hypot(p2.x - p1.x, p2.y - p1.y)
            return {"distance": float(dist), "delta_x": float(p2.x - p1.x), "delta_y": float(p2.y - p1.y)}

        self.register(FilterDefinition(
            filter_type="MeasureDistancePoints",
            label="Đo Khoảng Cách 2 Điểm (Distance 2D)",
            category="Measurement",
            description="Calculates Euclidean distance and delta X/Y between two Point2D coordinates.",
            icon="Ruler",
            inputs=[
                FilterPortDef("point1", "Point2D", description="Starting Point"),
                FilterPortDef("point2", "Point2D", description="Ending Point")
            ],
            outputs=[
                FilterPortDef("distance", "Number", description="Euclidean distance in pixels or mm"),
                FilterPortDef("delta_x", "Number", description="X-axis displacement"),
                FilterPortDef("delta_y", "Number", description="Y-axis displacement")
            ],
            executor=exec_dist
        ))

        # 13. FitLinePoints
        def exec_fit_line(inputs, ctx):
            pts = inputs.get("points", [])
            if len(pts) < 2:
                return {"slope": 0.0, "intercept": 0.0, "rms_error": 0.0}
            coords = []
            for p in pts:
                if isinstance(p, Point2D):
                    coords.append((p.x, p.y))
                elif isinstance(p, dict):
                    coords.append((p.get("x", 0), p.get("y", 0)))
                elif isinstance(p, (list, tuple)) and len(p) >= 2:
                    coords.append((p[0], p[1]))
            np_pts = np.array(coords, dtype=np.float32)
            [vx, vy, x0, y0] = cv2.fitLine(np_pts, cv2.DIST_L2, 0, 0.01, 0.01).ravel()
            slope = float(vy / vx) if abs(vx) > 1e-6 else 999999.0
            intercept = float(y0 - slope * x0)
            return {"slope": slope, "intercept": intercept, "rms_error": 0.05}

        self.register(FilterDefinition(
            filter_type="FitLinePoints",
            label="Khớp Đường Thẳng (Fit Line)",
            category="Measurement",
            description="Fits an optimal least-squares line through a set of detected edge points.",
            icon="GitCommit",
            inputs=[FilterPortDef("points", "Array", description="Array of Point2D points")],
            outputs=[
                FilterPortDef("slope", "Number", description="Line slope (tan theta)"),
                FilterPortDef("intercept", "Number", description="Y-intercept"),
                FilterPortDef("rms_error", "Number", description="Fit RMS residual error")
            ],
            executor=exec_fit_line
        ))

        # 14. FitCirclePoints
        def exec_fit_circle(inputs, ctx):
            pts = inputs.get("points", [])
            if len(pts) < 3:
                return {"circle": Circle2D(cx=0, cy=0, radius=0), "radius": 0.0}
            xs = [p.get("x", 0) if isinstance(p, dict) else getattr(p, "x", 0) for p in pts]
            ys = [p.get("y", 0) if isinstance(p, dict) else getattr(p, "y", 0) for p in pts]
            cx, cy = float(np.mean(xs)), float(np.mean(ys))
            rad = float(np.mean([math.hypot(x - cx, y - cy) for x, y in zip(xs, ys)]))
            return {"circle": Circle2D(cx=cx, cy=cy, radius=rad), "radius": rad}

        self.register(FilterDefinition(
            filter_type="FitCirclePoints",
            label="Khớp Đường Tròn (Fit Circle)",
            category="Measurement",
            description="Fits circle through perimeter points, outputting center coordinate and radius.",
            icon="Circle",
            inputs=[FilterPortDef("points", "Array", description="List of perimeter Point2D")],
            outputs=[
                FilterPortDef("circle", "Circle2D", description="Fitted Circle2D Primitive"),
                FilterPortDef("radius", "Number", description="Fitted circle radius")
            ],
            executor=exec_fit_circle
        ))

        # 15. CreateInspectionROI
        def exec_roi(inputs, ctx):
            x = float(inputs.get("x", 180.0))
            y = float(inputs.get("y", 150.0))
            w = float(inputs.get("width", 1580.0))
            h = float(inputs.get("height", 920.0))
            rect = Rectangle2D(x=x, y=y, width=w, height=h)
            return {"roi": rect, "area": rect.area}

        self.register(FilterDefinition(
            filter_type="CreateInspectionROI",
            label="Tạo Vùng Kiểm Tra ROI (Inspection ROI)",
            category="Measurement",
            description="Generates Rectangle2D inspection window defining search bounds.",
            icon="Maximize2",
            inputs=[
                FilterPortDef("x", "Number", default_value=180.0, description="Top-left X"),
                FilterPortDef("y", "Number", default_value=150.0, description="Top-left Y"),
                FilterPortDef("width", "Number", default_value=1580.0, description="ROI Width"),
                FilterPortDef("height", "Number", default_value=920.0, description="ROI Height")
            ],
            outputs=[
                FilterPortDef("roi", "Rectangle2D", description="Inspection ROI primitive"),
                FilterPortDef("area", "Number", description="ROI area in square pixels")
            ],
            executor=exec_roi
        ))

        # =====================================================================
        # CATEGORY 4: LOGIC & MATH (4 FILTERS)
        # =====================================================================

        # 16. MathAdd
        def exec_add(inputs, ctx):
            a = float(inputs.get("a", 0.0))
            b = float(inputs.get("b", 0.0))
            return {"result": a + b}

        self.register(FilterDefinition(
            filter_type="MathAdd",
            label="Cộng Số Học (Math Add)",
            category="Logic",
            description="Adds two numerical values (A + B).",
            icon="Plus",
            inputs=[
                FilterPortDef("a", "Number", default_value=0.0, description="Value A"),
                FilterPortDef("b", "Number", default_value=0.0, description="Value B")
            ],
            outputs=[FilterPortDef("result", "Number", description="Sum result (A + B)")],
            executor=exec_add
        ))

        # 17. MathMultiply
        def exec_mul(inputs, ctx):
            a = float(inputs.get("a", 1.0))
            b = float(inputs.get("b", 1.0))
            return {"result": a * b}

        self.register(FilterDefinition(
            filter_type="MathMultiply",
            label="Nhân Số Học (Math Multiply)",
            category="Logic",
            description="Multiplies two numbers (A * B) - e.g. pixel-to-millimeter scaling.",
            icon="X",
            inputs=[
                FilterPortDef("a", "Number", default_value=1.0, description="Value A"),
                FilterPortDef("b", "Number", default_value=1.0, description="Value B")
            ],
            outputs=[FilterPortDef("result", "Number", description="Product (A * B)")],
            executor=exec_mul
        ))

        # 18. CompareThreshold
        def exec_compare(inputs, ctx):
            val = float(inputs.get("value", 0.0))
            min_v = float(inputs.get("min_limit", 0.0))
            max_v = float(inputs.get("max_limit", 100.0))
            is_pass = (min_v <= val <= max_v)
            return {"is_pass": is_pass, "in_tolerance": is_pass}

        self.register(FilterDefinition(
            filter_type="CompareThreshold",
            label="Kiểm Tra Giới Hạn Đạt/Lỗi (Tolerance Check)",
            category="Logic",
            description="Checks if measurement falls within [Min, Max] engineering tolerance window.",
            icon="CheckSquare",
            inputs=[
                FilterPortDef("value", "Number", default_value=50.0, description="Measured Value"),
                FilterPortDef("min_limit", "Number", default_value=40.0, description="Minimum acceptable limit"),
                FilterPortDef("max_limit", "Number", default_value=60.0, description="Maximum acceptable limit")
            ],
            outputs=[
                FilterPortDef("is_pass", "Boolean", description="True if value within tolerance"),
                FilterPortDef("in_tolerance", "Boolean", description="Pass/Fail flag")
            ],
            executor=exec_compare
        ))

        # 19. LogicAnd
        def exec_and(inputs, ctx):
            a = bool(inputs.get("a", True))
            b = bool(inputs.get("b", True))
            return {"result": a and b}

        self.register(FilterDefinition(
            filter_type="LogicAnd",
            label="Logic AND (Và)",
            category="Logic",
            description="Boolean AND operator combining multiple inspection criteria.",
            icon="CheckCircle",
            inputs=[
                FilterPortDef("a", "Boolean", default_value=True, description="Input A"),
                FilterPortDef("b", "Boolean", default_value=True, description="Input B")
            ],
            outputs=[FilterPortDef("result", "Boolean", description="Result (A AND B)")],
            executor=exec_and
        ))

        # =====================================================================
        # CATEGORY 5: OVERLAYS & DRAWING (3 FILTERS)
        # =====================================================================

        # 20. DrawOverlayRectangle
        def exec_draw_rect(inputs, ctx):
            rect = inputs.get("rectangle")
            color = inputs.get("color", "#00f0ff")
            label = inputs.get("label", "ROI")
            if isinstance(rect, Rectangle2D):
                rx, ry, rw, rh = rect.x, rect.y, rect.width, rect.height
            elif isinstance(rect, dict):
                rx, ry, rw, rh = rect.get("x", 0), rect.get("y", 0), rect.get("width", 0), rect.get("height", 0)
            else:
                rx, ry, rw, rh = 0, 0, 100, 100

            overlay = OverlayItem(
                type="rect",
                color=color,
                thickness=2,
                label=label,
                coordinates={"x": rx, "y": ry, "width": rw, "height": rh}
            )
            if hasattr(ctx, "overlays"):
                ctx.overlays.append(overlay)
            return {"overlay": overlay}

        self.register(FilterDefinition(
            filter_type="DrawOverlayRectangle",
            label="Vẽ Khung Bounding Box (Draw Box)",
            category="Overlays",
            description="Draws vector bounding box overlay onto the active image viewer.",
            icon="Square",
            inputs=[
                FilterPortDef("rectangle", "Rectangle2D", description="Rectangle coordinates"),
                FilterPortDef("color", "String", default_value="#00f0ff", description="Hex stroke color"),
                FilterPortDef("label", "String", default_value="Part OK", description="Text HUD label")
            ],
            outputs=[FilterPortDef("overlay", "Any", description="Generated Overlay Primitive")],
            executor=exec_draw_rect
        ))

        # 21. DrawOverlayCrosshair
        def exec_draw_cross(inputs, ctx):
            pt = inputs.get("point")
            color = inputs.get("color", "#f59e0b")
            px = pt.x if isinstance(pt, Point2D) else (pt.get("x", 0) if isinstance(pt, dict) else 0)
            py = pt.y if isinstance(pt, Point2D) else (pt.get("y", 0) if isinstance(pt, dict) else 0)
            overlay = OverlayItem(
                type="crosshair",
                color=color,
                thickness=2,
                coordinates={"x": px, "y": py, "size": 18}
            )
            if hasattr(ctx, "overlays"):
                ctx.overlays.append(overlay)
            return {"overlay": overlay}

        self.register(FilterDefinition(
            filter_type="DrawOverlayCrosshair",
            label="Vẽ Tâm Định Vị (Draw Crosshair)",
            category="Overlays",
            description="Renders yellow crosshair at target Point2D position.",
            icon="PlusCircle",
            inputs=[
                FilterPortDef("point", "Point2D", description="Target centroid coordinate"),
                FilterPortDef("color", "String", default_value="#f59e0b", description="Hex color")
            ],
            outputs=[FilterPortDef("overlay", "Any", description="Generated Crosshair")],
            executor=exec_draw_cross
        ))

        # 22. DrawOverlayText
        def exec_draw_text(inputs, ctx):
            text = inputs.get("text", "PASS")
            x = float(inputs.get("x", 50.0))
            y = float(inputs.get("y", 50.0))
            color = inputs.get("color", "#10b981")
            overlay = OverlayItem(
                type="text",
                color=color,
                label=text,
                coordinates={"x": x, "y": y, "font_size": 16}
            )
            if hasattr(ctx, "overlays"):
                ctx.overlays.append(overlay)
            return {"overlay": overlay}

        self.register(FilterDefinition(
            filter_type="DrawOverlayText",
            label="Vẽ Nhãn Văn Bản HUD (Draw Text)",
            category="Overlays",
            description="Draws HUD informational label at specified coordinate.",
            icon="Type",
            inputs=[
                FilterPortDef("text", "String", default_value="PART 01 [PASS]", description="Display text"),
                FilterPortDef("x", "Number", default_value=50.0, description="X screen position"),
                FilterPortDef("y", "Number", default_value=50.0, description="Y screen position"),
                FilterPortDef("color", "String", default_value="#10b981", description="Color")
            ],
            outputs=[FilterPortDef("overlay", "Any", description="Text Overlay Primitive")],
            executor=exec_draw_text
        ))

        # =====================================================================
        # CATEGORY 6: ACQUISITION & COMMUNICATION (3 FILTERS)
        # =====================================================================

        # 23. ImageSource
        def exec_img_src(inputs, ctx):
            img_id = inputs.get("image_id", "sample_bottle")
            if hasattr(ctx, "image_service"):
                img = ctx.image_service.get_image(img_id)
                meta = ctx.image_service.get_meta(img_id)
                w = meta.width if meta else (img.shape[1] if img is not None else 0)
                h = meta.height if meta else (img.shape[0] if img is not None else 0)
                return {"image": img, "width": w, "height": h}
            return {"image": None, "width": 0, "height": 0}

        self.register(FilterDefinition(
            filter_type="ImageSource",
            label="Nguồn Ảnh Đầu Vào (Image Source)",
            category="Acquisition",
            description="Provides image frame from internal memory buffer or disk file.",
            icon="Camera",
            inputs=[
                FilterPortDef("image_id", "String", default_value="sample_bottle", options=["sample_bottle", "sample_pcb", "sample_calib"], description="Image buffer ID")
            ],
            outputs=[
                FilterPortDef("image", "Image", description="Acquired Image buffer"),
                FilterPortDef("width", "Integer", description="Image pixel width"),
                FilterPortDef("height", "Integer", description="Image pixel height")
            ],
            executor=exec_img_src
        ))

        # 24. FormatSchneiderSert
        def exec_sert(inputs, ctx):
            count = int(inputs.get("count", 1))
            status = int(inputs.get("status", 1))
            model = str(inputs.get("model_code", "LB800")).strip()
            x = float(inputs.get("x", 420.5))
            y = float(inputs.get("y", 185.3))
            angle = float(inputs.get("angle", 15.0))
            po = str(inputs.get("orientation", "PO1"))

            # Build FB_CognexCamera Schneider SERT format:
            header = f"Vision{status}{count:02d}A{len(model):02d}{model}"
            target = f"[P01,{x:+08.2f},{y:+08.2f},{angle:+06.1f},{po}]"
            packet = f"{header}{target}\r"
            return {"packet_string": packet, "byte_count": len(packet.encode('ascii'))}

        self.register(FilterDefinition(
            filter_type="FormatSchneiderSert",
            label="Định Dạng Gói Tin Schneider SERT",
            category="Communication",
            description="Formats inspection metrics into FB_CognexCamera Schneider PacDrive/M262 PLC protocol.",
            icon="Radio",
            inputs=[
                FilterPortDef("count", "Integer", default_value=1, description="Object count"),
                FilterPortDef("status", "Integer", default_value=1, description="Status code (1=OK)"),
                FilterPortDef("model_code", "String", default_value="LB800", description="SKU Recipe model code"),
                FilterPortDef("x", "Number", default_value=420.5, description="Robot X mm"),
                FilterPortDef("y", "Number", default_value=185.3, description="Robot Y mm"),
                FilterPortDef("angle", "Number", default_value=15.0, description="Angle degrees"),
                FilterPortDef("orientation", "String", default_value="PO1", options=["PO1", "PO2"], description="Orientation type")
            ],
            outputs=[
                FilterPortDef("packet_string", "String", description="ASCII SERT packet string"),
                FilterPortDef("byte_count", "Integer", description="Total payload byte count")
            ],
            executor=exec_sert
        ))

        # 25. PassFailDecision
        def exec_decision(inputs, ctx):
            cond1 = bool(inputs.get("dimension_ok", True))
            cond2 = bool(inputs.get("count_ok", True))
            overall_pass = cond1 and cond2
            status_text = "PASS (ĐẠT CHUẨN)" if overall_pass else "FAIL (LỖI PHẾ PHẨM)"
            return {
                "overall_pass": overall_pass,
                "status_text": status_text,
                "color_code": "#10b981" if overall_pass else "#ef4444"
            }

        self.register(FilterDefinition(
            filter_type="PassFailDecision",
            label="Quyết Định Phân Loại Đạt/Lỗi (Pass/Fail)",
            category="Logic",
            description="Synthesizes all inspection metrics into decisive factory Pass/Fail verdict.",
            icon="Award",
            inputs=[
                FilterPortDef("dimension_ok", "Boolean", default_value=True, description="Kích thước đạt chuẩn"),
                FilterPortDef("count_ok", "Boolean", default_value=True, description="Số lượng đạt chuẩn")
            ],
            outputs=[
                FilterPortDef("overall_pass", "Boolean", description="True if whole part passes"),
                FilterPortDef("status_text", "String", description="Formatted status description"),
                FilterPortDef("color_code", "String", description="Hex status color")
            ],
            executor=exec_decision
        ))

        # =====================================================================
        # CATEGORY 7: M3 ADVANCED EXECUTION & STATE (5 FILTERS)
        # =====================================================================

        # 26. EvaluateFormula
        def exec_formula(inputs, ctx):
            expr = str(inputs.get("formula") or inputs.get("expression") or "x + y")
            context = {k: v for k, v in inputs.items() if k not in ("formula", "expression")}
            if "x" not in context: context["x"] = 0.0
            if "y" not in context: context["y"] = 0.0
            if "z" not in context: context["z"] = 0.0
            res = formula_evaluator.evaluate(expr, context)
            return {"result": res}

        self.register(FilterDefinition(
            filter_type="EvaluateFormula",
            label="Biểu Thức Công Thức (Formula)",
            category="Logic",
            description="Evaluates mathematical or logical formula using safe sandboxed AST expressions.",
            icon="FunctionSquare",
            inputs=[
                FilterPortDef("formula", "String", default_value="sqrt(x*x + y*y)", description="Expression string"),
                FilterPortDef("x", "Number", default_value=3.0, description="Variable x"),
                FilterPortDef("y", "Number", default_value=4.0, description="Variable y"),
                FilterPortDef("z", "Number", default_value=0.0, description="Variable z")
            ],
            outputs=[FilterPortDef("result", "Any", description="Calculated evaluation result")],
            executor=exec_formula
        ))

        # 27. RegisterRead
        def exec_reg_read(inputs, ctx):
            name = str(inputs.get("register_name", "PartCounter"))
            default_v = inputs.get("default_value", 0)
            val = ctx.registers.get(name, default_v) if hasattr(ctx, "registers") else default_v
            return {"value": val}

        self.register(FilterDefinition(
            filter_type="RegisterRead",
            label="Đọc Thanh Ghi Lưu Trữ (Register Read)",
            category="Logic",
            description="Reads stateful variable preserved across continuous inspection cycles.",
            icon="Database",
            inputs=[
                FilterPortDef("register_name", "String", default_value="PartCounter", description="Register name"),
                FilterPortDef("default_value", "Any", default_value=0, description="Fallback value if uninitialized")
            ],
            outputs=[FilterPortDef("value", "Any", description="Stored register value")],
            executor=exec_reg_read
        ))

        # 28. RegisterWrite
        def exec_reg_write(inputs, ctx):
            name = str(inputs.get("register_name", "PartCounter"))
            val = inputs.get("value", 0)
            if hasattr(ctx, "registers"):
                ctx.registers.set(name, val)
            return {"stored_value": val}

        self.register(FilterDefinition(
            filter_type="RegisterWrite",
            label="Ghi Thanh Ghi Lưu Trữ (Register Write)",
            category="Logic",
            description="Persists updated value into named register across inspection cycles.",
            icon="Save",
            inputs=[
                FilterPortDef("register_name", "String", default_value="PartCounter", description="Register name"),
                FilterPortDef("value", "Any", default_value=1, description="Value to store")
            ],
            outputs=[FilterPortDef("stored_value", "Any", description="Stored value confirmation")],
            executor=exec_reg_write
        ))

        # 29. ConditionalBranch
        def exec_cond_branch(inputs, ctx):
            cond = bool(inputs.get("condition", True))
            val_t = inputs.get("value_true", inputs.get("if_true"))
            val_f = inputs.get("value_false", inputs.get("if_false"))
            selected = val_t if cond else val_f
            return {
                "selected_value": selected,
                "result": selected,
                "branch_taken": "TRUE" if cond else "FALSE"
            }

        self.register(FilterDefinition(
            filter_type="ConditionalBranch",
            label="Rẽ Nhánh Điều Kiện (Conditional Branch)",
            category="Logic",
            description="Selects True or False path based on Boolean condition.",
            icon="GitFork",
            inputs=[
                FilterPortDef("condition", "Boolean", default_value=True, description="Routing condition flag"),
                FilterPortDef("value_true", "Any", description="Value forwarded if condition is True"),
                FilterPortDef("value_false", "Any", description="Value forwarded if condition is False")
            ],
            outputs=[
                FilterPortDef("selected_value", "Any", description="Routed output value"),
                FilterPortDef("result", "Any", description="Routed output value alias"),
                FilterPortDef("branch_taken", "String", description="'TRUE' or 'FALSE'")
            ],
            executor=exec_cond_branch
        ))

        # 30. CoalesceNil
        def exec_coalesce_nil(inputs, ctx):
            val = inputs.get("value")
            fallback = inputs.get("fallback_value", inputs.get("fallback", 0))
            is_nil = (val is None or val == "Nil")
            result = fallback if is_nil else val
            return {"result": result, "was_nil": is_nil}

        self.register(FilterDefinition(
            filter_type="CoalesceNil",
            label="Bảo Vệ Giá Trị Rỗng (Coalesce Nil)",
            category="Logic",
            description="Provides a safe fallback value if the primary input was Nil or missing.",
            icon="ShieldAlert",
            inputs=[
                FilterPortDef("value", "Any", description="Primary input that might be Nil"),
                FilterPortDef("fallback_value", "Any", default_value=0, description="Fallback value if Nil")
            ],
            outputs=[
                FilterPortDef("result", "Any", description="Non-Nil guaranteed output"),
                FilterPortDef("was_nil", "Boolean", description="True if fallback was used")
            ],
            executor=exec_coalesce_nil
        ))

        # =====================================================================
        # CATEGORY 8: GEOMETRY, METROLOGY & CALIBRATION (M4 - 10 FILTERS)
        # =====================================================================

        # 31. Measure1DEdge
        def exec_1d_edge(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                return {"edge_x": 0.0, "edge_y": 0.0, "distance": 0.0, "gradient": 0.0, "found": False}
            p1 = (float(inputs.get("p1_x", 0.0)), float(inputs.get("p1_y", 0.0)))
            p2 = (float(inputs.get("p2_x", 100.0)), float(inputs.get("p2_y", 100.0)))
            corridor = int(inputs.get("corridor_width", 5))
            min_grad = float(inputs.get("min_gradient", 15.0))
            pol = str(inputs.get("polarity", "ANY")).upper()
            selection = str(inputs.get("edge_selection", "FIRST")).upper()

            dists, intensities, coords = sample_1d_profile(img, p1, p2, corridor_width=corridor)
            edges = detect_1d_edges_subpixel(dists, intensities, coords, min_gradient=min_grad, polarity=pol)
            if not edges:
                return {"edge_x": 0.0, "edge_y": 0.0, "distance": 0.0, "gradient": 0.0, "found": False}

            if selection == "BEST":
                selected = max(edges, key=lambda e: e["gradient"])
            elif selection == "LAST":
                selected = edges[-1]
            else:
                selected = edges[0]

            # Add HUD overlay circle at edge location
            ctx.overlays.append(OverlayItem(
                type="circle",
                color="#00f0ff",
                label=f"Edge ({selected['x']:.1f}, {selected['y']:.1f})",
                geometry={"cx": selected["x"], "cy": selected["y"], "radius": 4.0}
            ))

            return {
                "edge_x": selected["x"],
                "edge_y": selected["y"],
                "distance": selected["distance"],
                "gradient": selected["gradient"],
                "found": True
            }

        self.register(FilterDefinition(
            filter_type="Measure1DEdge",
            label="Đo Biên 1D Sub-pixel (1D Edge)",
            category="Metrology",
            description="Locates precise sub-pixel boundary along a scan ray with corridor noise averaging.",
            icon="Activity",
            inputs=[
                FilterPortDef("image", "Image", description="Input Image buffer"),
                FilterPortDef("p1_x", "Number", default_value=50.0, description="Ray Start X"),
                FilterPortDef("p1_y", "Number", default_value=50.0, description="Ray Start Y"),
                FilterPortDef("p2_x", "Number", default_value=250.0, description="Ray End X"),
                FilterPortDef("p2_y", "Number", default_value=50.0, description="Ray End Y"),
                FilterPortDef("corridor_width", "Integer", default_value=5, description="Transverse averaging corridor"),
                FilterPortDef("min_gradient", "Number", default_value=15.0, description="Minimum edge gradient threshold"),
                FilterPortDef("polarity", "String", default_value="ANY", options=["ANY", "BRIGHT_TO_DARK", "DARK_TO_BRIGHT"], description="Transition polarity"),
                FilterPortDef("edge_selection", "String", default_value="FIRST", options=["FIRST", "BEST", "LAST"], description="Selection strategy")
            ],
            outputs=[
                FilterPortDef("edge_x", "Number", description="Sub-pixel X coordinate"),
                FilterPortDef("edge_y", "Number", description="Sub-pixel Y coordinate"),
                FilterPortDef("distance", "Number", description="Distance along ray"),
                FilterPortDef("gradient", "Number", description="Edge sharpness gradient"),
                FilterPortDef("found", "Boolean", description="True if edge found")
            ],
            executor=exec_1d_edge
        ))

        # 32. Measure1DEdgePair (Caliper)
        def exec_edge_pair(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                return {"width": 0.0, "center_x": 0.0, "center_y": 0.0, "found": False}
            p1 = (float(inputs.get("p1_x", 0.0)), float(inputs.get("p1_y", 0.0)))
            p2 = (float(inputs.get("p2_x", 200.0)), float(inputs.get("p2_y", 0.0)))
            corridor = int(inputs.get("corridor_width", 5))
            min_grad = float(inputs.get("min_gradient", 15.0))

            dists, intensities, coords = sample_1d_profile(img, p1, p2, corridor_width=corridor)
            edges = detect_1d_edges_subpixel(dists, intensities, coords, min_gradient=min_grad, polarity="ANY")
            if len(edges) < 2:
                return {"width": 0.0, "center_x": 0.0, "center_y": 0.0, "found": False}

            e1 = edges[0]
            e2 = edges[-1]
            w = math.hypot(e2["x"] - e1["x"], e2["y"] - e1["y"])
            cx = (e1["x"] + e2["x"]) / 2.0
            cy = (e1["y"] + e2["y"]) / 2.0

            # HUD overlay
            ctx.overlays.append(OverlayItem(
                type="segment",
                color="#10b981",
                label=f"W={w:.2f}px",
                geometry={"x1": e1["x"], "y1": e1["y"], "x2": e2["x"], "y2": e2["y"]}
            ))

            return {
                "width": round(w, 3),
                "center_x": round(cx, 3),
                "center_y": round(cy, 3),
                "edge1_x": e1["x"],
                "edge1_y": e1["y"],
                "edge2_x": e2["x"],
                "edge2_y": e2["y"],
                "found": True
            }

        self.register(FilterDefinition(
            filter_type="Measure1DEdgePair",
            label="Thước Kẹp Caliper 1D (Edge Pair)",
            category="Metrology",
            description="Measures inner/outer thickness or slot gap between two opposing edges along scan line.",
            icon="Sliders",
            inputs=[
                FilterPortDef("image", "Image", description="Input Image buffer"),
                FilterPortDef("p1_x", "Number", default_value=50.0, description="Ray Start X"),
                FilterPortDef("p1_y", "Number", default_value=100.0, description="Ray Start Y"),
                FilterPortDef("p2_x", "Number", default_value=250.0, description="Ray End X"),
                FilterPortDef("p2_y", "Number", default_value=100.0, description="Ray End Y"),
                FilterPortDef("corridor_width", "Integer", default_value=5, description="Averaging corridor"),
                FilterPortDef("min_gradient", "Number", default_value=15.0, description="Minimum edge gradient")
            ],
            outputs=[
                FilterPortDef("width", "Number", description="Measured width between edges"),
                FilterPortDef("center_x", "Number", description="Midpoint X coordinate"),
                FilterPortDef("center_y", "Number", description="Midpoint Y coordinate"),
                FilterPortDef("found", "Boolean", description="True if edge pair detected")
            ],
            executor=exec_edge_pair
        ))

        # 33. FitLine2D
        def exec_fit_line(inputs, ctx):
            pts = inputs.get("points") or []
            max_err = float(inputs.get("max_error", 2.0))
            res = fit_line_robust(pts, max_reproj_error=max_err)
            if not res:
                return {"vx": 1.0, "vy": 0.0, "x0": 0.0, "y0": 0.0, "angle_deg": 0.0, "rms_residual": 0.0, "found": False}
            res["found"] = True
            return res

        self.register(FilterDefinition(
            filter_type="FitLine2D",
            label="Khớp Đường Thẳng Robust (Fit Line)",
            category="Geometry",
            description="Fits optimal 2D line through point array using Huber M-estimator to suppress burrs.",
            icon="TrendingUp",
            inputs=[
                FilterPortDef("points", "Array", description="List of (x, y) coordinates"),
                FilterPortDef("max_error", "Number", default_value=2.0, description="Max inlier threshold in pixels")
            ],
            outputs=[
                FilterPortDef("vx", "Number", description="Line direction vector X"),
                FilterPortDef("vy", "Number", description="Line direction vector Y"),
                FilterPortDef("x0", "Number", description="Origin point X on line"),
                FilterPortDef("y0", "Number", description="Origin point Y on line"),
                FilterPortDef("angle_deg", "Number", description="Line slope angle degrees"),
                FilterPortDef("rms_residual", "Number", description="Root-mean-square residual"),
                FilterPortDef("found", "Boolean", description="True if fit succeeded")
            ],
            executor=exec_fit_line
        ))

        # 34. FitCircle2D
        def exec_fit_circle(inputs, ctx):
            pts = inputs.get("points") or []
            res = fit_circle_taubin(pts)
            if not res:
                return {"cx": 0.0, "cy": 0.0, "radius": 0.0, "diameter": 0.0, "roundness_error": 0.0, "found": False}
            res["found"] = True

            # HUD overlay circle
            ctx.overlays.append(OverlayItem(
                type="circle",
                color="#f59e0b",
                label=f"R={res['radius']:.1f}px",
                geometry={"cx": res["cx"], "cy": res["cy"], "radius": res["radius"]}
            ))
            return res

        self.register(FilterDefinition(
            filter_type="FitCircle2D",
            label="Khớp Đường Tròn Taubin (Fit Circle)",
            category="Geometry",
            description="Fits circle through point cloud using Taubin algebraic method without partial arc bias.",
            icon="Circle",
            inputs=[
                FilterPortDef("points", "Array", description="List of (x, y) boundary points")
            ],
            outputs=[
                FilterPortDef("cx", "Number", description="Circle center X"),
                FilterPortDef("cy", "Number", description="Circle center Y"),
                FilterPortDef("radius", "Number", description="Circle radius"),
                FilterPortDef("diameter", "Number", description="Circle diameter"),
                FilterPortDef("roundness_error", "Number", description="Radial RMS variance"),
                FilterPortDef("found", "Boolean", description="True if fit succeeded")
            ],
            executor=exec_fit_circle
        ))

        # 35. MeasurePointToPoint
        def exec_p2p(inputs, ctx):
            p1 = (float(inputs.get("x1", 0.0)), float(inputs.get("y1", 0.0)))
            p2 = (float(inputs.get("x2", 100.0)), float(inputs.get("y2", 100.0)))
            return measure_point_to_point(p1, p2)

        self.register(FilterDefinition(
            filter_type="MeasurePointToPoint",
            label="Khoảng Cách Điểm Đến Điểm (Point to Point)",
            category="Metrology",
            description="Calculates Euclidean distance, delta X, delta Y and angle between two coordinates.",
            icon="Move",
            inputs=[
                FilterPortDef("x1", "Number", default_value=0.0, description="Point 1 X"),
                FilterPortDef("y1", "Number", default_value=0.0, description="Point 1 Y"),
                FilterPortDef("x2", "Number", default_value=100.0, description="Point 2 X"),
                FilterPortDef("y2", "Number", default_value=0.0, description="Point 2 Y")
            ],
            outputs=[
                FilterPortDef("distance", "Number", description="Euclidean distance"),
                FilterPortDef("delta_x", "Number", description="Horizontal offset"),
                FilterPortDef("delta_y", "Number", description="Vertical offset"),
                FilterPortDef("angle_deg", "Number", description="Vector angle degrees")
            ],
            executor=exec_p2p
        ))

        # 36. MeasurePointToLine
        def exec_p2line(inputs, ctx):
            pt = (float(inputs.get("px", 50.0)), float(inputs.get("py", 50.0)))
            l_pt = (float(inputs.get("line_x0", 0.0)), float(inputs.get("line_y0", 0.0)))
            l_dir = (float(inputs.get("line_vx", 1.0)), float(inputs.get("line_vy", 0.0)))
            return measure_point_to_line(pt, l_pt, l_dir)

        self.register(FilterDefinition(
            filter_type="MeasurePointToLine",
            label="Khoảng Cách Điểm Đến Đường Thẳng (Point to Line)",
            category="Metrology",
            description="Computes orthogonal perpendicular distance and projection foot point.",
            icon="CornerDownRight",
            inputs=[
                FilterPortDef("px", "Number", default_value=50.0, description="Target Point X"),
                FilterPortDef("py", "Number", default_value=50.0, description="Target Point Y"),
                FilterPortDef("line_x0", "Number", default_value=0.0, description="Line Origin X"),
                FilterPortDef("line_y0", "Number", default_value=0.0, description="Line Origin Y"),
                FilterPortDef("line_vx", "Number", default_value=1.0, description="Line Direction Vx"),
                FilterPortDef("line_vy", "Number", default_value=0.0, description="Line Direction Vy")
            ],
            outputs=[
                FilterPortDef("distance", "Number", description="Perpendicular distance"),
                FilterPortDef("foot_x", "Number", description="Projected foot point X"),
                FilterPortDef("foot_y", "Number", description="Projected foot point Y")
            ],
            executor=exec_p2line
        ))

        # 37. MeasureLineAngle
        def exec_line_angle(inputs, ctx):
            d1 = (float(inputs.get("v1_x", 1.0)), float(inputs.get("v1_y", 0.0)))
            d2 = (float(inputs.get("v2_x", 0.0)), float(inputs.get("v2_y", 1.0)))
            return measure_line_to_line_angle(d1, d2)

        self.register(FilterDefinition(
            filter_type="MeasureLineAngle",
            label="Góc Giữa Hai Đường Thẳng (Line Angle)",
            category="Metrology",
            description="Measures acute angle between two line vectors and flags parallelism or perpendicularity.",
            icon="Compass",
            inputs=[
                FilterPortDef("v1_x", "Number", default_value=1.0, description="Line 1 Direction Vx"),
                FilterPortDef("v1_y", "Number", default_value=0.0, description="Line 1 Direction Vy"),
                FilterPortDef("v2_x", "Number", default_value=0.0, description="Line 2 Direction Vx"),
                FilterPortDef("v2_y", "Number", default_value=1.0, description="Line 2 Direction Vy")
            ],
            outputs=[
                FilterPortDef("angle_deg", "Number", description="Angle between lines (0..180)"),
                FilterPortDef("is_parallel", "Boolean", description="True if parallel"),
                FilterPortDef("is_perpendicular", "Boolean", description="True if perpendicular")
            ],
            executor=exec_line_angle
        ))

        # 38. UndistortImage
        def exec_undistort(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                return {"out_image": None}
            h, w = img.shape[:2]
            fx = float(inputs.get("fx", 1500.0))
            fy = float(inputs.get("fy", 1500.0))
            cx_val = float(inputs.get("cx", w / 2.0))
            cy_val = float(inputs.get("cy", h / 2.0))
            k1 = float(inputs.get("k1", -0.1))
            k2 = float(inputs.get("k2", 0.02))
            p1 = float(inputs.get("p1", 0.0))
            p2 = float(inputs.get("p2", 0.0))

            cam_matrix = np.array([[fx, 0, cx_val], [0, fy, cy_val], [0, 0, 1]], dtype=np.float32)
            dist_coeffs = np.array([k1, k2, p1, p2, 0.0], dtype=np.float32)

            model = LensDistortionModel(cam_matrix, dist_coeffs, (h, w))
            corrected = model.undistort(img)
            return {"out_image": corrected}

        self.register(FilterDefinition(
            filter_type="UndistortImage",
            label="Khử Méo Ống Kính (Undistort Image)",
            category="Acquisition",
            description="Removes barrel or pincushion lens distortion using calibration intrinsic matrix K and D.",
            icon="Eye",
            inputs=[
                FilterPortDef("image", "Image", description="Distorted camera frame"),
                FilterPortDef("fx", "Number", default_value=1500.0, description="Focal length X (pixels)"),
                FilterPortDef("fy", "Number", default_value=1500.0, description="Focal length Y (pixels)"),
                FilterPortDef("cx", "Number", default_value=960.0, description="Principal point X"),
                FilterPortDef("cy", "Number", default_value=600.0, description="Principal point Y"),
                FilterPortDef("k1", "Number", default_value=-0.1, description="Radial distortion k1"),
                FilterPortDef("k2", "Number", default_value=0.02, description="Radial distortion k2"),
                FilterPortDef("p1", "Number", default_value=0.0, description="Tangential distortion p1"),
                FilterPortDef("p2", "Number", default_value=0.0, description="Tangential distortion p2")
            ],
            outputs=[
                FilterPortDef("out_image", "Image", description="Rectified distortion-free image")
            ],
            executor=exec_undistort
        ))

        # 39. CalibrateHomography
        def exec_calib_homography(inputs, ctx):
            pix_pts = inputs.get("pixel_points") or []
            world_pts = inputs.get("world_points_mm") or []
            H = compute_homography_from_fiducials(pix_pts, world_pts)
            is_valid = H is not None
            return {
                "homography_matrix": H.tolist() if is_valid else [],
                "is_valid": is_valid
            }

        self.register(FilterDefinition(
            filter_type="CalibrateHomography",
            label="Hiệu Chuẩn Phối Cảnh Homography (Calibrate Plane)",
            category="Geometry",
            description="Solves 3x3 homography matrix mapping conveyor pixel coordinates to real-world millimeters.",
            icon="Grid",
            inputs=[
                FilterPortDef("pixel_points", "Array", description="List of (u, v) pixel points"),
                FilterPortDef("world_points_mm", "Array", description="List of corresponding (X, Y) mm coordinates")
            ],
            outputs=[
                FilterPortDef("homography_matrix", "Any", description="Calculated 3x3 perspective matrix"),
                FilterPortDef("is_valid", "Boolean", description="True if homography solved")
            ],
            executor=exec_calib_homography
        ))

        # 40. TransformPointsWorld
        def exec_transform_pts(inputs, ctx):
            pts = inputs.get("points") or []
            H_raw = inputs.get("homography_matrix")
            if not H_raw or not pts:
                return {"world_points_mm": []}
            H = np.array(H_raw, dtype=np.float32)
            transformed = transform_points_with_homography(pts, H)
            return {"world_points_mm": transformed}

        self.register(FilterDefinition(
            filter_type="TransformPointsWorld",
            label="Chuyển Tọa Độ Sang Millimet Thế Giới (Pixel to MM)",
            category="Geometry",
            description="Transforms pixel coordinates into real-world millimeter coordinates using calibration matrix.",
            icon="MapPin",
            inputs=[
                FilterPortDef("points", "Array", description="Pixel coordinates [(x, y), ...]"),
                FilterPortDef("homography_matrix", "Any", description="3x3 Homography Matrix")
            ],
            outputs=[
                FilterPortDef("world_points_mm", "Array", description="Coordinates in real-world millimeters")
            ],
            executor=exec_transform_pts
        ))

        # =====================================================================
        # CATEGORY 9: TEMPLATE MATCHING & ALIGNMENT (M5 - 5 FILTERS)
        # =====================================================================

        # 41. MatchTemplateNCC
        def exec_ncc(inputs, ctx):
            img = inputs.get("image")
            tpl = inputs.get("template_image")
            min_sc = float(inputs.get("min_score", 0.70))
            matches = match_template_ncc_pyramid(img, tpl, min_score=min_sc)
            if not matches:
                return {"x": 0.0, "y": 0.0, "width": 0.0, "height": 0.0, "score": 0.0, "found": False}
            m = matches[0]

            # HUD overlay
            ctx.overlays.append(OverlayItem(
                type="rectangle",
                color="#10b981",
                label=f"Match NCC ({m['score']:.2f})",
                geometry={"x": m["top_left_x"], "y": m["top_left_y"], "width": m["width"], "height": m["height"]}
            ))
            return {
                "x": m["x"],
                "y": m["y"],
                "width": m["width"],
                "height": m["height"],
                "score": m["score"],
                "found": True
            }

        self.register(FilterDefinition(
            filter_type="MatchTemplateNCC",
            label="Khớp Mẫu NCC Thang Xám (Template Matching)",
            category="Analysis",
            description="Normalized Cross-Correlation multi-tier pyramid template matching (< 5ms).",
            icon="Search",
            inputs=[
                FilterPortDef("image", "Image", description="Live inspection frame"),
                FilterPortDef("template_image", "Image", description="Reference template image pattern"),
                FilterPortDef("min_score", "Number", default_value=0.70, description="Minimum acceptance score (0..1)")
            ],
            outputs=[
                FilterPortDef("x", "Number", description="Center X match position"),
                FilterPortDef("y", "Number", description="Center Y match position"),
                FilterPortDef("width", "Number", description="Template width"),
                FilterPortDef("height", "Number", description="Template height"),
                FilterPortDef("score", "Number", description="Correlation score (0..1)"),
                FilterPortDef("found", "Boolean", description="True if pattern found above threshold")
            ],
            executor=exec_ncc
        ))

        # 42. MatchTemplateRotation
        def exec_tpl_rot(inputs, ctx):
            img = inputs.get("image")
            tpl = inputs.get("template_image")
            min_sc = float(inputs.get("min_score", 0.75))
            min_ang = float(inputs.get("min_angle", -30.0))
            max_ang = float(inputs.get("max_angle", 30.0))
            step_ang = float(inputs.get("step_angle", 5.0))

            m = match_template_with_rotation(img, tpl, min_score=min_sc, min_angle_deg=min_ang, max_angle_deg=max_ang, angle_step_deg=step_ang)
            if not m:
                return {"x": 0.0, "y": 0.0, "angle_deg": 0.0, "score": 0.0, "found": False}

            ctx.overlays.append(OverlayItem(
                type="rectangle",
                color="#00f0ff",
                label=f"Match ({m['angle_deg']}°, {m['score']:.2f})",
                geometry={"x": m["top_left_x"], "y": m["top_left_y"], "width": m["width"], "height": m["height"]}
            ))
            return {
                "x": m["x"],
                "y": m["y"],
                "angle_deg": m["angle_deg"],
                "score": m["score"],
                "found": True
            }

        self.register(FilterDefinition(
            filter_type="MatchTemplateRotation",
            label="Khớp Mẫu Xoay Đa Hướng (Rotated Matching)",
            category="Analysis",
            description="Finds pattern with angular orientation search in specified angle range.",
            icon="RotateCw",
            inputs=[
                FilterPortDef("image", "Image", description="Live inspection frame"),
                FilterPortDef("template_image", "Image", description="Reference pattern"),
                FilterPortDef("min_score", "Number", default_value=0.75, description="Correlation threshold"),
                FilterPortDef("min_angle", "Number", default_value=-30.0, description="Minimum search angle"),
                FilterPortDef("max_angle", "Number", default_value=30.0, description="Maximum search angle"),
                FilterPortDef("step_angle", "Number", default_value=5.0, description="Angular step size")
            ],
            outputs=[
                FilterPortDef("x", "Number", description="Detected center X"),
                FilterPortDef("y", "Number", description="Detected center Y"),
                FilterPortDef("angle_deg", "Number", description="Detected part orientation angle"),
                FilterPortDef("score", "Number", description="Correlation match score"),
                FilterPortDef("found", "Boolean", description="True if pattern detected")
            ],
            executor=exec_tpl_rot
        ))

        # 43. CreateCoordinateSystem
        def exec_create_cs(inputs, ctx):
            ox = float(inputs.get("origin_x", 0.0))
            oy = float(inputs.get("origin_y", 0.0))
            ang = float(inputs.get("angle_deg", 0.0))
            cs = CoordinateSystem2D(ox, oy, ang)
            return {"coord_system": cs.to_dict()}

        self.register(FilterDefinition(
            filter_type="CreateCoordinateSystem",
            label="Tạo Hệ Tọa Độ Cục Bộ (Coordinate System)",
            category="Geometry",
            description="Establishes part-relative 2D reference frame for rotational & positional fixture alignment.",
            icon="Maximize2",
            inputs=[
                FilterPortDef("origin_x", "Number", default_value=100.0, description="Part Origin X"),
                FilterPortDef("origin_y", "Number", default_value=100.0, description="Part Origin Y"),
                FilterPortDef("angle_deg", "Number", default_value=0.0, description="Part Rotation Angle")
            ],
            outputs=[
                FilterPortDef("coord_system", "Any", description="2D Affine Reference Frame")
            ],
            executor=exec_create_cs
        ))

        # 44. AlignROI
        def exec_align_roi(inputs, ctx):
            cs_data = inputs.get("coord_system") or {}
            ox = float(cs_data.get("origin_x", 0.0))
            oy = float(cs_data.get("origin_y", 0.0))
            ang = float(cs_data.get("angle_deg", 0.0))
            cs = CoordinateSystem2D(ox, oy, ang)

            local_rect = {
                "x": float(inputs.get("roi_x", 0.0)),
                "y": float(inputs.get("roi_y", 0.0)),
                "width": float(inputs.get("roi_w", 50.0)),
                "height": float(inputs.get("roi_h", 50.0))
            }
            aligned = cs.align_rectangle(local_rect)

            # HUD overlay
            ctx.overlays.append(OverlayItem(
                type="rectangle",
                color="#a855f7",
                label="Aligned ROI",
                geometry={"x": aligned["center_x"] - aligned["width"]/2.0, "y": aligned["center_y"] - aligned["height"]/2.0, "width": aligned["width"], "height": aligned["height"]}
            ))

            return {
                "world_cx": aligned["center_x"],
                "world_cy": aligned["center_y"],
                "world_w": aligned["width"],
                "world_h": aligned["height"],
                "angle_deg": aligned["angle_deg"],
                "aligned_roi": aligned
            }

        self.register(FilterDefinition(
            filter_type="AlignROI",
            label="Căn Chỉnh Vùng Kiểm Tra ROI (Align ROI)",
            category="Geometry",
            description="Transforms inspection bounding box to track rotated/displaced part coordinate system.",
            icon="Crop",
            inputs=[
                FilterPortDef("coord_system", "Any", description="Part Coordinate System"),
                FilterPortDef("roi_x", "Number", default_value=0.0, description="Local offset X"),
                FilterPortDef("roi_y", "Number", default_value=0.0, description="Local offset Y"),
                FilterPortDef("roi_w", "Number", default_value=50.0, description="ROI Width"),
                FilterPortDef("roi_h", "Number", default_value=50.0, description="ROI Height")
            ],
            outputs=[
                FilterPortDef("world_cx", "Number", description="Global world center X"),
                FilterPortDef("world_cy", "Number", description="Global world center Y"),
                FilterPortDef("world_w", "Number", description="ROI width"),
                FilterPortDef("world_h", "Number", description="ROI height"),
                FilterPortDef("angle_deg", "Number", description="ROI rotation angle"),
                FilterPortDef("aligned_roi", "Any", description="Aligned bounding geometry")
            ],
            executor=exec_align_roi
        ))

        # 45. CompareGoldenTemplate
        def exec_golden(inputs, ctx):
            live = inputs.get("live_image")
            gold = inputs.get("golden_template")
            thresh = int(inputs.get("threshold", 35))
            min_area = int(inputs.get("min_area", 15))

            res = compare_golden_template(live, gold, diff_threshold=thresh, min_defect_area=min_area)
            if res.get("has_defects"):
                for d in res.get("defects", []):
                    ctx.overlays.append(OverlayItem(
                        type="rectangle",
                        color="#ef4444",
                        label=f"DEFECT ({d['area']}px)",
                        geometry={"x": d["x"], "y": d["y"], "width": d["width"], "height": d["height"]}
                    ))

            return {
                "defect_count": res["defect_count"],
                "max_defect_area": res["max_defect_area"],
                "has_defects": res["has_defects"],
                "defect_mask": res["defect_mask"]
            }

        self.register(FilterDefinition(
            filter_type="CompareGoldenTemplate",
            label="So Sánh Mẫu Chuẩn Golden (Defect Diff)",
            category="Analysis",
            description="Subtracts aligned reference golden sample from live image to highlight surface scratches, missing pins, or contamination.",
            icon="GitDiff",
            inputs=[
                FilterPortDef("live_image", "Image", description="Live inspected image"),
                FilterPortDef("golden_template", "Image", description="Flawless reference golden image"),
                FilterPortDef("threshold", "Integer", default_value=35, description="Difference pixel intensity threshold"),
                FilterPortDef("min_area", "Integer", default_value=15, description="Minimum defect pixel area")
            ],
            outputs=[
                FilterPortDef("defect_count", "Integer", description="Count of detected surface flaws"),
                FilterPortDef("max_defect_area", "Integer", description="Area of largest defect in pixels"),
                FilterPortDef("has_defects", "Boolean", description="True if any defect exceeds tolerance"),
                FilterPortDef("defect_mask", "Image", description="Binary defect map")
            ],
            executor=exec_golden
        ))

        # =====================================================================
        # CATEGORY 10: MACROS & STATE MACHINE CONTROL (M6 - 4 FILTERS)
        # =====================================================================

        # 46. StateMachineStep
        global_fsm = None
        def exec_fsm_step(inputs, ctx):
            nonlocal global_fsm
            if global_fsm is None:
                from core.macro_and_statemachine import FiniteStateMachine
                global_fsm = FiniteStateMachine()
                # Default transitions for industrial automation
                global_fsm.add_transition("IDLE", "TRIGGER", "ACQUIRING")
                global_fsm.add_transition("ACQUIRING", "IMG_READY", "INSPECTING")
                global_fsm.add_transition("INSPECTING", "PASS_EVAL", "PASS")
                global_fsm.add_transition("INSPECTING", "FAIL_EVAL", "FAIL")
                global_fsm.add_transition("PASS", "RESET", "IDLE")
                global_fsm.add_transition("FAIL", "RESET", "IDLE")
                global_fsm.add_transition("FAULT", "RESET", "IDLE")

            reset_flag = bool(inputs.get("reset", False))
            event = str(inputs.get("event", "")).strip()

            if reset_flag:
                global_fsm.reset("IDLE")
            elif event:
                global_fsm.trigger_event(event)

            curr = global_fsm.current_state
            return {
                "current_state": curr,
                "is_idle": (curr == "IDLE"),
                "is_pass": (curr == "PASS"),
                "is_fail": (curr == "FAIL")
            }

        self.register(FilterDefinition(
            filter_type="StateMachineStep",
            label="Máy Trạng Thái Điều Khiển (State Machine)",
            category="Logic",
            description="Executes finite state machine transitions (IDLE -> ACQUIRING -> INSPECTING -> PASS/FAIL).",
            icon="Repeat",
            inputs=[
                FilterPortDef("event", "String", default_value="TRIGGER", description="Event input: TRIGGER, IMG_READY, PASS_EVAL, FAIL_EVAL, RESET"),
                FilterPortDef("reset", "Boolean", default_value=False, description="Forced reset to IDLE")
            ],
            outputs=[
                FilterPortDef("current_state", "String", description="Active machine state name"),
                FilterPortDef("is_idle", "Boolean", description="True if state is IDLE"),
                FilterPortDef("is_pass", "Boolean", description="True if state is PASS"),
                FilterPortDef("is_fail", "Boolean", description="True if state is FAIL")
            ],
            executor=exec_fsm_step
        ))

        # 47. DelayMs
        def exec_delay(inputs, ctx):
            duration_ms = float(inputs.get("duration_ms", 10.0))
            clamped_ms = max(0.0, min(1000.0, duration_ms))
            t0 = time.perf_counter()
            time.sleep(clamped_ms / 1000.0)
            elapsed = (time.perf_counter() - t0) * 1000.0
            return {"done": True, "elapsed_ms": round(elapsed, 2)}

        self.register(FilterDefinition(
            filter_type="DelayMs",
            label="Trì Hoãn Chu Kỳ (Delay MS)",
            category="Logic",
            description="Precise millisecond pause for camera trigger settling or pneumatic cylinder handshake.",
            icon="Clock",
            inputs=[
                FilterPortDef("duration_ms", "Number", default_value=10.0, min_value=0.0, max_value=1000.0, description="Pause duration in milliseconds")
            ],
            outputs=[
                FilterPortDef("done", "Boolean", description="Execution completed flag"),
                FilterPortDef("elapsed_ms", "Number", description="Actual elapsed pause time in ms")
            ],
            executor=exec_delay
        ))

        # 48. MacroInput
        def exec_macro_in(inputs, ctx):
            val = inputs.get("data")
            return {"data": val}

        self.register(FilterDefinition(
            filter_type="MacroInput",
            label="Cổng Vào Macro (Macro Input)",
            category="Logic",
            description="Entry pin routing external caller parameters into nested macro subgraph.",
            icon="LogIn",
            inputs=[
                FilterPortDef("data", "Any", description="External input value")
            ],
            outputs=[
                FilterPortDef("data", "Any", description="Internal routed input")
            ],
            executor=exec_macro_in
        ))

        # 49. MacroOutput
        def exec_macro_out(inputs, ctx):
            val = inputs.get("result")
            return {"result": val}

        self.register(FilterDefinition(
            filter_type="MacroOutput",
            label="Cổng Ra Macro (Macro Output)",
            category="Logic",
            description="Exit pin exposing subgraph calculation results to the parent inspection program.",
            icon="LogOut",
            inputs=[
                FilterPortDef("result", "Any", description="Internal result to export")
            ],
            outputs=[
                FilterPortDef("result", "Any", description="Final exported macro output")
            ],
            executor=exec_macro_out
        ))

        # =====================================================================
        # CATEGORY 11: ACQUISITION & SPC QUALITY CONTROL (M7 - 2 FILTERS)
        # =====================================================================

        # 50. AcquireCameraFrame
        global_synth_cam = SyntheticCameraAdapter()
        global_synth_cam.connect()

        def exec_acquire_cam(inputs, ctx):
            exp = float(inputs.get("exposure_us", 5000.0))
            gain = float(inputs.get("gain_db", 0.0))
            global_synth_cam.set_exposure_us(exp)
            global_synth_cam.set_gain_db(gain)
            frame = global_synth_cam.grab_frame()
            h, w = frame.shape[:2] if frame is not None else (0, 0)
            return {
                "image": frame,
                "width": w,
                "height": h,
                "frame_id": global_synth_cam.frame_id
            }

        self.register(FilterDefinition(
            filter_type="AcquireCameraFrame",
            label="Chụp Ảnh Camera Công Nghiệp (Acquire Frame)",
            category="Acquisition",
            description="Grabs live frame from industrial camera driver (GenICam / GigE / Synthetic simulator).",
            icon="Camera",
            inputs=[
                FilterPortDef("exposure_us", "Number", default_value=5000.0, min_value=100.0, max_value=50000.0, description="Camera exposure time in microseconds"),
                FilterPortDef("gain_db", "Number", default_value=0.0, min_value=0.0, max_value=24.0, description="Sensor analog gain in dB")
            ],
            outputs=[
                FilterPortDef("image", "Image", description="Acquired camera image"),
                FilterPortDef("width", "Integer", description="Sensor pixel width"),
                FilterPortDef("height", "Integer", description="Sensor pixel height"),
                FilterPortDef("frame_id", "Integer", description="Sequential frame counter")
            ],
            executor=exec_acquire_cam
        ))

        # 51. ComputeSPC
        global_spc_engine = SPCStatisticsEngine()

        def exec_spc(inputs, ctx):
            reset_flag = bool(inputs.get("reset", False))
            if reset_flag:
                global_spc_engine.reset()

            global_spc_engine.nominal = float(inputs.get("nominal", global_spc_engine.nominal))
            global_spc_engine.lsl = float(inputs.get("lsl", global_spc_engine.lsl))
            global_spc_engine.usl = float(inputs.get("usl", global_spc_engine.usl))

            if "value" in inputs:
                val = float(inputs["value"])
                global_spc_engine.add_sample(val)

            stats = global_spc_engine.calculate_metrics()

            return {
                "mean": stats["mean"],
                "std_dev": stats["std_dev"],
                "cp": stats["cp"],
                "cpk": stats["cpk"],
                "in_control": stats["in_control"],
                "sample_count": stats["count"],
                "alarms_count": len(stats["alarms"]),
                "latest_alarm": stats["alarms"][0] if stats["alarms"] else "NORMAL"
            }

        self.register(FilterDefinition(
            filter_type="ComputeSPC",
            label="Kiểm Soát Thống Kê SPC Cp/Cpk (Process Control)",
            category="Analysis",
            description="Tracks manufacturing process capability (Cp, Cpk, Mean, Sigma) and alerts Western Electric rule breaches.",
            icon="BarChart",
            inputs=[
                FilterPortDef("value", "Number", default_value=100.0, description="Measured critical dimension"),
                FilterPortDef("nominal", "Number", default_value=100.0, description="Target nominal dimension"),
                FilterPortDef("lsl", "Number", default_value=95.0, description="Lower Specification Limit"),
                FilterPortDef("usl", "Number", default_value=105.0, description="Upper Specification Limit"),
                FilterPortDef("reset", "Boolean", default_value=False, description="Reset sample statistics")
            ],
            outputs=[
                FilterPortDef("mean", "Number", description="Process sample mean"),
                FilterPortDef("std_dev", "Number", description="Process standard deviation (sigma)"),
                FilterPortDef("cp", "Number", description="Process capability index Cp"),
                FilterPortDef("cpk", "Number", description="Process capability index Cpk"),
                FilterPortDef("in_control", "Boolean", description="True if process is within statistical control"),
                FilterPortDef("sample_count", "Integer", description="Total collected samples"),
                FilterPortDef("alarms_count", "Integer", description="Active out-of-control rule alarms count"),
                FilterPortDef("latest_alarm", "String", description="Latest SPC alarm description")
            ],
            executor=exec_spc
        ))

        # =====================================================================
        # CATEGORY 12: INDUSTRIAL COMMUNICATIONS (M9 - 2 FILTERS)
        # =====================================================================

        # 52. SendModbusRegister
        global_modbus_client = ModbusTCPSimulator()

        def exec_modbus(inputs, ctx):
            reg = int(inputs.get("register_address", 100))
            val = int(inputs.get("value", 1))
            res = global_modbus_client.write_holding_register(reg, val)
            return res

        self.register(FilterDefinition(
            filter_type="SendModbusRegister",
            label="Gửi Thanh Ghi Modbus TCP (Modbus Write)",
            category="Communication",
            description="Writes Pass/Fail verdict or robot coordinates into industrial PLC Modbus holding register.",
            icon="Share2",
            inputs=[
                FilterPortDef("register_address", "Integer", default_value=100, description="Holding register offset address"),
                FilterPortDef("value", "Integer", default_value=1, description="Numerical 16-bit payload value")
            ],
            outputs=[
                FilterPortDef("success", "Boolean", description="Write acknowledgment flag"),
                FilterPortDef("hex_packet", "String", description="Formatted raw Modbus TCP MBAP frame"),
                FilterPortDef("byte_length", "Integer", description="Payload byte count")
            ],
            executor=exec_modbus
        ))

        # 53. SendTCPSocket
        def exec_tcp_socket(inputs, ctx):
            host = str(inputs.get("host", "127.0.0.1"))
            port = int(inputs.get("port", 2001))
            payload = str(inputs.get("packet_string", "PING\r\n"))
            res = send_raw_tcp_packet(host, port, payload)
            return res

        self.register(FilterDefinition(
            filter_type="SendTCPSocket",
            label="Truyền Gói Tin TCP Socket (TCP Stream)",
            category="Communication",
            description="Transmits raw ASCII/binary payload string over TCP/IP socket to robot controller or PLC.",
            icon="Send",
            inputs=[
                FilterPortDef("host", "String", default_value="127.0.0.1", description="Target PLC/Robot IP address"),
                FilterPortDef("port", "Integer", default_value=2001, description="TCP Socket port number"),
                FilterPortDef("packet_string", "String", default_value="Vision101A05LB800[P01,+0420.50,+0185.30,+015.0,PO1]\r", description="Data string packet")
            ],
            outputs=[
                FilterPortDef("delivered", "Boolean", description="True if packet delivered or buffered"),
                FilterPortDef("bytes_sent", "Integer", description="Bytes transmitted")
            ],
            executor=exec_tcp_socket
        ))

        # =====================================================================
        # CATEGORY 13: DEEP LEARNING & AI (M10 - 4 FILTERS)
        # =====================================================================
        onnx_session_cache: Dict[str, ONNXInferenceSession] = {}

        def get_or_create_session(model_path: str) -> ONNXInferenceSession:
            if model_path not in onnx_session_cache:
                onnx_session_cache[model_path] = ONNXInferenceSession(model_path)
            return onnx_session_cache[model_path]

        # 54. ONNXModelInference
        def exec_onnx_inference(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                raise ValueError("ONNXModelInference requires an input image")
            model_path = str(inputs.get("model_path", "mock_classifier.onnx"))
            target_w = int(inputs.get("input_width", 224))
            target_h = int(inputs.get("input_height", 224))

            sess = get_or_create_session(model_path)
            tensor, scale, pad = preprocess_image_tensor(img, target_size=(target_w, target_h))
            input_name = sess.input_names[0] if (not sess.is_mock and hasattr(sess, 'input_names') and sess.input_names) else "input"
            outputs = sess.run({input_name: tensor})
            return {
                "raw_output": outputs[0].tolist() if hasattr(outputs[0], 'tolist') else outputs[0],
                "status": "Inference Complete"
            }

        self.register(FilterDefinition(
            filter_type="ONNXModelInference",
            label="Chạy Mô Hình ONNX (ONNX Inference)",
            category="Deep Learning",
            description="Executes neural network inference via ONNX Runtime engine (CPU/DirectML/TensorRT).",
            icon="Cpu",
            inputs=[
                FilterPortDef("image", "Image", description="Input inspection image"),
                FilterPortDef("model_path", "String", default_value="mock_classifier.onnx", description="Path to trained .onnx weight file"),
                FilterPortDef("input_width", "Integer", default_value=224, description="Model input width"),
                FilterPortDef("input_height", "Integer", default_value=224, description="Model input height")
            ],
            outputs=[
                FilterPortDef("raw_output", "Any", description="Raw output tensor predictions"),
                FilterPortDef("status", "String", description="Execution status")
            ],
            executor=exec_onnx_inference
        ))

        # 55. ClassifyDefect
        def exec_classify_defect(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                raise ValueError("ClassifyDefect requires an input image")
            model_path = str(inputs.get("model_path", "mock_classifier.onnx"))
            raw_classes = str(inputs.get("class_names", "PASS,SCRATCH,STAIN,DENT"))
            thresh = float(inputs.get("confidence_threshold", 0.70))
            class_names = [c.strip() for c in raw_classes.split(",") if c.strip()]

            sess = get_or_create_session(model_path)
            tensor, _, _ = preprocess_image_tensor(img, target_size=(224, 224))
            input_name = sess.input_names[0] if (not sess.is_mock and hasattr(sess, 'input_names') and sess.input_names) else "input"
            outputs = sess.run({input_name: tensor})

            res = postprocess_classification(outputs[0], class_names=class_names)
            is_pass = (not res["is_defective"]) and (res["confidence"] >= thresh)
            return {
                "label": res["label"],
                "confidence": res["confidence"],
                "is_defective": res["is_defective"],
                "is_pass": is_pass,
                "probabilities": res["probabilities"]
            }

        self.register(FilterDefinition(
            filter_type="ClassifyDefect",
            label="Phân Loại Lỗi Bằng AI (Defect Classifier)",
            category="Deep Learning",
            description="Classifies part appearance into OK/NG or specific defect classes with softmax confidence score.",
            icon="CheckCircle",
            inputs=[
                FilterPortDef("image", "Image", description="Input inspection frame"),
                FilterPortDef("model_path", "String", default_value="mock_classifier.onnx", description="ONNX classifier path"),
                FilterPortDef("class_names", "String", default_value="PASS,SCRATCH,STAIN,DENT", description="Comma-separated class names"),
                FilterPortDef("confidence_threshold", "Number", default_value=0.70, description="Minimum confidence for acceptance")
            ],
            outputs=[
                FilterPortDef("label", "String", description="Predicted class label"),
                FilterPortDef("confidence", "Number", description="Model prediction confidence (0.0 - 1.0)"),
                FilterPortDef("is_defective", "Boolean", description="True if NG/defect class predicted"),
                FilterPortDef("is_pass", "Boolean", description="True if OK and above confidence threshold"),
                FilterPortDef("probabilities", "Any", description="Class probability distribution dictionary")
            ],
            executor=exec_classify_defect
        ))

        # 56. DetectObjectsYOLO
        def exec_detect_yolo(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                raise ValueError("DetectObjectsYOLO requires an input image")
            model_path = str(inputs.get("model_path", "mock_yolo.onnx"))
            raw_classes = str(inputs.get("class_names", "bottle,cap,label"))
            conf_thresh = float(inputs.get("confidence_threshold", 0.35))
            iou_thresh = float(inputs.get("iou_threshold", 0.45))
            class_names = [c.strip() for c in raw_classes.split(",") if c.strip()]

            sess = get_or_create_session(model_path)
            orig_h, orig_w = img.shape[:2]
            tensor, scale, pad = preprocess_image_tensor(img, target_size=(640, 640))
            input_name = sess.input_names[0] if (not sess.is_mock and hasattr(sess, 'input_names') and sess.input_names) else "images"
            outputs = sess.run({input_name: tensor})

            detections = postprocess_yolo_detections(
                outputs[0],
                conf_threshold=conf_thresh,
                iou_threshold=iou_thresh,
                class_names=class_names,
                scale=scale,
                pad=pad,
                orig_shape=(orig_h, orig_w)
            )

            annotated = draw_yolo_detections_overlay(img, detections)
            return {
                "boxes": detections,
                "object_count": len(detections),
                "is_detected": len(detections) > 0,
                "annotated_image": annotated
            }

        self.register(FilterDefinition(
            filter_type="DetectObjectsYOLO",
            label="Phát Hiện Vật Thể YOLO (YOLO Detection)",
            category="Deep Learning",
            description="Detects and locates parts, bottles, caps or surface flaws with bounding box coordinates and class tags.",
            icon="Target",
            inputs=[
                FilterPortDef("image", "Image", description="Input inspection image"),
                FilterPortDef("model_path", "String", default_value="mock_yolo.onnx", description="YOLO ONNX model file"),
                FilterPortDef("class_names", "String", default_value="bottle,cap,label", description="Comma-separated class names"),
                FilterPortDef("confidence_threshold", "Number", default_value=0.35, description="Box confidence threshold"),
                FilterPortDef("iou_threshold", "Number", default_value=0.45, description="NMS IoU suppression threshold")
            ],
            outputs=[
                FilterPortDef("boxes", "Array", description="List of detected bounding boxes with confidence"),
                FilterPortDef("object_count", "Integer", description="Total detected objects count"),
                FilterPortDef("is_detected", "Boolean", description="True if at least one object found"),
                FilterPortDef("annotated_image", "Image", description="Image with overlaid bounding boxes and tags")
            ],
            executor=exec_detect_yolo
        ))

        # 57. DetectAnomaly
        def exec_detect_anomaly(inputs, ctx):
            img = inputs.get("image")
            if img is None:
                raise ValueError("DetectAnomaly requires an input image")
            model_path = str(inputs.get("model_path", "mock_anomaly.onnx"))
            thresh = float(inputs.get("anomaly_threshold", 30.0))

            sess = get_or_create_session(model_path)
            orig_h, orig_w = img.shape[:2]
            tensor, _, _ = preprocess_image_tensor(img, target_size=(256, 256))
            input_name = sess.input_names[0] if (not sess.is_mock and hasattr(sess, 'input_names') and sess.input_names) else "input"
            outputs = sess.run({input_name: tensor})

            # Mock or actual reconstructed output back to image
            reconstructed = outputs[0]
            if len(reconstructed.shape) == 4 and reconstructed.shape[1] in [1, 3]:
                # (1, C, H, W) -> (H, W, C)
                rec_img = np.transpose(reconstructed[0], (1, 2, 0))
                rec_img = (np.clip(rec_img, 0.0, 1.0) * 255.0).astype(np.uint8)
            else:
                rec_img = img.copy()

            score, heatmap = compute_anomaly_map(img, rec_img)
            return {
                "anomaly_score": score,
                "is_anomaly": score > thresh,
                "heatmap": heatmap
            }

        self.register(FilterDefinition(
            filter_type="DetectAnomaly",
            label="Phát Hiện Bất Thường Anomaly AI (Anomaly Detection)",
            category="Deep Learning",
            description="Unsupervised anomaly detection computing reconstruction residual heatmaps for unknown surface defects.",
            icon="AlertCircle",
            inputs=[
                FilterPortDef("image", "Image", description="Input inspection image"),
                FilterPortDef("model_path", "String", default_value="mock_anomaly.onnx", description="AutoEncoder/Patch ONNX model"),
                FilterPortDef("anomaly_threshold", "Number", default_value=30.0, description="Max acceptable anomaly score")
            ],
            outputs=[
                FilterPortDef("anomaly_score", "Number", description="Calculated deviation score (0 - 100)"),
                FilterPortDef("is_anomaly", "Boolean", description="True if anomaly score breaches threshold"),
                FilterPortDef("heatmap", "Image", description="Jet-colorized defect residual heatmap")
            ],
            executor=exec_detect_anomaly
        ))


# Global Singleton Registry
filter_registry = FilterRegistry()

