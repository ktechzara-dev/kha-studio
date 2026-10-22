"""
High-Speed Bottle Detector & Orientation Classifier for Unilever Delta Robot Line
Optimized for < 15ms detection cycle on 1936x1216 Mono8 frames.
Extracts:
- Sub-pixel Centroid (m10/m00, m01/m00)
- Principle Axis of Inertia (Central Moments mu20, mu02, mu11)
- Deterministic PO1 vs PO2 (Neck vs Base) orientation classification
- Fast bounding box and contour analysis
"""

import time
import logging
from dataclasses import dataclass, field
import numpy as np
import cv2

from core.calibration import CameraCalibration

logger = logging.getLogger("UnileverVision.Detector")


@dataclass
class DetectedBottle:
    id: int
    pixel_x: float
    pixel_y: float
    robot_x: float
    robot_y: float
    angle_deg: float
    robot_angle_deg: float
    po_type: str  # 'PO1' (Cap leading/forward) or 'PO2' (Base leading/180 deg)
    area: float
    aspect_ratio: float
    bbox: tuple[int, int, int, int]  # (x, y, w, h) in full image coordinates
    contour: np.ndarray = field(repr=False)
    confidence: float = 0.98


@dataclass
class DetectorRecipe:
    name: str = "Lifebuoy 800ml"
    model_code: str = "LB800"
    roi: tuple[int, int, int, int] = (150, 100, 1636, 1000)  # (x, y, w, h)
    threshold_mode: str = "OTSU"  # 'OTSU', 'MANUAL', 'ADAPTIVE'
    binary_threshold: int = 120
    invert_threshold: bool = False
    min_area: float = 8000.0
    max_area: float = 85000.0
    min_aspect_ratio: float = 1.35
    max_aspect_ratio: float = 4.20
    neck_sensitivity: float = 0.65  # Ratio cutoff between neck and base width

    def to_dict(self) -> dict:
        return {
            "name": self.name,
            "model_code": self.model_code,
            "roi": list(self.roi),
            "threshold_mode": self.threshold_mode,
            "binary_threshold": self.binary_threshold,
            "invert_threshold": self.invert_threshold,
            "min_area": self.min_area,
            "max_area": self.max_area,
            "min_aspect_ratio": self.min_aspect_ratio,
            "max_aspect_ratio": self.max_aspect_ratio,
            "neck_sensitivity": self.neck_sensitivity
        }

    @classmethod
    def from_dict(cls, d: dict) -> "DetectorRecipe":
        return cls(
            name=d.get("name", "Default"),
            model_code=d.get("model_code", "GEN01"),
            roi=tuple(d.get("roi", [150, 100, 1636, 1000])),
            threshold_mode=d.get("threshold_mode", "OTSU"),
            binary_threshold=int(d.get("binary_threshold", 120)),
            invert_threshold=bool(d.get("invert_threshold", False)),
            min_area=float(d.get("min_area", 8000.0)),
            max_area=float(d.get("max_area", 85000.0)),
            min_aspect_ratio=float(d.get("min_aspect_ratio", 1.35)),
            max_aspect_ratio=float(d.get("max_aspect_ratio", 4.20)),
            neck_sensitivity=float(d.get("neck_sensitivity", 0.65))
        )


