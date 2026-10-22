"""
Headless Production Service for Unilever Delta Vision Line
Runs high-speed vision pipeline continuously without GUI (Industrial Edge Daemon).
"""

import os
import sys
import time
import json
import signal
import logging
import argparse

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import numpy as np

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, BASE_DIR)

from core.calibration import CameraCalibration
from core.detector import BottleDetector, DetectorRecipe
from core.plc_protocol import SchneiderSertClient
from core.camera_stream import CameraStream

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] (%(threadName)s) %(message)s",
    datefmt="%Y-%m-%d %H:%M:%S"
)
logger = logging.getLogger("UnileverVision.Service")


class UnileverVisionService:
    def __init__(self, config_file: str = "config/system.json"):
        if not os.path.isabs(config_file):
            self.config_file = os.path.join(BASE_DIR, config_file)
        else:
            self.config_file = config_file
        self.is_running = False

        self.calibration = CameraCalibration()
        self.detector = BottleDetector(self.calibration)
        self.plc_client: SchneiderSertClient | None = None
        self.camera: CameraStream | None = None

        self.total_frames = 0
        self.total_bottles = 0
        self.start_time = 0.0

        self._load_configuration()

    def _load_configuration(self):
        logger.info(f"Loading configuration from {self.config_file}...")
        calib_file = os.path.join(BASE_DIR, "config", "calibration_extracted.json")
        recipe_file = os.path.join(BASE_DIR, "config", "recipes", "model_16_lb800.json")
        plc_host = "190.201.100.100"
        plc_port = 50001
        mock_mode = False
        target_fps = 40.0

        if os.path.exists(self.config_file):
            try:
                with open(self.config_file, "r", encoding="utf-8") as f:
                    cfg = json.load(f)

                plc_cfg = cfg.get("plc", {})
                plc_host = plc_cfg.get("host", plc_host)
                plc_port = int(plc_cfg.get("port", plc_port))
                mock_mode = bool(plc_cfg.get("mock_mode", mock_mode))

                cam_cfg = cfg.get("camera", {})
                target_fps = float(cam_cfg.get("target_fps", target_fps))

                calib_file_cfg = cfg.get("calibration_file")
                if calib_file_cfg:
                    calib_file = os.path.join(BASE_DIR, calib_file_cfg) if not os.path.isabs(calib_file_cfg) else calib_file_cfg

                recipe_name = cfg.get("active_recipe", "model_16_lb800.json")
                recipe_file = os.path.join(BASE_DIR, "config", "recipes", recipe_name)
            except Exception as ex:
                logger.error(f"Error parsing system configuration: {ex}")

        # Load Calibration
        self.calibration.load_from_json(calib_file)

        # Load Recipe
        if os.path.exists(recipe_file):
            try:
                with open(recipe_file, "r", encoding="utf-8") as f:
                    r_dict = json.load(f)
                self.detector.recipe = DetectorRecipe.from_dict(r_dict)
                logger.info(f"Loaded recipe: {self.detector.recipe.name} (Code: {self.detector.recipe.model_code})")
            except Exception as ex:
                logger.error(f"Error loading recipe: {ex}")

        # Initialize PLC Client
        self.plc_client = SchneiderSertClient(
            host=plc_host,
            port=plc_port,
            mock_mode=mock_mode
        )

        # Initialize Camera Stream
        self.camera = CameraStream(
            image_width=self.calibration.image_width,
            image_height=self.calibration.image_height,
            target_fps=target_fps
        )
        self.camera.on_frame_captured = self._on_frame_captured

    def start(self):
        self.is_running = True
        self.start_time = time.time()
        logger.info("=" * 65)
        logger.info("🚀 STARTING UNILEVER 3-DELTA ROBOT VISION HEADLESS SERVICE")
        logger.info(f"   Target SPP: 510 | Target Cycle: < 25ms | Camera: {self.calibration.image_width}x{self.calibration.image_height}")
        logger.info(f"   PLC Host: {self.plc_client.host}:{self.plc_client.port} | Mock={self.plc_client.mock_mode}")
        logger.info("=" * 65)

        self.plc_client.start()
        self.camera.start()

    def stop(self):
        logger.info("Stopping Unilever Vision Service...")
        self.is_running = False
        if self.camera:
            self.camera.stop()
        if self.plc_client:
            self.plc_client.stop()
        logger.info("Unilever Vision Service stopped cleanly.")

    def _on_frame_captured(self, frame: np.ndarray, frame_id: int, timestamp: float):
        if not self.is_running:
            return

        t0 = time.perf_counter()

        # Step 1: Lens Undistortion
        # Using precomputed maps CV_16SC2 (~3.2 ms)
        t_u0 = time.perf_counter()
        undistorted = self.calibration.undistort_image(frame)
        t_undistort = (time.perf_counter() - t_u0) * 1000.0

        # Step 2: High-Speed Detection & PO1/PO2 Classification (< 15 ms)
        bottles = self.detector.detect(undistorted)

        # Step 3: SERT PLC Encoding & TCP Transmission (< 1 ms)
        t_p0 = time.perf_counter()
        sent = self.plc_client.send_detection_result(
            bottles=bottles,
            model_code=self.detector.recipe.model_code,
            status=1
        )
        t_plc = (time.perf_counter() - t_p0) * 1000.0

        total_ms = (time.perf_counter() - t0) * 1000.0

        self.total_frames += 1
        self.total_bottles += len(bottles)

        # Periodic telemetry log (every 40 frames ~ 1 sec)
        if self.total_frames % 40 == 0:
            uptime = max(0.1, time.time() - self.start_time)
            fps = self.total_frames / uptime
            spp = (self.total_bottles / uptime) * 60.0
            status_symbol = "🟢" if total_ms < 25.0 else "🟡"
            logger.info(
                f"{status_symbol} Frame #{self.total_frames} | Cycle: {total_ms:5.2f}ms "
                f"[Undist: {t_undistort:4.2f}ms, Detect: {self.detector.last_execution_time_ms:5.2f}ms, PLC: {t_plc:4.2f}ms] | "
                f"Bottles: {len(bottles)} | Speed: {fps:4.1f} FPS ({spp:5.1f} SPP)"
            )


def main():
    parser = argparse.ArgumentParser(description="Unilever Delta Vision Headless Service")
    parser.add_argument("--config", default="config/system.json", help="Path to system config JSON")
    args = parser.parse_args()

    service = UnileverVisionService(config_file=args.config)

    def signal_handler(sig, frame):
        logger.info(f"Received signal {sig}, terminating...")
        service.stop()
        sys.exit(0)

    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    service.start()

    try:
        while service.is_running:
            time.sleep(1.0)
    except (KeyboardInterrupt, SystemExit):
        service.stop()


if __name__ == "__main__":
    main()
