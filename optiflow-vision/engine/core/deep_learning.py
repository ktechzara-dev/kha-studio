"""
OptiFlow Vision Studio - Milestone 10 Deep Learning & ONNX Inference
Provides production-grade ONNX Runtime inference, defect classification,
YOLO-based object detection, and anomaly scoring with graceful mock fallbacks.
"""

from __future__ import annotations
import os
import cv2
import numpy as np
from typing import Dict, Any, List, Optional, Tuple

try:
    import onnxruntime as ort
    HAS_ONNXRUNTIME = True
except ImportError:
    ort = None
    HAS_ONNXRUNTIME = False


def preprocess_image_tensor(
    image: np.ndarray,
    target_size: Tuple[int, int] = (640, 640),
    mean: Tuple[float, float, float] = (0.0, 0.0, 0.0),
    std: Tuple[float, float, float] = (1.0, 1.0, 1.0),
    normalize_0_1: bool = True,
    channel_first: bool = True
) -> Tuple[np.ndarray, float, Tuple[int, int]]:
    """
    Letterbox resizing and tensor formatting for neural network inference.
    Returns:
        tensor: np.ndarray shaped (1, C, H, W) or (1, H, W, C), float32
        scale: float scaling ratio applied
        pad: (pad_x, pad_y) offsets added
    """
    if image is None:
        raise ValueError("Input image cannot be None")

    if len(image.shape) == 2:
        img_rgb = cv2.cvtColor(image, cv2.COLOR_GRAY2RGB)
    elif image.shape[2] == 4:
        img_rgb = cv2.cvtColor(image, cv2.COLOR_BGRA2RGB)
    else:
        img_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)

    orig_h, orig_w = img_rgb.shape[:2]
    target_w, target_h = target_size

    # Calculate letterbox scale
    scale = min(target_w / orig_w, target_h / orig_h)
    new_w, new_h = int(orig_w * scale), int(orig_h * scale)
    resized = cv2.resize(img_rgb, (new_w, new_h), interpolation=cv2.INTER_LINEAR)

    pad_w = target_w - new_w
    pad_h = target_h - new_h
    pad_left = pad_w // 2
    pad_right = pad_w - pad_left
    pad_top = pad_h // 2
    pad_bottom = pad_h - pad_top

    padded = cv2.copyMakeBorder(
        resized, pad_top, pad_bottom, pad_left, pad_right,
        cv2.BORDER_CONSTANT, value=(114, 114, 114)
    )

    tensor = padded.astype(np.float32)
    if normalize_0_1:
        tensor /= 255.0

    mean_arr = np.array(mean, dtype=np.float32)
    std_arr = np.array(std, dtype=np.float32)
    tensor = (tensor - mean_arr) / (std_arr + 1e-7)

    if channel_first:
        tensor = np.transpose(tensor, (2, 0, 1))  # (C, H, W)

    tensor = np.expand_dims(tensor, axis=0)  # (1, C, H, W)
    return tensor, scale, (pad_left, pad_top)


def non_max_suppression(
    boxes: np.ndarray,
    scores: np.ndarray,
    iou_threshold: float = 0.45
) -> List[int]:
    """
    Classic vectorized Non-Maximum Suppression (NMS) for axis-aligned bounding boxes.
    boxes: np.ndarray shaped (N, 4) in format [x1, y1, x2, y2]
    scores: np.ndarray shaped (N,)
    """
    if len(boxes) == 0:
        return []

    x1 = boxes[:, 0]
    y1 = boxes[:, 1]
    x2 = boxes[:, 2]
    y2 = boxes[:, 3]

    areas = np.maximum(0.0, x2 - x1) * np.maximum(0.0, y2 - y1)
    order = scores.argsort()[::-1]

    keep: List[int] = []
    while order.size > 0:
        i = order[0]
        keep.append(int(i))
        if order.size == 1:
            break

        xx1 = np.maximum(x1[i], x1[order[1:]])
        yy1 = np.maximum(y1[i], y1[order[1:]])
        xx2 = np.minimum(x2[i], x2[order[1:]])
        yy2 = np.minimum(y2[i], y2[order[1:]])

        w = np.maximum(0.0, xx2 - xx1)
        h = np.maximum(0.0, yy2 - yy1)
        inter = w * h

        union = areas[i] + areas[order[1:]] - inter
        iou = inter / np.maximum(union, 1e-7)

        inds = np.where(iou <= iou_threshold)[0]
        order = order[inds + 1]

    return keep