class BottleDetector:
    def __init__(self, calibration: CameraCalibration | None = None):
        self.calibration = calibration or CameraCalibration()
        self.recipe = DetectorRecipe()

        # Preallocated 3x3 morphological kernel
        self._kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3))

        # Benchmarking metrics
        self.last_execution_time_ms = 0.0
        self.timings = {
            "crop_ms": 0.0,
            "thresh_ms": 0.0,
            "morph_ms": 0.0,
            "contours_ms": 0.0,
            "features_ms": 0.0,
            "total_ms": 0.0
        }

    def detect(self, image: np.ndarray) -> list[DetectedBottle]:
        """
        Executes the detection pipeline on the input image.
        Target execution time: < 15.0 ms.
        Returns: list of DetectedBottle objects sorted by X coordinate (conveyor flow).
        """
        t0 = time.perf_counter()

        # Step 1: Crop ROI with zero-copy slice
        rx, ry, rw, rh = self.recipe.roi
        img_h, img_w = image.shape[:2]

        # Clamp ROI to image boundaries
        rx = max(0, min(rx, img_w - 10))
        ry = max(0, min(ry, img_h - 10))
        rw = max(10, min(rw, img_w - rx))
        rh = max(10, min(rh, img_h - ry))

        if len(image.shape) == 3:
            roi_gray = cv2.cvtColor(image[ry:ry+rh, rx:rx+rw], cv2.COLOR_BGR2GRAY)
        else:
            roi_gray = image[ry:ry+rh, rx:rx+rw]

        t1 = time.perf_counter()

        # Step 2: Binarization (Thresholding)
        if self.recipe.threshold_mode == "OTSU":
            thresh_flag = cv2.THRESH_BINARY_INV if self.recipe.invert_threshold else cv2.THRESH_BINARY
            _, bin_img = cv2.threshold(roi_gray, 0, 255, thresh_flag + cv2.THRESH_OTSU)
        elif self.recipe.threshold_mode == "ADAPTIVE":
            thresh_flag = cv2.THRESH_BINARY_INV if self.recipe.invert_threshold else cv2.THRESH_BINARY
            bin_img = cv2.adaptiveThreshold(roi_gray, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, thresh_flag, 31, 5)
        else:
            thresh_flag = cv2.THRESH_BINARY_INV if self.recipe.invert_threshold else cv2.THRESH_BINARY
            _, bin_img = cv2.threshold(roi_gray, self.recipe.binary_threshold, 255, thresh_flag)

        t2 = time.perf_counter()

        # Step 3: Fast morphological opening (removes small conveyor texture noise)
        bin_clean = cv2.morphologyEx(bin_img, cv2.MORPH_OPEN, self._kernel, iterations=1)
        t3 = time.perf_counter()

        # Step 4: Contour extraction
        contours, _ = cv2.findContours(bin_clean, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        t4 = time.perf_counter()

        # Step 5: Feature Extraction & Moment Analysis
        bottles: list[DetectedBottle] = []
        bottle_id = 1

        for cnt in contours:
            area = cv2.contourArea(cnt)
            if area < self.recipe.min_area or area > self.recipe.max_area:
                continue

            # Minimum bounding box
            bx, by, bw, bh = cv2.boundingRect(cnt)
            aspect_ratio = max(bw, bh) / max(1.0, min(bw, bh))

            if aspect_ratio < self.recipe.min_aspect_ratio or aspect_ratio > self.recipe.max_aspect_ratio:
                continue

            # Compute Spatial & Central Moments
            M = cv2.moments(cnt)
            if M['m00'] <= 1e-4:
                continue

            # Subpixel centroid in full image coordinates
            cx = (M['m10'] / M['m00']) + rx
            cy = (M['m01'] / M['m00']) + ry

            # Principal Axis of Inertia angle from Central Moments:
            # theta = 0.5 * atan2(2 * mu11, mu20 - mu02)
            mu20 = M['mu20'] / M['m00']
            mu02 = M['mu02'] / M['m00']
            mu11 = M['mu11'] / M['m00']

            angle_rad = 0.5 * np.arctan2(2.0 * mu11, mu20 - mu02)
            angle_deg = np.degrees(angle_rad)

            # Classify Neck vs Base (PO1 vs PO2)
            po_type, corrected_angle_deg = self._classify_po_and_correct_angle(
                cnt, M, rx, ry, cx, cy, angle_deg
            )

            # Map to Robot Coordinates via Calibration Homography
            robot_x, robot_y, robot_angle = self.calibration.pixel_to_robot(
                cx, cy, corrected_angle_deg
            )

            # Offset contour to full image coordinate system
            cnt_full = cnt.copy()
            cnt_full[:, 0, 0] += rx
            cnt_full[:, 0, 1] += ry

            bottle = DetectedBottle(
                id=bottle_id,
                pixel_x=cx,
                pixel_y=cy,
                robot_x=robot_x,
                robot_y=robot_y,
                angle_deg=corrected_angle_deg,
                robot_angle_deg=robot_angle,
                po_type=po_type,
                area=area,
                aspect_ratio=aspect_ratio,
                bbox=(bx + rx, by + ry, bw, bh),
                contour=cnt_full,
                confidence=min(0.99, max(0.85, 1.0 - abs(aspect_ratio - 2.5) / 10.0))
            )
            bottles.append(bottle)
            bottle_id += 1

        t5 = time.perf_counter()

        # Sort bottles from left to right along conveyor feed direction
        bottles.sort(key=lambda b: b.pixel_x)

        # Re-assign sequential IDs
        for idx, b in enumerate(bottles, start=1):
            b.id = idx

        self.last_execution_time_ms = (t5 - t0) * 1000.0
        self.timings = {
            "crop_ms": (t1 - t0) * 1000.0,
            "thresh_ms": (t2 - t1) * 1000.0,
            "morph_ms": (t3 - t2) * 1000.0,
            "contours_ms": (t4 - t3) * 1000.0,
            "features_ms": (t5 - t4) * 1000.0,
            "total_ms": self.last_execution_time_ms
        }

        return bottles

    def _classify_po_and_correct_angle(
        self,
        cnt: np.ndarray,
        M: dict,
        rx: int,
        ry: int,
        cx: float,
        cy: float,
        angle_deg: float
    ) -> tuple[str, float]:
        """
        Determines whether the bottle neck (cap) is pointing forward (PO1)
        or backward/inverted (PO2) by analyzing width/area profile along the principal axis.
        Also resolves the 180° ambiguity of the moment-based orientation angle.
        """
        # Direction vector along principal axis
        rad = np.radians(angle_deg)
        vx = np.cos(rad)
        vy = np.sin(rad)

        # Center points relative to centroid in ROI coordinates
        rel_x = (cnt[:, 0, 0] + rx) - cx
        rel_y = (cnt[:, 0, 1] + ry) - cy

        # Dot product with direction vector v
        projections = rel_x * vx + rel_y * vy

        # Split contour points into two halves along principal axis
        pos_half = projections[projections > 10.0]
        neg_half = projections[projections < -10.0]

        # Use 3rd order central moment / skewness if available,
        # or compare the count/variance of points in positive vs negative halves
        # Bottle neck has significantly fewer pixels / narrower distribution
        count_pos = len(pos_half)
        count_neg = len(neg_half)

        # Narrower end is the neck/cap
        if count_pos < count_neg * self.recipe.neck_sensitivity:
            # Positive half is neck -> bottle points along +v
            po = "PO1"
            final_angle = angle_deg
        elif count_neg < count_pos * self.recipe.neck_sensitivity:
            # Negative half is neck -> bottle points along -v (180 deg opposite)
            po = "PO2"
            final_angle = (angle_deg + 180.0) % 360.0
            if final_angle > 180.0:
                final_angle -= 360.0
        else:
            # Symmetrical or borderline: check 3rd moment mu30/mu03
            skew = (M.get('mu30', 0.0) * vx + M.get('mu03', 0.0) * vy)
            if skew < 0:
                po = "PO1"
                final_angle = angle_deg
            else:
                po = "PO2"
                final_angle = (angle_deg + 180.0) % 360.0
                if final_angle > 180.0:
                    final_angle -= 360.0

        return po, final_angle
