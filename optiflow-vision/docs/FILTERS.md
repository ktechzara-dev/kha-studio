# OptiFlow Vision Studio — Filter Library Specification

This document details the original filter library hierarchy planned and implemented progressively across milestones. Each filter is implemented with typed ports, deterministic execution, and unit tests.

---

## Filter Categories

### 1. Image Processing (`Processing`)
- `ConvertToGrayscale`: Multi-channel to Mono8 conversion.
- `ThresholdGlobal`: Fixed threshold with inverse option.
- `ThresholdOtsu`: Automatic optimal binarization.
- `ThresholdAdaptive`: Local adaptive window threshold.
- `ImageFilterBlur`: Gaussian, box, and bilateral smoothing.
- `Morphology`: Erode, dilate, open, close, and gradient.
- `EqualizeHistogram`: CLAHE and global contrast enhancement.

### 2. Region & Blob Analysis (`Analysis`)
- `ExtractBlobs`: Connected components with feature analysis (area, perimeter, centroid, orientation, circularity).
- `FilterBlobsByArea`: Filter regions within min/max area range.
- `RegionMoments`: Second-order central moments ($\mu_{20}, \mu_{02}, \mu_{11}$) and inertia axes.

### 3. 1D Edge Detection & Metrology (`Measurement`)
- `ScanEdgeProfile`: Sub-pixel 1D edge detection along line or arc.
- `MeasureWidth`: Caliper distance between rising and falling edge pairs.
- `FitLineRANSAC`: Robust outlier-resistant line fitting.
- `FitCircle`: Precision circle center and radius fitting.

### 4. Template Matching & Tracking (`Matching`)
- `CreateEdgeModel`: Geometric edge-based template model extraction.
- `MatchTemplate`: Pyramid-accelerated template matching with pose output ($X, Y, \theta$, score).
- `AlignCoordinates`: Dynamic coordinate frame transformation for downstream tools.

### 5. Identification & Text (`Identification`)
- `ReadBarcode1D`: EAN-13, Code 128, Code 39 via zxing-cpp.
- `ReadMatrix2D`: QR Code, DataMatrix reader.
- `SegmentText`: Morphological character region segmentation.

### 6. Calibration (`Calibration`)
- `CalibrateCamera`: Checkerboard target intrinsics ($K$), distortion ($D$).
- `ComputeHomography`: Planar homography ($H$) mapping pixels to real-world millimeters.
- `UndistortImage`: Fast precomputed `CV_16SC2` remap.

### 7. Communication & I/O (`Communication`)
- `TcpSocketClient`: High-speed binary/ASCII socket transmission.
- `SchneiderSertDispatch`: Byte-accurate `FB_CognexCamera` SERT protocol packet.
- `ModbusTcpMaster`: Modbus holding register reader/writer.
- `MqttPublisher`: Industrial IoT telemetry dispatch.