class ONNXInferenceSession:
    """
    Wrapper for ONNX Runtime InferenceSession with fallback mock inference support.
    """
    def __init__(self, model_path: str, providers: Optional[List[str]] = None):
        self.model_path = model_path
        self.providers = providers or ["CPUExecutionProvider"]
        self.session = None
        self.is_mock = False

        if HAS_ONNXRUNTIME and os.path.exists(model_path):
            try:
                self.session = ort.InferenceSession(model_path, providers=self.providers)
                self.input_names = [inp.name for inp in self.session.get_inputs()]
                self.output_names = [out.name for out in self.session.get_outputs()]
            except Exception as e:
                self.is_mock = True
        else:
            self.is_mock = True

    def run(self, input_feed: Dict[str, np.ndarray]) -> List[np.ndarray]:
        """Runs inference or simulated mock inference."""
        if not self.is_mock and self.session is not None:
            output_names = self.output_names
            return self.session.run(output_names, input_feed)

        # Mock inference behavior for testing/demo
        first_input = next(iter(input_feed.values()))
        batch_size = first_input.shape[0]

        if "yolo" in self.model_path.lower():
            # Mock YOLO output: shape (batch, 6, 8400) -> [cx, cy, w, h, class0, class1]
            num_classes = 2
            mock_preds = np.zeros((batch_size, 4 + num_classes, 100), dtype=np.float32)
            # Inject a realistic detected object
            mock_preds[:, 0, 0] = 320.0  # cx
            mock_preds[:, 1, 0] = 240.0  # cy
            mock_preds[:, 2, 0] = 120.0  # w
            mock_preds[:, 3, 0] = 80.0   # h
            mock_preds[:, 4, 0] = 0.92   # Class 0 confidence
            mock_preds[:, 5, 0] = 0.05   # Class 1 confidence
            return [mock_preds]

        elif "anomaly" in self.model_path.lower():
            # Mock Anomaly reconstruction: returns reconstructed tensor matching input
            return [first_input.copy()]

        else:
            # Default mock classification: 3 classes [Pass, DefectA, DefectB]
            logits = np.array([[0.88, 0.08, 0.04] for _ in range(batch_size)], dtype=np.float32)
            return [logits]


def postprocess_classification(
    logits_or_probs: np.ndarray,
    class_names: Optional[List[str]] = None
) -> Dict[str, Any]:
    """
    Postprocesses classification logits via softmax, identifying top class & confidence.
    """
    flat = logits_or_probs.flatten()
    # Safe softmax
    exp_logits = np.exp(flat - np.max(flat))
    probs = exp_logits / (np.sum(exp_logits) + 1e-7)

    top_idx = int(np.argmax(probs))
    confidence = float(probs[top_idx])

    if class_names and top_idx < len(class_names):
        top_label = class_names[top_idx]
    else:
        top_label = f"Class_{top_idx}"

    is_defective = ("defect" in top_label.lower() or "ng" in top_label.lower())

    probabilities = {
        (class_names[i] if class_names and i < len(class_names) else f"Class_{i}"): float(probs[i])
        for i in range(len(probs))
    }

    return {
        "label": top_label,
        "class_id": top_idx,
        "confidence": round(confidence, 4),
        "is_defective": is_defective,
        "probabilities": probabilities
    }


def postprocess_yolo_detections(
    predictions: np.ndarray,
    conf_threshold: float = 0.25,
    iou_threshold: float = 0.45,
    class_names: Optional[List[str]] = None,
    scale: float = 1.0,
    pad: Tuple[int, int] = (0, 0),
    orig_shape: Optional[Tuple[int, int]] = None
) -> List[Dict[str, Any]]:
    """
    Decodes standard YOLOv8/v11 output tensor shape (1, 4 + C, N) or (1, N, 4 + C).
    Maps bounding boxes back to original unpadded image coordinate space.
    """
    if len(predictions.shape) == 3:
        b, d1, d2 = predictions.shape
        # In YOLOv8/v11, shape is (batch, 4+C, N) where N (e.g. 8400) >> 4+C (e.g. 5 to 84)
        if d1 < d2:
            preds = np.transpose(predictions[0], (1, 0))  # (N, 4+C)
        else:
            preds = predictions[0]  # (N, 4+C)
    else:
        preds = predictions

    cx = preds[:, 0]
    cy = preds[:, 1]
    w = preds[:, 2]
    h = preds[:, 3]

    scores_matrix = preds[:, 4:]
    max_scores = np.max(scores_matrix, axis=1)
    class_ids = np.argmax(scores_matrix, axis=1)

    # Confidence mask
    mask = max_scores >= conf_threshold
    cx = cx[mask]
    cy = cy[mask]
    w = w[mask]
    h = h[mask]
    scores = max_scores[mask]
    class_ids = class_ids[mask]

    if len(scores) == 0:
        return []

    # Convert cx, cy, w, h to x1, y1, x2, y2 in target_size padded coords
    x1 = cx - w / 2.0
    y1 = cy - h / 2.0
    x2 = cx + w / 2.0
    y2 = cy + h / 2.0

    boxes_padded = np.column_stack([x1, y1, x2, y2])
    keep_indices = non_max_suppression(boxes_padded, scores, iou_threshold)

    results: List[Dict[str, Any]] = []
    pad_x, pad_y = pad

    for idx in keep_indices:
        b_x1 = (boxes_padded[idx, 0] - pad_x) / scale
        b_y1 = (boxes_padded[idx, 1] - pad_y) / scale
        b_x2 = (boxes_padded[idx, 2] - pad_x) / scale
        b_y2 = (boxes_padded[idx, 3] - pad_y) / scale

        # Clip to original image bounds if provided
        if orig_shape:
            orig_h, orig_w = orig_shape
            b_x1 = max(0.0, min(float(b_x1), float(orig_w - 1)))
            b_y1 = max(0.0, min(float(b_y1), float(orig_h - 1)))
            b_x2 = max(0.0, min(float(b_x2), float(orig_w - 1)))
            b_y2 = max(0.0, min(float(b_y2), float(orig_h - 1)))

        cid = int(class_ids[idx])
        cname = class_names[cid] if class_names and cid < len(class_names) else f"class_{cid}"

        results.append({
            "box": [round(float(b_x1), 1), round(float(b_y1), 1), round(float(b_x2), 1), round(float(b_y2), 1)],
            "class_id": cid,
            "class_name": cname,
            "confidence": round(float(scores[idx]), 4)
        })

    return results


