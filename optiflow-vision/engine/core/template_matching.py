"""
OptiFlow Vision Studio - Template Matching, Alignment & Coordinate Systems
Milestone 5 Implementation:
- Multi-tier Pyramid Normalized Cross-Correlation (NCC)
- Angle-searchable Geometric Edge / Grayscale Template Matcher
- 2D Local Coordinate System (Part Alignment / Fixture Frame)
- Geometric ROI Alignment (translates and rotates inspection ROIs to part frame)
- Golden Template Differential Defect Inspection
"""

import math
from typing import List, Dict, Any, Tuple, Optional
import numpy as np
import cv2


# ==============================================================================
# 1. 2D COORDINATE SYSTEMS & AFFINE ALIGNMENT (FIXTURE)
# ==============================================================================

class CoordinateSystem2D:
    """
    2D Local Coordinate Frame defined by origin (x0, y0) and rotation angle in degrees.
    Used to fixture inspection ROIs to moving parts on conveyor belts.
    """
    def __init__(self, origin_x: float = 0.0, origin_y: float = 0.0, angle_deg: float = 0.0):
        self.origin_x = float(origin_x)
        self.origin_y = float(origin_y)
        self.angle_deg = float(angle_deg)
        self.angle_rad = math.radians(self.angle_deg)
        self.cos_a = math.cos(self.angle_rad)
        self.sin_a = math.sin(self.angle_rad)

    def to_dict(self) -> Dict[str, Any]:
        return {
            "origin_x": round(self.origin_x, 3),
            "origin_y": round(self.origin_y, 3),
            "angle_deg": round(self.angle_deg, 2)
        }

    def point_to_world(self, local_x: float, local_y: float) -> Tuple[float, float]:
        """Maps point from local part coordinates to global image space."""
        world_x = self.origin_x + local_x * self.cos_a - local_y * self.sin_a
        world_y = self.origin_y + local_x * self.sin_a + local_y * self.cos_a
        return (round(float(world_x), 3), round(float(world_y), 3))

    def point_to_local(self, world_x: float, world_y: float) -> Tuple[float, float]:
        """Maps point from global image space to local part coordinates."""
        dx = world_x - self.origin_x
        dy = world_y - self.origin_y
        local_x = dx * self.cos_a + dy * self.sin_a
        local_y = -dx * self.sin_a + dy * self.cos_a
        return (round(float(local_x), 3), round(float(local_y), 3))

    def align_rectangle(self, local_rect: Dict[str, float]) -> Dict[str, Any]:
        """
        Transforms local ROI rectangle {x, y, width, height} to world coordinate box.
        """
        lx = float(local_rect.get("x", 0.0))
        ly = float(local_rect.get("y", 0.0))
        w = float(local_rect.get("width", 100.0))
        h = float(local_rect.get("height", 100.0))

        # 4 corners in local space
        p_tl = self.point_to_world(lx, ly)
        p_tr = self.point_to_world(lx + w, ly)
        p_br = self.point_to_world(lx + w, ly + h)
        p_bl = self.point_to_world(lx, ly + h)

        return {
            "center_x": round((p_tl[0] + p_br[0]) / 2.0, 3),
            "center_y": round((p_tl[1] + p_br[1]) / 2.0, 3),
            "width": round(w, 3),
            "height": round(h, 3),
            "angle_deg": round(self.angle_deg, 2),
            "corners": [p_tl, p_tr, p_br, p_bl]
        }

    def align_scan_ray(self, p1_local: Tuple[float, float], p2_local: Tuple[float, float]) -> Tuple[Tuple[float, float], Tuple[float, float]]:
        """Maps 1D measurement scan line from part coordinates to world image space."""
        p1_w = self.point_to_world(p1_local[0], p1_local[1])
        p2_w = self.point_to_world(p2_local[0], p2_local[1])
        return p1_w, p2_w


# ==============================================================================
# 2. MULTI-SCALE & ANGLE-SEARCHABLE TEMPLATE MATCHING
# ==============================================================================

