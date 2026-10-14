# VisionStudio Tool Catalog

This catalog documents all vision and flow control tools available in VisionStudio, detailing their Category, Ports, and configurable Parameters.

---

## 1. Flow Control Tools (`VisionStudio.Core`)

### Flow.ConstantSource
- **Category:** Flow Control
- **Description:** Emits configured constant numbers and strings for simulation and parameter feeding.
- **Parameters:**
  - `NumberValue` (Double): Constant numerical value to emit (default: 42.0).
  - `TextValue` (String): Constant string value to emit (default: "Part-A100").
- **Inputs:** None.
- **Outputs:**
  - `OutNumber` (Number): The emitted number.
  - `OutText` (String): The emitted string.

### Flow.Branch
- **Category:** Flow Control
- **Description:** Conditionally routes input data to either TrueOut or FalseOut port.
- **Parameters:** None.
- **Inputs:**
  - `Condition` (Boolean): Gate condition (default: true).
  - `DataIn` (Any): Data packet to route.
- **Outputs:**
  - `TrueOut` (Any): Emitted when Condition is true.
  - `FalseOut` (Any): Emitted when Condition is false.
  - `ConditionOut` (Boolean): Passthrough condition value.

### Flow.Counter
- **Category:** Flow Control
- **Description:** Tracks execution iterations and signals target threshold completion.
- **Parameters:**
  - `InitialValue` (Integer): Starting count (default: 0).
  - `Step` (Integer): Step increment per cycle (default: 1).
  - `Target` (Integer): Limit count target (default: 100).
- **Inputs:** None.
- **Outputs:**
  - `Count` (Number): Current accumulated integer count.
  - `TargetReached` (Boolean): True when count >= Target.

### Flow.Delay
- **Category:** Flow Control
- **Description:** Pauses execution for a specified duration to allow camera/lighting/actuator settling.
- **Parameters:**
  - `Milliseconds` (Integer): Delay duration in ms (0 to 60000 ms, default: 50).
- **Inputs:**
  - `DataIn` (Any, optional): Passthrough payload.
- **Outputs:**
  - `DataOut` (Any): Passthrough payload.

### Flow.Expression
- **Category:** Flow Control
- **Description:** Evaluates safe arithmetic and relational operations between numbers.
- **Parameters:**
  - `Operation` (Enum): `Add`, `Subtract`, `Multiply`, `Divide`, `GreaterThan`, `LessThan`, `Equal`, `InRange`.
  - `Tolerance` (Double): Comparison tolerance delta (default: 0.001).
- **Inputs:**
  - `A` (Number): First numerical operand.
  - `B` (Number, optional): Second numerical operand.
- **Outputs:**
  - `ResultNumber` (Number): Computed numeric value.
  - `ResultBool` (Boolean): Boolean evaluation outcome.

### Output.Judge
- **Category:** Output
- **Description:** Evaluates numerical measurement tolerance limits and emits pass/fail verdict with graphical overlays.
- **Parameters:**
  - `LowerLimit` (Double): Minimum acceptable threshold.
  - `UpperLimit` (Double): Maximum acceptable threshold.
- **Inputs:**
  - `Value` (Number): Measurement value to test.
- **Outputs:**
  - `IsOk` (Boolean): True if value is within [LowerLimit, UpperLimit].
  - `Verdict` (String): "OK" or "NG".

---

## 2. Acquisition Tools (`VisionStudio.Imaging.Acquisition`)

### Acquisition.CameraGrab
- **Category:** Acquisition
- **Description:** Acquires a frame from an industrial camera or built-in test generator.
- **Parameters:**
  - `CameraId` (String): Camera device identifier.
  - `Width` (Integer): Frame width in pixels (default: 640).
  - `Height` (Integer): Frame height in pixels (default: 480).
  - `Pattern` (Enum): `TargetPart`, `Checkerboard`, `ColorBars`, `Noise`.
- **Inputs:** None.
- **Outputs:**
  - `Image` (Image): Acquired native frame.
  - `FrameId` (Number): Frame sequence counter.

### Acquisition.ImageFile
- **Category:** Acquisition
- **Description:** Loads a single image from a local file path.
- **Parameters:**
  - `FilePath` (FilePath): Local image path.
- **Inputs:** None.
- **Outputs:**
  - `Image` (Image): Decoded image frame.

### Acquisition.FolderSequence
- **Category:** Acquisition
- **Description:** Sequentially loads image batches from a folder to simulate streams or play test sequences.
- **Parameters:**
  - `FolderPath` (FilePath): Directory path.
  - `Loop` (Boolean): Whether to wrap around to first frame upon completion.
  - `SearchPattern` (String): Extension filter (default: `*.png|*.jpg|*.bmp`).
- **Inputs:** None.
- **Outputs:**
  - `Image` (Image): Current frame.
  - `FileName` (String): Source file basename.
  - `Index` (Number): Zero-based image index.

### Acquisition.VideoFile
- **Category:** Acquisition
- **Description:** Decodes frames sequentially from a video stream file.
- **Parameters:**
  - `FilePath` (FilePath): Video path.
  - `Loop` (Boolean): Loop playback.
