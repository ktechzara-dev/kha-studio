namespace VisionStudio.DeepLearning.Model;

/// <summary>
/// Industrial Deep Learning inspection task categories.
/// </summary>
public enum DeepLearningTaskType
{
    /// <summary>
    /// Unsupervised / semi-supervised anomaly detection trained on good samples only (PatchCore/PaDiM class).
    /// Produces anomaly heatmaps, overall anomaly score, and segmented defect regions.
    /// </summary>
    AnomalyDetection = 0,

    /// <summary>
    /// Single or multi-label image classification with confidence scores and top-k ranking.
    /// </summary>
    Classification = 1,

    /// <summary>
    /// Bounding box object detection (e.g. YOLO, SSD, Faster R-CNN) with NMS filtering.
    /// </summary>
    ObjectDetection = 2,

    /// <summary>
    /// Pixel-level semantic or instance segmentation masks convertible to Region geometries.
    /// </summary>
    InstanceSegmentation = 3,

    /// <summary>
    /// Deep learning text detection and character recognition with template verification.
    /// </summary>
    DeepOcr = 4
}