def match_template_ncc_pyramid(
    image: np.ndarray,
    template: np.ndarray,
    min_score: float = 0.70,
    pyramid_levels: int = 2
) -> List[Dict[str, Any]]:
    """
    Fast Normalized Cross-Correlation (NCC) with Gaussian multi-resolution pyramid.
    Matches coarse level first to locate candidate ROIs, then refines at level 0.
    """
    if image is None or template is None:
        return []

    # Ensure grayscale
    gray_img = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image
    gray_tpl = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY) if len(template.shape) == 3 else template

    th, tw = gray_tpl.shape[:2]
    ih, iw = gray_img.shape[:2]

    if tw > iw or th > ih or tw < 5 or th < 5:
        return []

    # Level 0 direct match if template is small
    if pyramid_levels <= 1 or tw < 32 or th < 32:
        res = cv2.matchTemplate(gray_img, gray_tpl, cv2.TM_CCOEFF_NORMED)
        min_v, max_v, min_l, max_l = cv2.minMaxLoc(res)
        if max_v >= min_score:
            top_left_x, top_left_y = max_l
            return [{
                "x": float(top_left_x + tw / 2.0),
                "y": float(top_left_y + th / 2.0),
                "top_left_x": float(top_left_x),
                "top_left_y": float(top_left_y),
                "width": float(tw),
                "height": float(th),
                "score": round(float(max_v), 3),
                "angle_deg": 0.0
            }]
        return []

    # 2-Tier Pyramid Optimization
    scale = 0.5
    img_coarse = cv2.resize(gray_img, (0, 0), fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    tpl_coarse = cv2.resize(gray_tpl, (0, 0), fx=scale, fy=scale, interpolation=cv2.INTER_AREA)

    res_coarse = cv2.matchTemplate(img_coarse, tpl_coarse, cv2.TM_CCOEFF_NORMED)
    _, max_v, _, max_l = cv2.minMaxLoc(res_coarse)

    if max_v < (min_score * 0.85):
        return []

    # Refine at fine level in local bounding neighborhood (+- 16 px)
    cand_x = int(max_l[0] / scale)
    cand_y = int(max_l[1] / scale)

    search_pad = 16
    x0 = max(0, cand_x - search_pad)
    y0 = max(0, cand_y - search_pad)
    x1 = min(iw, cand_x + tw + search_pad)
    y1 = min(ih, cand_y + th + search_pad)

    roi_img = gray_img[y0:y1, x0:x1]
    if roi_img.shape[0] < th or roi_img.shape[1] < tw:
        return []

    res_fine = cv2.matchTemplate(roi_img, gray_tpl, cv2.TM_CCOEFF_NORMED)
    _, fine_max_v, _, fine_max_l = cv2.minMaxLoc(res_fine)

    if fine_max_v >= min_score:
        final_tl_x = x0 + fine_max_l[0]
        final_tl_y = y0 + fine_max_l[1]
        return [{
            "x": float(final_tl_x + tw / 2.0),
            "y": float(final_tl_y + th / 2.0),
            "top_left_x": float(final_tl_x),
            "top_left_y": float(final_tl_y),
            "width": float(tw),
            "height": float(th),
            "score": round(float(fine_max_v), 3),
            "angle_deg": 0.0
        }]

    return []


def match_template_with_rotation(
    image: np.ndarray,
    template: np.ndarray,
    min_score: float = 0.75,
    min_angle_deg: float = -30.0,
    max_angle_deg: float = 30.0,
    angle_step_deg: float = 5.0
) -> Optional[Dict[str, Any]]:
    """
    Template matcher with rotation search. Evaluates multiple rotation angles
    and returns the best candidate pose (x, y, angle_deg, score).
    """
    if image is None or template is None:
        return None

    gray_img = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY) if len(image.shape) == 3 else image
    gray_tpl = cv2.cvtColor(template, cv2.COLOR_BGR2GRAY) if len(template.shape) == 3 else template

    th, tw = gray_tpl.shape[:2]
    ih, iw = gray_img.shape[:2]
    if tw > iw or th > ih:
        return None

    center_tpl = (tw / 2.0, th / 2.0)
    best_match = None
    best_score = -1.0

    angles = np.arange(min_angle_deg, max_angle_deg + 1e-4, angle_step_deg)
    for ang in angles:
        rot_mat = cv2.getRotationMatrix2D(center_tpl, float(ang), 1.0)
        rotated_tpl = cv2.warpAffine(gray_tpl, rot_mat, (tw, th), flags=cv2.INTER_LINEAR, borderMode=cv2.BORDER_REPLICATE)

        res = cv2.matchTemplate(gray_img, rotated_tpl, cv2.TM_CCOEFF_NORMED)
        _, max_v, _, max_l = cv2.minMaxLoc(res)

        if max_v > best_score:
            best_score = float(max_v)
            tl_x, tl_y = max_l
            best_match = {
                "x": round(float(tl_x + tw / 2.0), 3),
                "y": round(float(tl_y + th / 2.0), 3),
                "top_left_x": float(tl_x),
                "top_left_y": float(tl_y),
                "width": float(tw),
                "height": float(th),
                "angle_deg": round(float(ang), 2),
                "score": round(best_score, 3)
            }

    if best_match and best_match["score"] >= min_score:
        return best_match
    return None


# ==============================================================================
# 3. GOLDEN TEMPLATE DIFFERENTIAL INSPECTION
# ==============================================================================

def compare_golden_template(
    live_image: np.ndarray,
    golden_template: np.ndarray,
    diff_threshold: int = 35,
    min_defect_area: int = 15
) -> Dict[str, Any]:
    """
    Computes absolute difference image between aligned live part and reference
    golden sample to detect surface flaws, missing pins, scratches, or burns.
    """
    if live_image is None or golden_template is None:
        return {"defect_count": 0, "max_defect_area": 0, "has_defects": False, "defect_mask": None}

    g_live = cv2.cvtColor(live_image, cv2.COLOR_BGR2GRAY) if len(live_image.shape) == 3 else live_image
    g_gold = cv2.cvtColor(golden_template, cv2.COLOR_BGR2GRAY) if len(golden_template.shape) == 3 else golden_template

    # Align dimensions if necessary
    h = min(g_live.shape[0], g_gold.shape[0])
    w = min(g_live.shape[1], g_gold.shape[1])
    diff = cv2.absdiff(g_live[:h, :w], g_gold[:h, :w])

    _, mask = cv2.threshold(diff, diff_threshold, 255, cv2.THRESH_BINARY)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3))
    clean_mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel)

    num_labels, labels, stats, centroids = cv2.connectedComponentsWithStats(clean_mask)

    defect_count = 0
    max_area = 0
    defect_boxes = []

    for i in range(1, num_labels):
        area = int(stats[i, cv2.CC_STAT_AREA])
        if area >= min_defect_area:
            defect_count += 1
            if area > max_area:
                max_area = area
            defect_boxes.append({
                "x": int(stats[i, cv2.CC_STAT_LEFT]),
                "y": int(stats[i, cv2.CC_STAT_TOP]),
                "width": int(stats[i, cv2.CC_STAT_WIDTH]),
                "height": int(stats[i, cv2.CC_STAT_HEIGHT]),
                "area": area
            })

    return {
        "defect_count": defect_count,
        "max_defect_area": max_area,
        "has_defects": (defect_count > 0),
        "defects": defect_boxes,
        "diff_image": diff,
        "defect_mask": clean_mask
    }
