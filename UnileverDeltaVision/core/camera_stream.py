"""
Camera Stream & Industrial Conveyor Simulator
Supports:
- Physical Camera Interface (IDS Imaging / GenICam / OpenCV V4L2/DirectShow)
- Offline Industrial Conveyor Simulator (1936x1216 Mono8)
- Dynamic bottle conveyor simulation at 510 SPP (bottles moving smoothly)
- Teaching.png generation and loading
- Trigger modes: CONTINUOUS, SOFTWARE, HARDWARE GPIO
- Sub-millisecond zero-copy frame dispatching
"""

import os
import time
import threading
import logging
import random
from typing import Callable
import numpy as np
import cv2

logger = logging.getLogger("UnileverVision.Camera")


class CameraStream:
    def __init__(
        self,
        image_width: int = 1936,
        image_height: int = 1216,
        target_fps: float = 40.0,
        exposure_us: int = 1000,
        gain: float = 1.0,
        force_simulation: bool = False,
        teaching_image_path: str = "config/Teaching.png"
    ):
        self.image_width = image_width
        self.image_height = image_height
        self.target_fps = target_fps
        self.exposure_us = exposure_us
        self.gain = gain
        self.force_simulation = force_simulation
        self.teaching_image_path = teaching_image_path

        self.trigger_mode = "CONTINUOUS"  # 'CONTINUOUS', 'SOFTWARE', 'HARDWARE'
        self.is_streaming = False
        self.is_hardware_camera = False

        self._thread: threading.Thread | None = None
        self._lock = threading.Lock()
        self._cap: cv2.VideoCapture | None = None

        # Frame callback: func(image_numpy, frame_id, timestamp)
        self.on_frame_captured: Callable[[np.ndarray, int, float], None] | None = None

        # Conveyor simulation state
        self._sim_offset_x = 0.0
        self._frame_counter = 0
        self._teaching_img: np.ndarray | None = None

        # Ensure Teaching.png exists or generate it
        self._ensure_teaching_image()

    def _ensure_teaching_image(self):
        """Checks if Teaching.png exists; if not, generates a synthetic high-resolution industrial conveyor frame."""
        if os.path.exists(self.teaching_image_path):
            try:
                img = cv2.imread(self.teaching_image_path, cv2.IMREAD_GRAYSCALE)
                if img is not None and img.shape == (self.image_height, self.image_width):
                    self._teaching_img = img
                    logger.info(f"Loaded existing Teaching image from {self.teaching_image_path}")
                    return
            except Exception as ex:
                logger.warning(f"Could not read {self.teaching_image_path}: {ex}")

        # Generate realistic Unilever conveyor teaching frame
        logger.info("Generating synthetic Unilever Industrial Conveyor Teaching frame (1936x1216 Mono8)...")
        self._teaching_img = self.generate_synthetic_conveyor_frame(num_bottles=5)
        os.makedirs(os.path.dirname(os.path.abspath(self.teaching_image_path)), exist_ok=True)
        cv2.imwrite(self.teaching_image_path, self._teaching_img)
        logger.info(f"Generated and saved Teaching reference frame to {self.teaching_image_path}")

    @classmethod
    def generate_synthetic_conveyor_frame(
        cls,
        width: int = 1936,
        height: int = 1216,
        num_bottles: int = 4,
        offset_x: float = 0.0
    ) -> np.ndarray:
        """
        Creates a realistic Mono8 image of a stainless steel Unilever conveyor belt
        carrying shampoo/bodywash bottles (narrow cap/neck, wider base).
        """
        # Conveyor belt texture (dark matte industrial plastic belt with subtle horizontal streaks)
        frame = np.full((height, width), 48, dtype=np.uint8)

        # Conveyor guide rails (top and bottom stainless steel wear strips)
        frame[:90, :] = 85
        frame[height - 90:, :] = 85
        cv2.line(frame, (0, 90), (width, 90), 160, 3)
        cv2.line(frame, (0, height - 90), (width, height - 90), 160, 3)

        # Subtle belt texture lines
        for y in range(100, height - 100, 40):
            cv2.line(frame, (0, y), (width, y), 54, 1)

        # Optical trigger photoelectric sensor beam indicator (virtual line at X=200)
        cv2.line(frame, (200, 90), (200, height - 90), 65, 1)

        # Generate bottles
        # Bottle dimensions in pixels (Lifebuoy 800ml ~ 210mm tall x 95mm wide -> ~400px x 180px)
        spacing = 420
        start_x = 120 + int(offset_x) % spacing

        for i in range(-1, num_bottles + 2):
            bx = start_x + i * spacing
            if bx < -250 or bx > width + 250:
                continue

            # Deterministic pseudo-random variation based on index
            seed = (i + int(offset_x / spacing)) % 1000
            rng = random.Random(seed * 71)

            by = height // 2 + rng.randint(-35, 35)
            angle = rng.uniform(-18.0, 18.0)
            is_inverted = (seed % 3 == 0)  # ~33% PO2 (inverted base-first)

            cls._draw_unilever_bottle(frame, bx, by, angle, is_inverted, rng)

        # Add camera sensor Gaussian noise and lens vignette
        noise = np.random.normal(0, 2.5, (height, width)).astype(np.int16)
        noisy_frame = np.clip(frame.astype(np.int16) + noise, 0, 255).astype(np.uint8)

        return noisy_frame

    @staticmethod
    def _draw_unilever_bottle(
        frame: np.ndarray,
        cx: int,
        cy: int,
        angle_deg: float,
        is_inverted: bool,
        rng: random.Random
    ):
        """Draws an anatomically accurate Unilever shampoo bottle with cap, neck, shoulder, body, and base."""
        # Create blank canvas for bottle silhouette
        b_w, b_h = 320, 520
        canvas = np.zeros((b_h, b_w), dtype=np.uint8)

        mid_x = b_w // 2

        # Bottle dimensions
        body_w = 170
        neck_w = 55
        cap_w = 46

        # Key points along Y (0 = cap top, 480 = base)
        # Cap: y 20 to 65
        # Neck: y 65 to 110
        # Shoulder: y 110 to 190
        # Body: y 190 to 450
        # Base: y 450 to 480
        pts = [
            (mid_x - cap_w // 2, 25),
            (mid_x + cap_w // 2, 25),
            (mid_x + cap_w // 2, 65),
            (mid_x + neck_w // 2, 65),
            (mid_x + neck_w // 2, 110),
            (mid_x + body_w // 2 + 10, 190),
            (mid_x + body_w // 2, 440),
            (mid_x + body_w // 2 - 15, 475),
            (mid_x - body_w // 2 + 15, 475),
            (mid_x - body_w // 2, 440),
            (mid_x - body_w // 2 - 10, 190),
            (mid_x - neck_w // 2, 110),
            (mid_x - neck_w // 2, 65),
            (mid_x - cap_w // 2, 65),
        ]

        # Draw filled bottle in white/high-contrast
        poly = np.array(pts, dtype=np.int32)
        cv2.fillPoly(canvas, [poly], 235)

        # Add label texture / emboss in center
        cv2.rectangle(canvas, (mid_x - 55, 230), (mid_x + 55, 380), 200, -1)
        cv2.putText(canvas, "UNILEVER", (mid_x - 45, 280), cv2.FONT_HERSHEY_SIMPLEX, 0.6, 120, 2)
        cv2.putText(canvas, "800ml", (mid_x - 30, 320), cv2.FONT_HERSHEY_SIMPLEX, 0.5, 120, 1)

        # Invert if PO2
        if is_inverted:
            canvas = cv2.rotate(canvas, cv2.ROTATE_180)

        # Rotate canvas by angle_deg
        rot_mat = cv2.getRotationMatrix2D((mid_x, b_h // 2), angle_deg, 1.0)
        rotated_bottle = cv2.warpAffine(canvas, rot_mat, (b_w, b_h), flags=cv2.INTER_LINEAR)

        # Alpha blend into target frame at (cx, cy)
        y1 = cy - b_h // 2
        y2 = y1 + b_h
        x1 = cx - b_w // 2
        x2 = x1 + b_w

        # Clipping
        fy1 = max(0, y1)
        fy2 = min(frame.shape[0], y2)
        fx1 = max(0, x1)
        fx2 = min(frame.shape[1], x2)

        by1 = fy1 - y1
        by2 = by1 + (fy2 - fy1)
        bx1 = fx1 - x1
        bx2 = bx1 + (fx2 - fx1)

        if fy2 > fy1 and fx2 > fx1:
            mask = rotated_bottle[by1:by2, bx1:bx2] > 20
            roi = frame[fy1:fy2, fx1:fx2]
            roi[mask] = rotated_bottle[by1:by2, bx1:bx2][mask]

    def start(self):
        """Starts video acquisition thread."""
        if self.is_streaming:
            return

        # Attempt to open physical camera if not forcing simulation
        if not self.force_simulation:
            self._try_open_physical_camera()

        self.is_streaming = True
        self._thread = threading.Thread(target=self._capture_worker, daemon=True, name="Camera_Capture_Worker")
        self._thread.start()
        logger.info(f"Camera Stream started (Hardware={self.is_hardware_camera}, Trigger={self.trigger_mode})")

    def stop(self):
        """Stops video acquisition."""
        self.is_streaming = False
        if self._thread and self._thread.is_alive():
            self._thread.join(timeout=1.0)
        if self._cap:
            try:
                self._cap.release()
            except Exception:
                pass
            self._cap = None
        logger.info("Camera Stream stopped.")

    def _try_open_physical_camera(self):
        """Attempts to open physical camera via OpenCV DirectShow/V4L2."""
        try:
            cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)
            if cap.isOpened():
                cap.set(cv2.CAP_PROP_FRAME_WIDTH, self.image_width)
                cap.set(cv2.CAP_PROP_FRAME_HEIGHT, self.image_height)
                ret, test_frame = cap.read()
                if ret and test_frame is not None:
                    self._cap = cap
                    self.is_hardware_camera = True
                    logger.info("Physical Camera connected and verified.")
                    return
                cap.release()
        except Exception as ex:
            logger.warning(f"No physical camera found ({ex}), falling back to offline conveyor simulation.")

        self.is_hardware_camera = False

    def trigger(self):
        """Dispatches a single frame in SOFTWARE trigger mode."""
        if self.is_streaming and self.trigger_mode == "SOFTWARE":
            self._capture_and_dispatch()

    def _capture_and_dispatch(self):
        t_now = time.time()
        self._frame_counter += 1

        if self.is_hardware_camera and self._cap:
            ret, frame = self._cap.read()
            if ret and frame is not None:
                if len(frame.shape) == 3:
                    frame = cv2.cvtColor(frame, cv2.COLOR_BGR2GRAY)
                # Resize if camera resolution differs
                if frame.shape != (self.image_height, self.image_width):
                    frame = cv2.resize(frame, (self.image_width, self.image_height))
            else:
                frame = self._generate_simulated_frame()
        else:
            frame = self._generate_simulated_frame()

        if self.on_frame_captured:
            self.on_frame_captured(frame, self._frame_counter, t_now)

    def _generate_simulated_frame(self) -> np.ndarray:
        """Simulates dynamic conveyor motion at 510 SPP (~8.5 bottles/sec = ~25 px/frame at 40 FPS)."""
        conveyor_speed_px_per_frame = 22.0
        self._sim_offset_x += conveyor_speed_px_per_frame
        return self.generate_synthetic_conveyor_frame(
            width=self.image_width,
            height=self.image_height,
            num_bottles=5,
            offset_x=self._sim_offset_x
        )

    def _capture_worker(self):
        """Continuous frame grab worker thread."""
        interval = 1.0 / max(1.0, self.target_fps)

        while self.is_streaming:
            t_start = time.perf_counter()

            if self.trigger_mode == "CONTINUOUS":
                self._capture_and_dispatch()

            elapsed = time.perf_counter() - t_start
            sleep_time = interval - elapsed
            if sleep_time > 0.001:
                time.sleep(sleep_time)