def draw_yolo_detections_overlay(
    image: np.ndarray,
    detections: List[Dict[str, Any]]
) -> np.ndarray:
    """
    Renders high-contrast bounding boxes with class tags on the inspection image.
    """
    if image is None:
        return None
    overlay = image.copy()
    if len(overlay.shape) == 2:
        overlay = cv2.cvtColor(overlay, cv2.COLOR_GRAY2BGR)

    colors = [
        (0, 220, 110),   # Pass/Green
        (40, 60, 255),   # Defect/Red
        (255, 180, 0),   # Cyan/Orange
        (220, 50, 220)   # Purple
    ]

    for det in detections:
        box = det["box"]
        x1, y1, x2, y2 = [int(v) for v in box]
        cid = det.get("class_id", 0)
        cname = det.get("class_name", "object")
        conf = det.get("confidence", 0.0)

        color = colors[cid % len(colors)]
        cv2.rectangle(overlay, (x1, y1), (x2, y2), color, 2)

        tag = f"{cname} {conf*100:.1f}%"
        (tw, th), baseline = cv2.getTextSize(tag, cv2.FONT_HERSHEY_SIMPLEX, 0.5, 1)
        cv2.rectangle(overlay, (x1, max(0, y1 - th - 6)), (x1 + tw + 6, max(0, y1)), color, -1)
        cv2.putText(overlay, tag, (x1 + 3, max(th, y1 - 3)), cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 255, 255), 1, cv2.LINE_AA)

    return overlay


def compute_anomaly_map(
    orig_image: np.ndarray,
    reconstructed_image: np.ndarray,
    gaussian_blur_ksize: int = 15
) -> Tuple[float, np.ndarray]:
    """
    Calculates L2 reconstruction residual heatmap and global anomaly score.
    Returns:
        anomaly_score: float normalized in [0, 100]
        heatmap_bgr: colorized jet heatmap
    """
    if orig_image.shape != reconstructed_image.shape:
        reconstructed_image = cv2.resize(reconstructed_image, (orig_image.shape[1], orig_image.shape[0]))

    diff = np.abs(orig_image.astype(np.float32) - reconstructed_image.astype(np.float32))
    if len(diff.shape) == 3:
        diff_gray = np.mean(diff, axis=2)
    else:
        diff_gray = diff

    if gaussian_blur_ksize > 1 and gaussian_blur_ksize % 2 == 1:
        diff_gray = cv2.GaussianBlur(diff_gray, (gaussian_blur_ksize, gaussian_blur_ksize), 0)

    max_val = np.max(diff_gray)
    mean_val = np.mean(diff_gray)

    # Anomaly score combining peak error and distribution spread
    anomaly_score = float(np.clip(mean_val * 1.5 + max_val * 0.2, 0.0, 100.0))

    norm_diff = cv2.normalize(diff_gray, None, 0, 255, cv2.NORM_MINMAX, dtype=cv2.CV_8U)
    heatmap_bgr = cv2.applyColorMap(norm_diff, cv2.COLORMAP_JET)

    return round(anomaly_score, 2), heatmap_bgr
