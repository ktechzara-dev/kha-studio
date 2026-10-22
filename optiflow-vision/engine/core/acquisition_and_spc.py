"""
OptiFlow Vision Studio - Industrial Acquisition & Statistical Process Control (SPC)
Milestone 7 Implementation:
- Pluggable Camera Adapter Abstraction (Synthetic, GenICam Mock, Filmstrip Directory)
- Statistical Process Control (SPC): Cp, Cpk, Mean, StdDev, Western Electric Rule Alarms
"""

import os
import math
import glob
from abc import ABC, abstractmethod
from typing import List, Dict, Any, Optional, Tuple
import numpy as np
import cv2


# ==============================================================================
# 1. INDUSTRIAL CAMERA ADAPTER INTERFACES
# ==============================================================================

class CameraAdapterBase(ABC):
    """
    Abstract interface for industrial vision cameras (GigE Vision, USB3 Vision, GenICam).
    """
    @abstractmethod
    def connect(self) -> bool:
        pass

    @abstractmethod
    def disconnect(self) -> None:
        pass

    @abstractmethod
    def is_connected(self) -> bool:
        pass

    @abstractmethod
    def grab_frame(self, timeout_ms: int = 1000) -> Optional[np.ndarray]:
        pass

    @abstractmethod
    def set_exposure_us(self, exposure_us: float) -> bool:
        pass

    @abstractmethod
    def set_gain_db(self, gain_db: float) -> bool:
        pass


class SyntheticCameraAdapter(CameraAdapterBase):
    """
    Procedural camera simulator producing realistic conveyor bottle/part inspection frames
    with motion displacement, illumination noise, and dimensional variances.
    """
    def __init__(self, width: int = 1280, height: int = 960):
        self.width = width
        self.height = height
        self._connected = False
        self.exposure_us = 5000.0
        self.gain_db = 0.0
        self.frame_id = 0

    def connect(self) -> bool:
        self._connected = True
        return True

    def disconnect(self) -> None:
        self._connected = False

    def is_connected(self) -> bool:
        return self._connected

    def set_exposure_us(self, exposure_us: float) -> bool:
        self.exposure_us = max(100.0, min(50000.0, float(exposure_us)))
        return True

    def set_gain_db(self, gain_db: float) -> bool:
        self.gain_db = max(0.0, min(24.0, float(gain_db)))
        return True

    def grab_frame(self, timeout_ms: int = 1000) -> Optional[np.ndarray]:
        if not self._connected:
            return None

        self.frame_id += 1
        # Create dark conveyor canvas
        frame = np.ones((self.height, self.width, 3), dtype=np.uint8) * 35

        # Conveyor belt track markings
        cv2.line(frame, (0, 150), (self.width, 150), (60, 60, 60), 2)
        cv2.line(frame, (0, self.height - 150), (self.width, self.height - 150), (60, 60, 60), 2)

        # Part moves with frame_id
        shift_x = (self.frame_id * 35) % (self.width - 250) + 120
        center_y = self.height // 2

        # Bottle body
        cv2.rectangle(frame, (shift_x - 60, center_y - 120), (shift_x + 60, center_y + 120), (180, 180, 180), -1)
        # Cap
        cv2.rectangle(frame, (shift_x - 25, center_y - 160), (shift_x + 25, center_y - 120), (0, 200, 230), -1)
        # Liquid fill level
        cv2.rectangle(frame, (shift_x - 55, center_y - 80), (shift_x + 55, center_y + 115), (20, 160, 60), -1)

        # Add simulated sensor noise
        noise = np.random.normal(0, 3 + self.gain_db * 0.5, frame.shape).astype(np.int16)
        noisy = np.clip(frame.astype(np.int16) + noise, 0, 255).astype(np.uint8)
        return noisy