- **Inputs:** None.
- **Outputs:**
  - `Image` (Image): Decoded video frame.
  - `FrameIndex` (Number): Current frame index.

---

## 3. Geometry & ROI Tools (`VisionStudio.Imaging.Geometry`)

### Geometry.Crop
- **Category:** Geometry / ROI
- **Description:** Crops an axis-aligned rectangular sub-window from an input image.
- **Parameters:**
  - `X` (Integer): Horizontal origin coordinate.
  - `Y` (Integer): Vertical origin coordinate.
  - `Width` (Integer): Sub-window width.
  - `Height` (Integer): Sub-window height.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Geometry.Roi
- **Category:** Geometry / ROI
- **Description:** Creates an interactive inspection Region of Interest (Rectangle, Circle, Ring) and outputs binary mask.
- **Parameters:**
  - `Shape` (Enum): `Rectangle`, `Circle`, `Ring`.
  - `CenterX`, `CenterY` (Integer): Center position.
  - `Radius`, `InnerRadius` (Integer): Outer and inner radii.
  - `Width`, `Height` (Integer): Rectangular dimensions.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `Mask` (Image): 8-bit binary mask (0 or 255).
  - `MaskedImage` (Image): Image with masked pixels preserved.

### Geometry.Rotate
- **Category:** Geometry / ROI
- **Description:** Rotates an image around a pivot center by arbitrary angles with optional scaling.
- **Parameters:**
  - `Angle` (Double): Rotation angle in degrees (-360 to +360).
  - `Scale` (Double): Scale factor (0.1 to 10.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Geometry.Flip
- **Category:** Geometry / ROI
- **Description:** Mirrors image horizontally, vertically, or both.
- **Parameters:**
  - `FlipMode` (Enum): `Horizontal`, `Vertical`, `Both`.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Geometry.Resize
- **Category:** Geometry / ROI
- **Description:** Rescales image to explicit dimensions or proportional ratio.
- **Parameters:**
  - `Mode` (Enum): `ExplicitSize`, `ScaleFactor`.
  - `Width`, `Height` (Integer): Target pixel dimensions.
  - `Scale` (Double): Proportional scale.
  - `Interpolation` (Enum): `Nearest`, `Linear`, `Area`, `Cubic`.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Geometry.PerspectiveWarp
- **Category:** Geometry / ROI
- **Description:** Warps quadrilateral skewed planar surfaces into rectified rectangular views.
- **Parameters:**
  - `OutputWidth`, `OutputHeight` (Integer): Dimensions of rectified output.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Geometry.PolarUnwrap
- **Category:** Geometry / ROI
- **Description:** Unwraps circular and annular targets (bearings, caps, labels) into planar strips.
- **Parameters:**
  - `MaxRadius` (Double): Max outer radius.
  - `OutputWidth` (Integer): Angular resolution (horizontal pixels).
  - `OutputHeight` (Integer): Radial thickness (vertical pixels).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Geometry.MaskFromRegion
- **Category:** Geometry / ROI
- **Description:** Applies a binary mask to an image or inverts the masking region.
- **Parameters:**
  - `InvertMask` (Boolean): Inverts mask logic.
- **Inputs:**
  - `ImageIn` (Image): Source image.
  - `MaskIn` (Image): Binary mask.
- **Outputs:** `ImageOut` (Image).

---

## 4. Color & Intensity Tools (`VisionStudio.Imaging.Color`)

### Color.Grayscale
- **Category:** Color / Intensity
- **Description:** Converts multi-channel BGR/BGRA images to 8-bit monochrome grayscale.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image, Mono8).

### Color.ChannelSplit
- **Category:** Color / Intensity
- **Description:** Separates color image into individual Blue, Green, and Red channel frames.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `Blue`, `Green`, `Red` (Images, Mono8).

### Color.ColorSpaceConvert
- **Category:** Color / Intensity
- **Description:** Transforms color spaces between BGR, HSV, CIE Lab, and YCrCb.
- **Parameters:**
  - `TargetSpace` (Enum): `HSV`, `Lab`, `YCrCb`.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image), `Ch1`, `Ch2`, `Ch3` (Mono8 channel planes).

### Color.BrightnessContrast
- **Category:** Color / Intensity
- **Description:** Linear gain and offset intensity adjustment: `dst = alpha * src + beta`.
- **Parameters:**
  - `ContrastGain` (Double): Alpha multiplier (0.1 to 5.0).
  - `BrightnessOffset` (Double): Beta offset (-150 to +150).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Color.Gamma
