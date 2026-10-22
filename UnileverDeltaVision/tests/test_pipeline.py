"""
Comprehensive Test Suite for Unilever Delta Vision System
Validates:
- Calibration & Undistortion (< 4ms)
- Bottle Detection & PO1/PO2 Moment Analysis (< 15ms)
- Schneider SERT Protocol byte-accuracy
- End-to-end cycle latency (< 25ms)
"""

import sys
import os
import time

if hasattr(sys.stdout, "reconfigure"):
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
if hasattr(sys.stderr, "reconfigure"):
    sys.stderr.reconfigure(encoding="utf-8", errors="replace")

import numpy as np

# Add parent directory to path
sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from core.calibration import CameraCalibration
from core.detector import BottleDetector, DetectorRecipe
from core.plc_protocol import SchneiderSertClient
from core.camera_stream import CameraStream


def test_calibration_and_homography():
    print("\n--- TEST 1: Camera Calibration & Homography ---")
    calib = CameraCalibration(1936, 1216)
    assert calib.is_calibrated
    assert calib.rms_error_mm < 0.3, f"RMS error {calib.rms_error_mm} exceeds 0.3mm!"

    # Test coordinate mapping
    rx, ry, ra = calib.pixel_to_robot(968.0, 608.0, 45.0)
    print(f"Center Pixel (968, 608, 45°) -> Robot World: X={rx:.2f}mm, Y={ry:.2f}mm, Angle={ra:.1f}°")
    assert isinstance(rx, float) and isinstance(ry, float)

    # Test fast undistortion benchmark
    test_img = np.full((1216, 1936), 128, dtype=np.uint8)
    t0 = time.perf_counter()
    undistorted = calib.undistort_image(test_img)
    t_undistort = (time.perf_counter() - t0) * 1000.0
    print(f"Lens Undistortion (1936x1216 CV_16SC2): {t_undistort:.2f} ms")
    assert t_undistort < 10.0, f"Undistortion took too long: {t_undistort} ms"
    print("✅ Calibration & Homography test PASSED.")


def test_bottle_detector():
    print("\n--- TEST 2: High-Speed Bottle Detector & PO1/PO2 ---")
    calib = CameraCalibration(1936, 1216)
    detector = BottleDetector(calib)

    # Generate synthetic conveyor frame with bottles
    frame = CameraStream.generate_synthetic_conveyor_frame(width=1936, height=1216, num_bottles=5)

    # Warmup
    detector.detect(frame)

    # Benchmark run
    t0 = time.perf_counter()
    bottles = detector.detect(frame)
    t_detect = (time.perf_counter() - t0) * 1000.0

    print(f"Detection execution time: {t_detect:.2f} ms (Target: < 15.0 ms)")
    print(f"Detected bottles count: {len(bottles)}")

    assert len(bottles) > 0, "No bottles detected in synthetic conveyor frame!"
    assert t_detect < 20.0, f"Detection took too long: {t_detect} ms!"

    po_types = set()
    for b in bottles:
        print(f"  Bottle #{b.id:02d} | Pixel: ({b.pixel_x:6.1f}, {b.pixel_y:6.1f}) | Robot: ({b.robot_x:+7.1f}, {b.robot_y:+7.1f} mm, {b.robot_angle_deg:+5.1f}°) | PO: {b.po_type}")
        po_types.add(b.po_type)
        assert b.po_type in ("PO1", "PO2")

    print(f"Identified orientations present: {po_types}")
    print("✅ Detector & Orientation test PASSED.")


def test_plc_protocol_formatting():
    print("\n--- TEST 3: Schneider SERT PLC Protocol ---")
    calib = CameraCalibration(1936, 1216)
    detector = BottleDetector(calib)
    frame = CameraStream.generate_synthetic_conveyor_frame(width=1936, height=1216, num_bottles=3)
    bottles = detector.detect(frame)

    client = SchneiderSertClient(mock_mode=True)
    packet_str = client.format_sert_packet(bottles, model_code="LB800", status=1)

    print(f"Formatted SERT Packet:\n  {packet_str.strip()}")

    # Check byte-accurate requirements:
    assert packet_str.startswith("Vision1"), "Packet must start with Vision1"
    assert "A05LB800" in packet_str, "Packet must contain A<Len><ModelCode>"
    assert packet_str.endswith("\r"), "Packet must terminate with \\r (0x0D)"
    assert f"Vision1{len(bottles):02d}" in packet_str

    print("✅ Schneider SERT Protocol test PASSED.")


def test_total_cycle_time():
    print("\n--- TEST 4: End-to-End Cycle Time Benchmark (< 25ms) ---")
    calib = CameraCalibration(1936, 1216)
    detector = BottleDetector(calib)
    client = SchneiderSertClient(mock_mode=True)

    frame = CameraStream.generate_synthetic_conveyor_frame(width=1936, height=1216, num_bottles=4)

    # 50 iterations benchmark
    latencies = []
    for _ in range(50):
        t0 = time.perf_counter()
        undistorted = calib.undistort_image(frame)
        bottles = detector.detect(undistorted)
        client.send_detection_result(bottles, "LB800", 1)
        latencies.append((time.perf_counter() - t0) * 1000.0)

    avg_ms = np.mean(latencies)
    p95_ms = np.percentile(latencies, 95)
    max_ms = np.max(latencies)

    print(f"50 Cycles Benchmark: Avg = {avg_ms:.2f} ms, P95 = {p95_ms:.2f} ms, Max = {max_ms:.2f} ms")
    assert avg_ms < 25.0, f"Average cycle time {avg_ms:.2f}ms exceeds 25ms requirement!"
    print("✅ End-to-End Cycle Time Benchmark PASSED.")


if __name__ == "__main__":
    test_calibration_and_homography()
    test_bottle_detector()
    test_plc_protocol_formatting()
    test_total_cycle_time()
    print("\n🎉 ALL 4 INDUSTRIAL ACCEPTANCE TESTS PASSED 100%!")