class DirectoryFilmstripAdapter(CameraAdapterBase):
    """
    Loads image sequences from local directory filmstrip folder for automated testing.
    """
    def __init__(self, directory_path: str):
        self.directory_path = directory_path
        self._connected = False
        self.image_files: List[str] = []
        self.current_index = 0

    def connect(self) -> bool:
        if os.path.isdir(self.directory_path):
            exts = ("*.jpg", "*.png", "*.bmp", "*.jpeg")
            files = []
            for ext in exts:
                files.extend(glob.glob(os.path.join(self.directory_path, ext)))
            self.image_files = sorted(files)
        self._connected = True
        return True

    def disconnect(self) -> None:
        self._connected = False

    def is_connected(self) -> bool:
        return self._connected

    def set_exposure_us(self, exposure_us: float) -> bool:
        return True

    def set_gain_db(self, gain_db: float) -> bool:
        return True

    def grab_frame(self, timeout_ms: int = 1000) -> Optional[np.ndarray]:
        if not self._connected or not self.image_files:
            return None
        file_path = self.image_files[self.current_index % len(self.image_files)]
        self.current_index += 1
        return cv2.imread(file_path)


# ==============================================================================
# 2. STATISTICAL PROCESS CONTROL (SPC) ENGINE
# ==============================================================================

class SPCStatisticsEngine:
    """
    Statistical Process Control (SPC) Quality Analyser.
    Tracks sample distribution, Cp, Cpk, standard deviation, and out-of-control alarms.
    """
    def __init__(
        self,
        nominal: float = 100.0,
        lsl: float = 95.0,
        usl: float = 105.0,
        max_samples: int = 200
    ):
        self.nominal = float(nominal)
        self.lsl = float(lsl)
        self.usl = float(usl)
        self.max_samples = max_samples
        self.samples: List[float] = []

    def add_sample(self, val: float) -> None:
        self.samples.append(float(val))
        if len(self.samples) > self.max_samples:
            self.samples.pop(0)

    def reset(self) -> None:
        self.samples.clear()

    def calculate_metrics(self) -> Dict[str, Any]:
        n = len(self.samples)
        if n < 3:
            return {
                "count": n,
                "mean": round(float(np.mean(self.samples)), 3) if n > 0 else self.nominal,
                "std_dev": 0.0,
                "cp": 0.0,
                "cpk": 0.0,
                "lsl": self.lsl,
                "usl": self.usl,
                "nominal": self.nominal,
                "in_control": True,
                "alarms": []
            }

        arr = np.array(self.samples, dtype=np.float64)
        mean_val = float(np.mean(arr))
        # Sample standard deviation (ddof=1)
        std_val = float(np.std(arr, ddof=1))

        if std_val < 1e-6:
            cp = 99.0
            cpk = 99.0
        else:
            cp = (self.usl - self.lsl) / (6.0 * std_val)
            cpu = (self.usl - mean_val) / (3.0 * std_val)
            cpl = (mean_val - self.lsl) / (3.0 * std_val)
            cpk = min(cpu, cpl)

        # Western Electric / Nelson Out-of-control rules:
        alarms = []
        # Rule 1: Point beyond 3-sigma
        latest = self.samples[-1]
        if std_val > 0.0 and abs(latest - mean_val) > 3.0 * std_val:
            alarms.append(f"OUT_OF_3SIGMA: Latest measurement ({latest:.2f}) exceeds 3-sigma control limit")

        # Rule 2: Point outside engineering spec LSL..USL
        if latest < self.lsl or latest > self.usl:
            alarms.append(f"SPEC_BREACH: Measurement ({latest:.2f}) outside tolerance [{self.lsl}, {self.usl}]")

        # Rule 3: 8 consecutive points on one side of center line (mean)
        if n >= 8:
            last_8 = self.samples[-8:]
            if all(x > mean_val for x in last_8) or all(x < mean_val for x in last_8):
                alarms.append("RUN_TREND: 8 consecutive points on one side of mean")

        # Histogram distribution: 10 bins
        hist_counts, bin_edges = np.histogram(arr, bins=10)
        hist_bins = [
            {"bin_start": round(float(bin_edges[i]), 2), "bin_end": round(float(bin_edges[i+1]), 2), "count": int(hist_counts[i])}
            for i in range(len(hist_counts))
        ]

        return {
            "count": n,
            "mean": round(mean_val, 3),
            "std_dev": round(std_val, 3),
            "cp": round(max(0.0, cp), 2),
            "cpk": round(max(0.0, cpk), 2),
            "lsl": self.lsl,
            "usl": self.usl,
            "nominal": self.nominal,
            "in_control": len(alarms) == 0,
            "alarms": alarms,
            "histogram": hist_bins
        }
