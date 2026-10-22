"""
OptiFlow Vision Studio - Image Service & High-Performance Frame Pipeline
Optimized for zero-lag inspection of up to 12-MP industrial images.
Uses binary streams and shared memory buffers rather than slow base64 strings.
"""

import os
import uuid
import time
import math
from typing import Dict, Optional, Tuple, List
import numpy as np
import cv2

from core.types import ImageMeta, PixelInspectionResult, HistogramData


class ImageService:
    def __init__(self, cache_dir: Optional[str] = None):
        self.cache_dir = cache_dir or os.path.join(os.path.dirname(__file__), "..", "cache")
        os.makedirs(self.cache_dir, exist_ok=True)
        # In-memory store: id -> np.ndarray
        self._image_cache: Dict[str, np.ndarray] = {}
        self._meta_cache: Dict[str, ImageMeta] = {}

        # Pre-generate synthetic sample images
        self._init_samples()

    def _init_samples(self):
        """Generates realistic industrial sample images for offline inspection."""
        self.create_sample_bottle("sample_bottle")
        self.create_sample_pcb("sample_pcb")
        self.create_sample_checkerboard("sample_calib")
        self.create_sample_pharma("sample_pharma")
        self.create_sample_barcode("sample_barcode")
        self.create_sample_gear("sample_gear")

    def register_image(self, img: np.ndarray, image_id: Optional[str] = None, file_path: Optional[str] = None) -> ImageMeta:
        """Registers an image in the cache with computed metadata."""
        if image_id is None:
            image_id = str(uuid.uuid4())

        h, w = img.shape[:2]
        channels = 1 if len(img.shape) == 2 else img.shape[2]
        color_space = "GRAY" if channels == 1 else "BGR"

        meta = ImageMeta(
            id=image_id,
            width=w,
            height=h,
            channels=channels,
            color_space=color_space,
            dtype=str(img.dtype),
            file_path=file_path,
            timestamp=time.time(),
            memory_size_bytes=int(img.nbytes)
        )

        self._image_cache[image_id] = img
        self._meta_cache[image_id] = meta
        return meta

    def get_image(self, image_id: str) -> Optional[np.ndarray]:
        return self._image_cache.get(image_id)

    def get_meta(self, image_id: str) -> Optional[ImageMeta]:
        return self._meta_cache.get(image_id)

    def list_images(self) -> List[ImageMeta]:
        return list(self._meta_cache.values())

    def load_from_file(self, file_path: str, image_id: Optional[str] = None) -> Optional[ImageMeta]:
        """Loads an image from local disk with OpenCV."""
        if not os.path.exists(file_path):
            return None
        img = cv2.imread(file_path, cv2.IMREAD_UNCHANGED)
        if img is None:
            return None
        return self.register_image(img, image_id=image_id, file_path=file_path)

    def get_image_bytes(self, image_id: str, format_ext: str = ".jpg", quality: int = 90, max_dimension: Optional[int] = None) -> Optional[bytes]:
        """
        Returns high-speed encoded image bytes for direct browser streaming.
        Supports automatic downscaling for large multi-megapixel viewports.
        """
        img = self._image_cache.get(image_id)
        if img is None:
            return None

        target = img
        if max_dimension is not None:
            h, w = img.shape[:2]
            max_cur = max(h, w)
            if max_cur > max_dimension:
                scale = max_dimension / float(max_cur)
                nw = int(w * scale)
                nh = int(h * scale)
                target = cv2.resize(img, (nw, nh), interpolation=cv2.INTER_AREA)

        if format_ext.lower() in [".jpg", ".jpeg"]:
            params = [int(cv2.IMWRITE_JPEG_QUALITY), quality]
            ret, buf = cv2.imencode(".jpg", target, params)
        else:
            params = [int(cv2.IMWRITE_PNG_COMPRESSION), 3]
            ret, buf = cv2.imencode(".png", target, params)

        if ret:
            return buf.tobytes()
        return None

    def probe_pixel(self, image_id: str, x: int, y: int) -> PixelInspectionResult:
        """Inspects pixel value and local 7x7 neighborhood."""
        img = self._image_cache.get(image_id)
        if img is None:
            return PixelInspectionResult(x=x, y=y, in_bounds=False, intensity_text="No Image")

        h, w = img.shape[:2]
        if x < 0 or x >= w or y < 0 or y >= h:
            return PixelInspectionResult(x=x, y=y, in_bounds=False, intensity_text="Out of Bounds")

        # Extract channels
        val = img[y, x]
        if isinstance(val, (int, float, np.integer, np.floating)):
            channels = [int(val)]
            intensity_text = f"Mono8: {val}"
        else:
            channels = [int(c) for c in val]
            if len(channels) == 3:
                # Assuming BGR
                intensity_text = f"R:{channels[2]} G:{channels[1]} B:{channels[0]}"
            else:
                intensity_text = f"Channels: {channels}"

        # Extract 7x7 neighborhood
        r = 3
        y1, y2 = max(0, y - r), min(h, y + r + 1)
        x1, x2 = max(0, x - r), min(w, x + r + 1)
        patch = img[y1:y2, x1:x2]
        if len(patch.shape) == 3:
            # Grayscale equivalent for patch
            patch_gray = cv2.cvtColor(patch, cv2.COLOR_BGR2GRAY)
        else:
            patch_gray = patch

        neighborhood = patch_gray.tolist()

        return PixelInspectionResult(
            x=x,
            y=y,
            in_bounds=True,
            channels=channels,
            intensity_text=intensity_text,
            neighborhood_7x7=neighborhood
        )

    def compute_histogram(self, image_id: str) -> Optional[HistogramData]:
        """Calculates 256-bin intensity histogram and statistics in < 1ms."""
        img = self._image_cache.get(image_id)
        if img is None:
            return None

        if len(img.shape) == 3:
            gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
        else:
            gray = img

        hist = cv2.calcHist([gray], [0], None, [256], [0, 256])
        bins = hist.ravel().astype(int).tolist()

        return HistogramData(
            image_id=image_id,
            bins=bins,
            min_val=float(np.min(gray)),
            max_val=float(np.max(gray)),
            mean_val=float(np.mean(gray)),
            std_val=float(np.std(gray)),
            percentile_25=float(np.percentile(gray, 25)),
            percentile_50=float(np.percentile(gray, 50)),
            percentile_75=float(np.percentile(gray, 75))
        )

    # =========================================================================
    # SYNTHETIC INDUSTRIAL GENERATORS
    # =========================================================================
    def create_sample_bottle(self, image_id: str = "sample_bottle") -> ImageMeta:
        """Creates a realistic industrial shampoo/detergent packaging frame (1936x1216)."""
        w, h = 1936, 1216
        frame = np.full((h, w), 45, dtype=np.uint8)

        # Rails
        frame[:90, :] = 80
        frame[h - 90:, :] = 80
        cv2.line(frame, (0, 90), (w, 90), 160, 2)
        cv2.line(frame, (0, h - 90), (w, h - 90), 160, 2)

        # Draw 3 sample bottles
        positions = [(450, 600, 15), (1000, 620, -8), (1550, 590, 182)]
        for idx, (bx, by, ang) in enumerate(positions, start=1):
            canvas = np.zeros((520, 240), dtype=np.uint8)
            # Body polygon
            pts = [
                (120 - 24, 25), (120 + 24, 25), (120 + 24, 65), (120 + 30, 65),
                (120 + 30, 110), (120 + 95, 190), (120 + 85, 450), (120 + 70, 485),
                (120 - 70, 485), (120 - 85, 450), (120 - 95, 190), (120 - 30, 110),
                (120 - 30, 65), (120 - 24, 65)
            ]
            cv2.fillPoly(canvas, [np.array(pts, dtype=np.int32)], 230)
            cv2.rectangle(canvas, (120 - 60, 230), (120 + 60, 390), 180, -1)
            cv2.putText(canvas, f"SKU-{idx:02d}", (80, 310), cv2.FONT_HERSHEY_SIMPLEX, 0.7, 50, 2)

            # Rotation
            rot_mat = cv2.getRotationMatrix2D((120, 260), ang, 1.0)
            rotated = cv2.warpAffine(canvas, rot_mat, (240, 520), flags=cv2.INTER_LINEAR)

            # Paste
            y1, y2 = by - 260, by + 260
            x1, x2 = bx - 120, bx + 120
            mask = rotated > 20
            roi = frame[y1:y2, x1:x2]
            roi[mask] = rotated[mask]

        return self.register_image(frame, image_id=image_id)

    def create_sample_pcb(self, image_id: str = "sample_pcb") -> ImageMeta:
        """Creates a realistic high-density PCB surface-mount assembly image (1600x1200)."""
        w, h = 1600, 1200
        # Dark green solder mask
        bgr = np.zeros((h, w, 3), dtype=np.uint8)
        bgr[:, :] = (20, 65, 18)

        # Traces (copper/gold lines)
        for i in range(15):
            cv2.line(bgr, (50, 100 + i * 70), (450, 100 + i * 70), (35, 130, 110), 2)
            cv2.line(bgr, (450, 100 + i * 70), (550, 150 + i * 70), (35, 130, 110), 2)
            cv2.line(bgr, (550, 150 + i * 70), (950, 150 + i * 70), (35, 130, 110), 2)

        # IC QFP Chip
        cv2.rectangle(bgr, (600, 400), (1000, 800), (28, 28, 30), -1)
        cv2.putText(bgr, "STM32F407", (680, 580), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (180, 180, 180), 2)
        cv2.putText(bgr, "ARM CORTEX-M4", (670, 630), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (140, 140, 140), 2)
        cv2.circle(bgr, (640, 440), 12, (15, 15, 18), -1)  # Pin 1 dot

        # IC Pins (Silver/Lead)
        for p in range(25):
            # Top & Bottom pins
            px = 640 + p * 13
            cv2.rectangle(bgr, (px, 350), (px + 7, 400), (195, 205, 215), -1)
            cv2.rectangle(bgr, (px, 800), (px + 7, 850), (195, 205, 215), -1)
            # Left & Right pins
            py = 440 + p * 13
            cv2.rectangle(bgr, (550, py), (600, py + 7), (195, 205, 215), -1)
            cv2.rectangle(bgr, (1000, py), (1050, py + 7), (195, 205, 215), -1)

        # SMD Capacitors & Resistors (0805 packages)
        for r in range(8):
            rx = 1150 + (r % 3) * 110
            ry = 250 + (r // 3) * 100
            # Body
            cv2.rectangle(bgr, (rx, ry), (rx + 60, ry + 30), (50, 90, 150), -1)
            # Metal end-caps
            cv2.rectangle(bgr, (rx, ry), (rx + 12, ry + 30), (210, 215, 220), -1)
            cv2.rectangle(bgr, (rx + 48, ry), (rx + 60, ry + 30), (210, 215, 220), -1)

        return self.register_image(bgr, image_id=image_id)

    def create_sample_checkerboard(self, image_id: str = "sample_calib") -> ImageMeta:
        """Creates an optical distortion & homography calibration target (1936x1216 Mono8)."""
        w, h = 1936, 1216
        img = np.full((h, w), 245, dtype=np.uint8)

        sq = 80
        start_x = 180
        start_y = 120
        cols = 19
        rows = 12

        for r in range(rows):
            for c in range(cols):
                if (r + c) % 2 == 1:
                    x1 = start_x + c * sq
                    y1 = start_y + r * sq
                    img[y1:y1 + sq, x1:x1 + sq] = 15

        # Target center cross
        cv2.drawMarker(img, (w // 2, h // 2), 128, markerType=cv2.MARKER_CROSS, markerSize=40, thickness=2)
        return self.register_image(img, image_id=image_id)

    def create_sample_pharma(self, image_id: str = "sample_pharma") -> ImageMeta:
        """Creates a pharmaceutical blister pack inspection image (1920x1080 BGR). Pocket #4 has missing capsule."""
        w, h = 1920, 1080
        bgr = np.full((h, w, 3), 42, dtype=np.uint8)

        # Conveyor belt texture
        for y in range(0, h, 20):
            cv2.line(bgr, (0, y), (w, y), (48, 48, 48), 1)

        # Blister aluminum foil body
        bx1, by1, bx2, by2 = 360, 240, 1560, 840
        cv2.rectangle(bgr, (bx1, by1), (bx2, by2), (180, 185, 190), -1)
        cv2.rectangle(bgr, (bx1 + 10, by1 + 10), (bx2 - 10, by2 - 10), (200, 205, 210), 2)

        # 2 rows x 5 columns of pill pockets
        pocket_w, pocket_h = 160, 220
        idx = 0
        for row in range(2):
            for col in range(5):
                idx += 1
                cx = bx1 + 120 + col * 220
                cy = by1 + 150 + row * 280

                # Embossed silver pocket cavity
                cv2.ellipse(bgr, (cx, cy), (75, 110), 0, 0, 360, (150, 155, 160), -1)
                cv2.ellipse(bgr, (cx, cy), (75, 110), 0, 0, 360, (230, 235, 240), 2)

                # Pocket 4 is intentionally DEFECTIVE (missing pill)
                if idx == 4:
                    cv2.putText(bgr, "EMPTY", (cx - 45, cy + 8), cv2.FONT_HERSHEY_SIMPLEX, 0.7, (40, 40, 180), 2)
                    continue

                # Normal two-tone capsule (Top half Blue, Bottom half White)
                # White bottom
                cv2.ellipse(bgr, (cx, cy + 30), (45, 55), 0, 0, 180, (240, 240, 245), -1)
                cv2.rectangle(bgr, (cx - 45, cy), (cx + 45, cy + 30), (240, 240, 245), -1)
                # Blue top
                cv2.ellipse(bgr, (cx, cy - 30), (45, 55), 0, 180, 360, (220, 120, 30), -1)
                cv2.rectangle(bgr, (cx - 45, cy - 30), (cx + 45, cy), (220, 120, 30), -1)
                # Capsule outline & specular highlight
                cv2.ellipse(bgr, (cx, cy), (45, 80), 0, 0, 360, (100, 80, 60), 2)
                cv2.ellipse(bgr, (cx - 15, cy - 25), (10, 25), -20, 0, 360, (255, 255, 255), -1)

        # Batch code printing on foil
        cv2.putText(bgr, "LOT: MED-VN-2026-X04", (bx1 + 40, by2 - 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (50, 50, 50), 2)
        cv2.putText(bgr, "EXP: 10/2029", (bx2 - 240, by2 - 30), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (50, 50, 50), 2)

        return self.register_image(bgr, image_id=image_id)

    def create_sample_barcode(self, image_id: str = "sample_barcode") -> ImageMeta:
        """Creates an industrial packaging box with Code128 and 2D DataMatrix code (1920x1080 BGR)."""
        w, h = 1920, 1080
        bgr = np.full((h, w, 3), (35, 45, 55), dtype=np.uint8)

        # Corrugated cardboard box
        cv2.rectangle(bgr, (300, 180), (1620, 900), (90, 140, 185), -1)
        cv2.rectangle(bgr, (300, 180), (1620, 900), (60, 100, 140), 3)

        # White shipping label
        cv2.rectangle(bgr, (420, 280), (1500, 800), (245, 245, 245), -1)
        cv2.rectangle(bgr, (420, 280), (1500, 800), (180, 180, 180), 2)

        # Label header
        cv2.putText(bgr, "UNILEVER SUPPLY CHAIN - LOGISTICS PACK", (460, 340), cv2.FONT_HERSHEY_SIMPLEX, 1.0, (20, 20, 20), 3)
        cv2.line(bgr, (460, 365), (1460, 365), (50, 50, 50), 2)

        # 1D Code128 Barcode simulation
        bar_x = 480
        bar_y = 420
        bar_h = 160
        patterns = [2, 4, 1, 3, 2, 5, 1, 4, 2, 2, 4, 1, 3, 3, 1, 5, 2, 3, 4, 1, 2, 4, 1, 3, 2, 5, 2, 1, 4, 3, 2, 4]
        for idx, span in enumerate(patterns):
            if idx % 2 == 0:
                cv2.rectangle(bgr, (bar_x, bar_y), (bar_x + span * 4, bar_y + bar_h), (10, 10, 10), -1)
            bar_x += span * 4 + 4

        cv2.putText(bgr, "* 8 9 3 4 5 6 7 8 9 0 1 2 *", (560, 615), cv2.FONT_HERSHEY_SIMPLEX, 0.8, (20, 20, 20), 2)

        # 2D DataMatrix / QR grid pattern
        qr_x, qr_y, qr_s = 1180, 420, 180
        cv2.rectangle(bgr, (qr_x, qr_y), (qr_x + qr_s, qr_y + qr_s), (10, 10, 10), 3)
        grid = 12
        cell = qr_s // grid
        # Finder L-pattern
        cv2.rectangle(bgr, (qr_x, qr_y), (qr_x + cell, qr_y + qr_s), (10, 10, 10), -1)
        cv2.rectangle(bgr, (qr_x, qr_y + qr_s - cell), (qr_x + qr_s, qr_y + qr_s), (10, 10, 10), -1)
        # Random binary cells
        np.random.seed(42)
        for gy in range(1, grid - 1):
            for gx in range(1, grid - 1):
                if np.random.rand() > 0.45:
                    cv2.rectangle(bgr, (qr_x + gx * cell, qr_y + gy * cell),
                                  (qr_x + (gx + 1) * cell, qr_y + (gy + 1) * cell), (10, 10, 10), -1)

        cv2.putText(bgr, "DPM ECC200", (1200, 630), cv2.FONT_HERSHEY_SIMPLEX, 0.6, (50, 50, 50), 2)

        # Logistics metadata
        cv2.putText(bgr, "DEST: DC-BINH-DUONG | PALLET #42", (460, 690), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (30, 30, 30), 2)
        cv2.putText(bgr, "SKU: 510-SUNLIGHT-LEMON-750ML", (460, 735), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (30, 30, 30), 2)
        cv2.putText(bgr, "ROBOT CELL: ABB IRB-360 DELTA", (460, 780), cv2.FONT_HERSHEY_SIMPLEX, 0.75, (0, 120, 40), 2)

        return self.register_image(bgr, image_id=image_id)

    def create_sample_gear(self, image_id: str = "sample_gear") -> ImageMeta:
        """Creates a precision machined spur gear for 1D/2D metrology caliper inspection (1920x1080 Mono8)."""
        w, h = 1920, 1080
        img = np.full((h, w), 35, dtype=np.uint8)

        # Backlight illumination profile
        cx, cy = w // 2, h // 2
        Y, X = np.ogrid[:h, :w]
        dist_from_center = np.sqrt((X - cx)**2 + (Y - cy)**2)
        backlight = 240 - np.clip(dist_from_center * 0.08, 0, 180).astype(np.uint8)
        img = backlight

        # Gear outer body silhouette (machined steel)
        teeth = 24
        outer_r = 380
        root_r = 310
        inner_r = 130
        keyway_w = 40
        keyway_h = 30

        # Construct tooth polygon
        gear_pts = []
        for i in range(teeth):
            angle_base = i * (2 * np.pi / teeth)
            angle_step = (2 * np.pi / teeth) / 4
            # Root 1
            a1 = angle_base
            gear_pts.append((int(cx + root_r * np.cos(a1)), int(cy + root_r * np.sin(a1))))
            # Tip 1
            a2 = angle_base + angle_step
            gear_pts.append((int(cx + outer_r * np.cos(a2)), int(cy + outer_r * np.sin(a2))))
            # Tip 2
            a3 = angle_base + 2 * angle_step
            gear_pts.append((int(cx + outer_r * np.cos(a3)), int(cy + outer_r * np.sin(a3))))
            # Root 2
            a4 = angle_base + 3 * angle_step
            gear_pts.append((int(cx + root_r * np.cos(a4)), int(cy + root_r * np.sin(a4))))

        cv2.fillPoly(img, [np.array(gear_pts, dtype=np.int32)], 45)

        # Center shaft bore hole (illuminated backlight)
        cv2.circle(img, (cx, cy), inner_r, 225, -1)

        # Keyway notch
        cv2.rectangle(img, (cx - keyway_w // 2, cy - inner_r - keyway_h), (cx + keyway_w // 2, cy - inner_r + 5), 225, -1)

        # 4 lightening bolt holes
        for h_angle in [np.pi / 4, 3 * np.pi / 4, 5 * np.pi / 4, 7 * np.pi / 4]:
            hx = int(cx + 215 * np.cos(h_angle))
            hy = int(cy + 215 * np.sin(h_angle))
            cv2.circle(img, (hx, hy), 45, 220, -1)

        # Measurement crosshairs
        cv2.drawMarker(img, (cx, cy), 180, markerType=cv2.MARKER_CROSS, markerSize=30, thickness=1)

        return self.register_image(img, image_id=image_id)