- **Category:** Color / Intensity
- **Description:** Non-linear power-law gamma luminance transformation via precalculated LUT.
- **Parameters:**
  - `Gamma` (Double): Gamma exponent (0.1 to 5.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Color.HistogramEqualize
- **Category:** Color / Intensity
- **Description:** Contrast enhancement via global histogram equalization or CLAHE.
- **Parameters:**
  - `Method` (Enum): `CLAHE`, `Standard`.
  - `ClaheClipLimit` (Double): Clip limit for contrast limitation (default: 2.0).
  - `ClaheTileSize` (Integer): Grid tile size (default: 8).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Color.Normalize
- **Category:** Color / Intensity
- **Description:** Stretches histogram distribution between configured min/max boundaries.
- **Parameters:**
  - `MinVal`, `MaxVal` (Double): Target range (default: 0.0 to 255.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Color.Invert
- **Category:** Color / Intensity
- **Description:** Bitwise NOT intensity inversion producing photographic negative.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Color.ColorThreshold
- **Category:** Color / Intensity
- **Description:** Segments chromatic pixels within lower/upper bounds in HSV or BGR.
- **Parameters:**
  - `ColorSpace` (Enum): `HSV`, `BGR`.
  - `MinCh1`, `MaxCh1`, `MinCh2`, `MaxCh2`, `MinCh3`, `MaxCh3` (Integer): Boundary ranges.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `Mask` (Image, Mono8): Binary match mask.
  - `PixelCount` (Number): Count of matching pixels.
  - `AreaFraction` (Number): Percentage of frame covered.

---

## 5. Filtering Tools (`VisionStudio.Imaging.Filtering`)

### Filter.Gaussian
- **Category:** Filtering
- **Description:** 2D Gaussian spatial low-pass convolution filter.
- **Parameters:**
  - `KernelSize` (Integer): Odd kernel diameter (default: 5).
  - `SigmaX`, `SigmaY` (Double): Standard deviation scales.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Filter.Median
- **Category:** Filtering
- **Description:** Non-linear rank filter eliminating salt-and-pepper impulse noise.
- **Parameters:**
  - `KernelSize` (Integer): Odd kernel diameter (default: 3).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Filter.Bilateral
- **Category:** Filtering
- **Description:** Edge-preserving smoothing combining spatial distance and radiometric difference.
- **Parameters:**
  - `Diameter` (Integer): Neighborhood diameter (default: 9).
  - `SigmaColor` (Double): Filter sigma in color space (default: 75.0).
  - `SigmaSpace` (Double): Filter sigma in coordinate space (default: 75.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Filter.Box
- **Category:** Filtering
- **Description:** Uniform area averaging box convolution filter.
- **Parameters:**
  - `KernelWidth`, `KernelHeight` (Integer): Kernel dimensions (default: 5x5).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Filter.Sharpen
- **Category:** Filtering
- **Description:** Unsharp masking high-pass boost for edge transition crispening.
- **Parameters:**
  - `Amount` (Double): Sharpness boost factor (default: 1.5).
  - `Radius` (Integer): Blur kernel radius (default: 5).
  - `Threshold` (Double): Edge difference threshold.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Filter.MorphGradient
- **Category:** Filtering
- **Description:** Computes morphological gradient (dilation minus erosion) highlighting boundaries.
- **Parameters:**
  - `KernelSize` (Integer): Odd kernel diameter (default: 3).
  - `Shape` (Enum): `Rect`, `Cross`, `Ellipse`.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Filter.FlatFieldCorrection
- **Category:** Filtering
- **Description:** Corrects non-uniform illumination and lens vignetting via reference normalization.
- **Inputs:**
  - `RawImage` (Image): Uncorrected raw frame.
  - `BrightField` (Image): Uniform white/bright reference image.
  - `DarkField` (Image, optional): Dark reference frame.
- **Outputs:** `ImageOut` (Image): Corrected frame.

### Filter.FftBandpass
- **Category:** Filtering
- **Description:** 2D Fourier frequency domain band-pass / notch filter for periodic defect isolation.
- **Parameters:**
  - `FilterMode` (Enum): `LowPass`, `HighPass`, `BandPass`.
  - `LowCutoffRadius`, `HighCutoffRadius` (Double): Radial cutoffs.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `ImageOut` (Image): Filtered spatial domain image.
  - `MagnitudeSpectrum` (Image): Log-magnitude frequency spectrum.

---

## 6. Segmentation Tools (`VisionStudio.Imaging.Segmentation`)

### Segmentation.Threshold
- **Category:** Segmentation
- **Description:** Converts grayscale image to black/white binary mask via Fixed, Otsu, or Adaptive methods.
- **Parameters:**
  - `Method` (Enum): `Fixed`, `Otsu`, `AdaptiveMean`, `AdaptiveGaussian`.
  - `ThresholdValue` (Double): Fixed threshold cutoff (default: 128.0).
  - `Invert` (Boolean): Inverts black and white output.
  - `AdaptiveBlockSize` (Integer): Neighborhood block size (default: 11).
  - `AdaptiveC` (Double): Adaptive offset constant (default: 2.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `Mask` (Image, Mono8): Binary output mask.
  - `CalculatedThreshold` (Number): Computed threshold value.

### Segmentation.DualThreshold
- **Category:** Segmentation
- **Description:** Isolates pixels within lower and upper intensity bands.
- **Parameters:**
  - `LowThreshold`, `HighThreshold` (Double): Lower and upper cutoffs.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `Mask` (Image, Mono8): Output mask.
  - `PixelCount` (Number): Non-zero pixel count.

### Segmentation.Watershed
- **Category:** Segmentation
- **Description:** Separates touching or clustered objects (particles, pills, overlapping components) using distance-transform watershed.
- **Parameters:**
  - `DistanceThresholdRatio` (Double): Fraction of peak distance for foreground seed extraction (default: 0.4).
  - `DarkObjectsonLightBackground` (Boolean): Inverts threshold if target objects are darker than background.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `SegmentedImage` (Image): Colorized partition result with boundary contours.
  - `ObjectCount` (Number): Number of uniquely segmented objects.

### Segmentation.KMeans
- **Category:** Segmentation
- **Description:** Clusters color pixels into K dominant palette centroids via K-Means machine learning.
- **Parameters:**
  - `K` (Integer): Cluster count (2 to 16, default: 3).
  - `MaxIterations` (Integer): Convergence iterations (default: 10).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image, Bgr24).

---

## 7. Morphology Tools (`VisionStudio.Imaging.Morphology`)

### Morphology.Basic
- **Category:** Morphology
- **Description:** Executes fundamental mathematical morphology operations.
- **Parameters:**
  - `Operation` (Enum): `Erode`, `Dilate`, `Open`, `Close`, `TopHat`, `BlackHat`.
  - `KernelSize` (Integer): Odd kernel diameter (default: 3).
  - `KernelShape` (Enum): `Rect`, `Cross`, `Ellipse`.
  - `Iterations` (Integer): Repetitions (default: 1).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image).

### Morphology.FillHoles
- **Category:** Morphology
- **Description:** Fills interior enclosed dark cavities within binary foreground shapes without altering external contours.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image, Mono8).

### Morphology.Skeleton
- **Category:** Morphology
- **Description:** Thins binary shapes to single-pixel topological centerline skeletons via morphological decomposition.
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `ImageOut` (Image, Mono8).

---

## 8. Blob Analysis (`VisionStudio.Imaging.Blob`)

### Blob.Analysis
- **Category:** Blob Analysis
- **Description:** Connected component analysis computing Area, Perimeter, Centroid, Bounding Box, Rotated Box, Circularity, Aspect Ratio, and Orientation, with feature filtering and sorting.
- **Parameters:**
  - `MinArea`, `MaxArea` (Double): Pixel area bounds.
  - `MinCircularity`, `MaxCircularity` (Double): Form factor circularity bounds (0.0 to 1.0).
  - `SortBy` (Enum): `AreaDescending`, `AreaAscending`, `XAscending`, `YAscending`, `CircularityDescending`.
  - `MaxBlobs` (Integer): Maximum candidate blobs to retain.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `BlobCount` (Number): Number of qualified blobs detected.
  - `PrimaryArea` (Number): Area of dominant/primary blob.
  - `PrimaryCenterX`, `PrimaryCenterY` (Number): Centroid coordinates of primary blob.
  - `LabeledImage` (Image): Annotated image with rotated bounding boxes, centroid crosshairs, and metrics.
- **Overlays:** RotatedRectOverlay, CrossOverlay, TextOverlay.

---

## 9. Edges & Contours (`VisionStudio.Imaging.Edges`)

### Edges.Canny
- **Category:** Edges & Contours
- **Description:** Industrial Canny edge detector with hysteresis thresholding and aperture selection.
- **Parameters:**
  - `Threshold1` (Double): Lower hysteresis threshold (default: 50.0).
  - `Threshold2` (Double): Upper hysteresis threshold (default: 150.0).
  - `ApertureSize` (Integer): Sobel aperture size (3, 5, or 7, default: 3).
  - `L2Gradient` (Boolean): Use more accurate L2 norm (default: false).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `EdgeImage` (Image, Mono8).

### Edges.SobelScharr
- **Category:** Edges & Contours
- **Description:** First-order spatial derivative edge filtering along X, Y, or combined magnitude using Sobel or Scharr operators.
- **Parameters:**
  - `Operator` (Enum): `Sobel`, `Scharr`.
  - `Direction` (Enum): `Both`, `XOnly`, `YOnly`.
  - `ApertureSize` (Integer): Sobel aperture size (1, 3, 5, or 7, default: 3).
  - `Scale` (Double): Derivative scale factor (default: 1.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `EdgeImage` (Image, Mono8).

### Edges.Laplacian
- **Category:** Edges & Contours
- **Description:** Second-order isotropic Laplacian edge filter highlighting rapid intensity variations.
- **Parameters:**
  - `KernelSize` (Integer): Aperture size (1, 3, 5, or 7, default: 3).
  - `Scale` (Double): Result scaling multiplier (default: 1.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:** `EdgeImage` (Image, Mono8).

### Edges.FindContours
- **Category:** Edges & Contours
- **Description:** Topological contour extraction with hierarchical grouping, polygon approximation, and length/area filtering.
- **Parameters:**
  - `RetrievalMode` (Enum): `External`, `List`, `CComp`, `Tree`.
  - `ApproxMethod` (Enum): `ApproxSimple`, `ApproxNone`, `ApproxTC89L1`, `ApproxTC89KCOS`.
  - `MinContourArea` (Double): Minimum surface area threshold (default: 20.0).
  - `MinContourLength` (Double): Minimum arc length threshold (default: 10.0).
  - `PolygonEpsilon` (Double): Ramer-Douglas-Peucker polygon approximation epsilon (default: 2.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `ContourCount` (Number): Total qualified contours detected.
  - `MaxContourArea` (Number): Area of largest contour.
  - `ContourImage` (Image): Color rendering of drawn contours.
- **Overlays:** PolygonOverlay.

### Edges.SubPixel
- **Category:** Edges & Contours
- **Description:** Sub-pixel contour boundary localization using image moments and gradient normal refinement.
- **Parameters:**
  - `CannyLow` (Double): Low edge threshold (default: 40.0).
  - `CannyHigh` (Double): High edge threshold (default: 120.0).
  - `RefinementWindow` (Integer): Sub-pixel search half-window (default: 3).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `PointCount` (Number): Number of extracted sub-pixel boundary points.
  - `AnnotatedImage` (Image): Image annotated with sub-pixel points.
- **Overlays:** CrossOverlay.

---

## 10. Geometry Fitting (`VisionStudio.Imaging.Fitting`)

### Fitting.GeometryFit
- **Category:** Geometry Fitting
- **Description:** Fits analytic geometrical primitives (Straight Line, Best-fit Circle, Least-squares Ellipse) through 2D edge point sets.
- **Parameters:**
  - `PrimitiveType` (Enum): `Line`, `Circle`, `Ellipse`.
  - `DistanceType` (Enum): `L2`, `L1`, `Huber`, `Fair`, `Welsch`.
- **Inputs:**
  - `ImageIn` (Image, optional): Background image for overlays.
  - `ContourIn` (Contour/Any, optional): Point set to fit.
- **Outputs:**
  - `CenterX` (Number): Circle/Ellipse center X or Line point X0.
  - `CenterY` (Number): Circle/Ellipse center Y or Line point Y0.
  - `Radius` (Number): Fitted circle radius.
  - `AngleDeg` (Number): Line slope or Ellipse major-axis inclination angle in degrees.
  - `FitResidual` (Number): Mean orthogonal residual error.
- **Overlays:** LineOverlay, CircleOverlay, RotatedRectOverlay.

### Fitting.HoughLines
- **Category:** Geometry Fitting
- **Description:** Standard and Probabilistic Hough Transform for detecting straight lines, segments, and angle alignments.
- **Parameters:**
  - `UseProbabilistic` (Boolean): Enable probabilistic line segment detection (default: true).
  - `Threshold` (Integer): Accumulator vote threshold (default: 50).
  - `MinLineLength` (Double): Minimum segment length in pixels (default: 30.0).
  - `MaxLineGap` (Double): Maximum allowable pixel gap between colinear segments (default: 10.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `LineCount` (Number): Total straight lines/segments detected.
  - `DominantAngleDeg` (Number): Angle of longest or primary line.
  - `AnnotatedImage` (Image): Image with rendered line overlays.
- **Overlays:** LineOverlay.

### Fitting.HoughCircles
- **Category:** Geometry Fitting
- **Description:** 2-1 Hough Gradient circle detection locating circular industrial features (holes, washers, pins, bottle caps).
- **Parameters:**
  - `Dp` (Double): Inverse accumulator resolution ratio (default: 1.2).
  - `MinDist` (Double): Minimum distance between circle centers (default: 30.0).
  - `Param1` (Double): Canny upper threshold (default: 100.0).
  - `Param2` (Double): Accumulator threshold for center detection (default: 30.0).
  - `MinRadius`, `MaxRadius` (Integer): Circle radius search range.
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `CircleCount` (Number): Qualified circles detected.
  - `PrimaryCenterX`, `PrimaryCenterY` (Number): Center coordinates of prominent circle.
  - `PrimaryRadius` (Number): Radius of prominent circle.
  - `AnnotatedImage` (Image): Image with rendered circle and centroid crosshair overlays.
- **Overlays:** CircleOverlay, CrossOverlay.

### Fitting.Distance
- **Category:** Geometry Fitting
- **Description:** Computes Euclidean metric distances between Point-to-Point, Point-to-Line, and Line-to-Line primitives with optional mm scaling.
- **Parameters:**
  - `Mode` (Enum): `PointToPoint`, `PointToLine`, `LineToLine`.
  - `ScaleMmPerPixel` (Double): Calibration scale in mm/px (0 = pixels only, default: 0.0).
- **Inputs:**
  - `X1`, `Y1`, `X2`, `Y2`, `X3`, `Y3`, `X4`, `Y4` (Number): Coordinates defining points or line segments.
- **Outputs:**
  - `DistancePixels` (Number): Computed distance in pixel units.
  - `DistanceMm` (Number): Calibrated distance in millimeters.
- **Overlays:** LineOverlay, TextOverlay.

---

## 11. Measurement Tools (`VisionStudio.Imaging.Measurement`)

### Measurement.Caliper
- **Category:** Measurement
- **Description:** High-precision 1D sub-pixel edge caliper measuring edge pair transitions and distance along an arbitrary scan line (< 0.1 px accuracy via parabolic interpolation).
- **Parameters:**
  - `StartX`, `StartY`, `EndX`, `EndY` (Double): Scan vector endpoints.
  - `EdgeThreshold` (Double): Minimum 1st-derivative peak threshold (default: 20.0).
  - `Transition` (Enum): `Any`, `DarkToLight`, `LightToDark`.
  - `ScaleMmPerPixel` (Double): Calibration conversion factor (default: 0.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `WidthPixels` (Number): Width between first and last edge transition.
  - `WidthMm` (Number): Calibrated width in mm.
  - `Edge1X`, `Edge1Y` (Number): First edge sub-pixel coordinates.
  - `Edge2X`, `Edge2Y` (Number): Second edge sub-pixel coordinates.
  - `AnnotatedImage` (Image): Visual verification image.
- **Overlays:** LineOverlay, CrossOverlay, TextOverlay.

### Measurement.MultiCaliper
- **Category:** Measurement
- **Description:** Multi-rake caliper firing N parallel scan lines across an oriented rectangular inspection corridor.
- **Parameters:**
  - `CenterX`, `CenterY` (Double): Corridor center coordinates.
  - `Length` (Double): Corridor scan length (default: 100.0).
  - `Width` (Double): Corridor breadth across scan lines (default: 60.0).
  - `AngleDeg` (Double): Corridor orientation angle (default: 0.0).
  - `RakeCount` (Integer): Number of parallel scan lines (default: 7).
  - `EdgeThreshold` (Double): Derivative threshold (default: 20.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `MeanWidthPixels` (Number): Average width across valid rake measurements.
  - `StdDevPixels` (Number): Standard deviation of widths (form error / waviness).
  - `ValidRakeCount` (Number): Count of successfully locked rakes.
  - `AnnotatedImage` (Image): Scan corridors and detected edge points.
- **Overlays:** LineOverlay, RotatedRectOverlay, CrossOverlay.

### Measurement.ProfileExtraction
- **Category:** Measurement
- **Description:** Samples 1D intensity profile along a line segment with bilinear pixel interpolation.
- **Parameters:**
  - `StartX`, `StartY`, `EndX`, `EndY` (Double): Profile trajectory endpoints.
  - `SampleCount` (Integer): Number of equidistant sample steps (default: 200).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `MinIntensity`, `MaxIntensity`, `MeanIntensity` (Number): Profile statistical moments.
  - `AnnotatedImage` (Image): Line probe overlay.
- **Overlays:** LineOverlay.

### Measurement.Dimension
- **Category:** Measurement
- **Description:** Converts pixel measurements to real-world engineering units (mm, inches, microns) and validates upper/lower tolerances.
- **Parameters:**
  - `PixelToWorldScale` (Double): Calibration factor (units per pixel, default: 0.05).
  - `UnitName` (String): Display unit (default: "mm").
  - `NominalValue` (Double): Target design dimension.
  - `UpperTolerance` (Double): Maximum allowable deviation.
  - `LowerTolerance` (Double): Minimum allowable deviation.
- **Inputs:**
  - `PixelValue` (Number): Raw pixel dimension.
- **Outputs:**
  - `WorldValue` (Number): Converted measurement.
  - `Deviation` (Number): Difference from nominal value.
  - `IsWithinTolerance` (Boolean): Quality tolerance check result.
  - `Verdict` (String): "OK" or "NG".

---

## 12. Matching & Alignment (`VisionStudio.Imaging.Matching`)

### Matching.Template
- **Category:** Matching & Alignment
- **Description:** Normalized Cross-Correlation (NCC) pattern matching locating template occurrences with sub-pixel peak refinement.
- **Parameters:**
  - `TemplatePath` (FilePath): Golden template image file.
  - `MinScore` (Double): Minimum correlation score threshold (0.0 to 1.0, default: 0.8).
  - `MaxMatches` (Integer): Maximum matches to locate (default: 1).
  - `MatchingMethod` (Enum): `CCoeffNormed`, `CCorrNormed`, `SqDiffNormed`.
- **Inputs:**
  - `ImageIn` (Image): Search image.
  - `TemplateImage` (Image, optional): Dynamic template passed via port.
- **Outputs:**
  - `MatchCount` (Number): Number of successful matches.
  - `Score` (Number): Correlation score of best match.
  - `BestMatchX`, `BestMatchY` (Number): Center coordinates of best match.
  - `AnnotatedImage` (Image): Image with matched bounding box overlays.
- **Overlays:** RectOverlay, CrossOverlay, TextOverlay.

### Matching.RotationInvariant
- **Category:** Matching & Alignment
- **Description:** Multi-angle pyramid template matcher detecting rotated instances over 360-degree orientation.
- **Parameters:**
  - `TemplatePath` (FilePath): Template file.
  - `MinAngleDeg` (Double): Minimum search angle (default: -180.0).
  - `MaxAngleDeg` (Double): Maximum search angle (default: 180.0).
  - `AngleStepDeg` (Double): Coarse angle search resolution (default: 5.0).
  - `MinScore` (Double): Acceptance score threshold (default: 0.75).
- **Inputs:**
  - `ImageIn` (Image): Search image.
  - `TemplateImage` (Image, optional): Dynamic template.
- **Outputs:**
  - `Score` (Number): Best match score.
  - `BestMatchX`, `BestMatchY` (Number): Center coordinates.
  - `AngleDeg` (Number): Orientation angle in degrees.
  - `AnnotatedImage` (Image): Rotated bounding box overlay.
- **Overlays:** RotatedRectOverlay, CrossOverlay.

### Matching.Feature
- **Category:** Matching & Alignment
- **Description:** Scale and rotation-invariant feature matching using ORB descriptors and RANSAC homography estimation.
- **Parameters:**
  - `TemplatePath` (FilePath): Reference model image.
  - `MaxFeatures` (Integer): ORB keypoint budget (default: 1000).
  - `RansacReprojThreshold` (Double): Inlier threshold in pixels (default: 3.0).
  - `MinInliers` (Integer): Minimum RANSAC inlier count for OK status (default: 15).
- **Inputs:**
  - `ImageIn` (Image): Query scene.
  - `TemplateImage` (Image, optional): Reference model.
- **Outputs:**
  - `InlierCount` (Number): Number of geometrically verified inliers.
  - `CenterX`, `CenterY` (Number): Transformed model center in scene.
  - `AngleDeg` (Number): Estimated planar rotation.
  - `AnnotatedImage` (Image): Warped polygon boundary in scene.
- **Overlays:** PolygonOverlay, CrossOverlay.

### Matching.Shape
- **Category:** Matching & Alignment
- **Description:** Scale-, shift-, and rotation-invariant shape silhouette matching based on logarithmic Hu-moment distances.
- **Parameters:**
  - `MaxDistance` (Double): Maximum allowable Hu-moment divergence (default: 0.15).
  - `MinArea` (Double): Minimum candidate contour area (default: 100.0).
- **Inputs:**
  - `ImageIn` (Image): Binary or grayscale scene.
  - `TemplateImage` (Image, optional): Target shape silhouette.
- **Outputs:**
  - `ShapeDistance` (Number): Hu-moment shape metric (0 = identical).
  - `BestMatchX`, `BestMatchY` (Number): Centroid of matching shape.
  - `AnnotatedImage` (Image): Highlighted matching contour.
- **Overlays:** PolygonOverlay, CrossOverlay.

### Matching.PatternAlign
- **Category:** Matching & Alignment
- **Description:** Golden reference alignment computing rigid affine translation and rotation matrix ($X, Y, \theta$) to electronically rectify misaligned parts.
- **Parameters:**
  - `ReferenceX`, `ReferenceY` (Double): Golden nominal part position.
  - `ReferenceAngleDeg` (Double): Golden nominal part orientation.
- **Inputs:**
  - `CurrentX`, `CurrentY` (Number): Measured part coordinates.
  - `CurrentAngleDeg` (Number): Measured part orientation.
  - `ImageIn` (Image, optional): Raw unaligned part image.
- **Outputs:**
  - `DeltaX`, `DeltaY` (Number): Translation offsets to golden position.
  - `DeltaAngleDeg` (Number): Rotational angular offset.
  - `AlignedImage` (Image, optional): Rigidly transformed rectified image.

---

## 13. Identification Tools (`VisionStudio.Imaging.Identification`)

### Identification.Barcode
- **Category:** Identification
- **Description:** 1D barcode and 2D matrix code reader using ZXing.Net decoding engine (QR Code, DataMatrix, PDF417, Aztec, Code 128, Code 39, EAN-13).
- **Parameters:**
  - `Symbology` (Enum): `Any`, `QRCode`, `DataMatrix`, `Code128`, `Code39`, `EAN13`, `PDF417`.
  - `TryHarder` (Boolean): Deep scanning optimization for low-contrast/damaged codes (default: true).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `DecodedText` (String): Decoded alphanumeric payload.
  - `Format` (String): Detected symbology standard.
  - `Success` (Boolean): True if barcode read was successful.
  - `AnnotatedImage` (Image): Bounding polygon overlay around barcode.
- **Overlays:** PolygonOverlay, TextOverlay.

### Identification.ColorCheck
- **Category:** Identification
- **Description:** Industrial color comparison measuring perceptual color difference ($\Delta E$) in standard CIE $L^*a^*b^*$ color space.
- **Parameters:**
  - `TargetL` (Double): Reference CIE Lightness (0 to 100).
  - `TargetA` (Double): Reference green-red coordinate (-128 to 127).
  - `TargetB` (Double): Reference blue-yellow coordinate (-128 to 127).
  - `MaxDeltaE` (Double): Maximum acceptable color tolerance (default: 5.0).
  - `RoiX`, `RoiY`, `RoiWidth`, `RoiHeight` (Integer): Color probe sample region.
- **Inputs:** `ImageIn` (Image, Bgr24).
- **Outputs:**
  - `DeltaE` (Number): Perceptual Euclidean color difference.
  - `MeasuredL`, `MeasuredA`, `MeasuredB` (Number): Average CIE coordinates of probe.
  - `IsMatch` (Boolean): True if $\Delta E \le \text{MaxDeltaE}$.
  - `AnnotatedImage` (Image): Probe box overlay with color label.
- **Overlays:** RectOverlay, TextOverlay.

### Identification.PresenceAbsence
- **Category:** Identification
- **Description:** Fast verification of component presence or absence inside an ROI via intensity thresholding or edge density analysis.
- **Parameters:**
  - `Mode` (Enum): `Intensity`, `EdgeDensity`.
  - `RoiX`, `RoiY`, `RoiWidth`, `RoiHeight` (Integer): Inspection window.
  - `Threshold` (Double): Verification threshold (default: 50.0).
  - `InvertLogic` (Boolean): Invert presence decision (default: false).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `IsPresent` (Boolean): Presence decision.
  - `MeasuredValue` (Number): Mean intensity or edge pixel density percentage.
  - `AnnotatedImage` (Image): ROI box colored green (Present) or red (Absent).
- **Overlays:** RectOverlay, TextOverlay.

---

## 14. Defect Detection Tools (`VisionStudio.Imaging.Defect`)

### Defect.GoldenTemplateDiff
- **Category:** Defect Detection
- **Description:** Subtractive template comparison isolating cosmetic flaws (voids, contamination, missing traces, shorts) against a golden reference.
- **Parameters:**
  - `GoldenImagePath` (FilePath): Golden pristine reference image.
  - `IntensityThreshold` (Double): Absolute difference threshold (default: 30.0).
  - `MinDefectArea` (Double): Minimum pixel defect cluster size (default: 15.0).
  - `BlurRadius` (Integer): Pre-filter smoothing radius to mitigate alignment noise (default: 3).
- **Inputs:**
  - `ImageIn` (Image): Inspected image.
  - `GoldenImage` (Image, optional): Dynamic golden reference.
- **Outputs:**
  - `DefectCount` (Number): Total separate defect clusters.
  - `TotalDefectArea` (Number): Aggregate pixel surface area of defects.
  - `DiffMask` (Image, Mono8): Binary difference mask.
  - `AnnotatedImage` (Image): Defect locations outlined with bounding boxes.
- **Overlays:** RectOverlay, TextOverlay.

### Defect.ScratchDetection
- **Category:** Defect Detection
- **Description:** Detects fine linear scratches, hairline cracks, and fissures on textured surfaces using directional morphological top-hat line filters.
- **Parameters:**
  - `KernelLength` (Integer): Length of linear structuring element in pixels (default: 15).
  - `AngleStepDeg` (Double): Angle step for multi-directional filtering (default: 30.0).
  - `Threshold` (Double): Intensity threshold for scratch extraction (default: 25.0).
  - `MinScratchLength` (Double): Minimum contour arc length to qualify as a scratch (default: 20.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `ScratchCount` (Number): Number of distinct scratches detected.
  - `MaxScratchLength` (Number): Arc length of longest detected scratch.
  - `DefectMask` (Image, Mono8): Extracted scratch binary mask.
  - `AnnotatedImage` (Image): Overlaid scratch contours.
- **Overlays:** PolygonOverlay, TextOverlay.

### Defect.SurfaceAnomaly
- **Category:** Defect Detection
- **Description:** High-frequency anomaly detector highlighting local blemishes, stains, and pits by computing difference from local background Gaussian estimate.
- **Parameters:**
  - `BackgroundFilterSize` (Integer): Large Gaussian kernel diameter (odd, default: 31).
  - `Sigma` (Double): Spatial smoothing sigma (default: 10.0).
  - `SensitivityThreshold` (Double): Deviation threshold from local background (default: 20.0).
  - `MinAnomalyArea` (Double): Minimum area threshold (default: 10.0).
- **Inputs:** `ImageIn` (Image).
- **Outputs:**
  - `AnomalyCount` (Number): Number of localized anomalies detected.
  - `TotalAnomalyArea` (Number): Cumulative anomalous pixel area.
  - `AnnotatedImage` (Image): Rendered anomaly bounding boxes.
- **Overlays:** RectOverlay.

---

## 15. Output Tools (`VisionStudio.Imaging.Output`)

### Output.SaveImage
- **Category:** Output
- **Description:** Saves inspected, annotated, or defect images to disk with configurable filename prefixes, timestamps, and optional condition gating (e.g., save only on NG).
- **Parameters:**
  - `DestinationFolder` (FolderPath): Directory path for saving images.
  - `FilePrefix` (String): Prefix string (default: "Frame").
  - `ImageFormat` (Enum): `Png`, `Jpg`, `Bmp`, `Tiff`.
  - `SaveOnlyOnCondition` (Boolean): If true, saves image only when SaveCondition input is true (default: false).
- **Inputs:**
  - `ImageIn` (Image): Image to save.
  - `SaveCondition` (Boolean, optional): Trigger condition (e.g., connected to NG signal).
- **Outputs:**
  - `SavedFilePath` (String): Complete saved file path on disk.
  - `Success` (Boolean): Save operation status.

### Output.LogToCsv
- **Category:** Output
- **Description:** Appends runtime inspection metrics, dimensions, verdicts, and cycle times to a designated CSV spreadsheet.
- **Parameters:**
  - `CsvFilePath` (FilePath): Target CSV file path.
  - `Headers` (String): Comma-separated column headers (e.g., "Timestamp,PartId,Width,Verdict").
- **Inputs:**
  - `Value1`, `Value2`, `Value3`, `Value4`, `Value5` (Any): Values to log in row.
- **Outputs:**
  - `Success` (Boolean): Append operation status.
  - `LoggedLine` (String): Serialized CSV line text.
