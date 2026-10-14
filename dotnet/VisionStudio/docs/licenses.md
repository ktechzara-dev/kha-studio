# Third-Party Licenses & Dependency Compliance

VisionStudio adheres strictly to permissive open-source licensing standards suitable for commercial and factory industrial deployments (MIT, Apache-2.0, BSD-3-Clause).

## 1. Managed Dependencies (.NET / NuGet)

| Package | Version | License | Commercial Use Permitted | Purpose |
| :--- | :--- | :--- | :---: | :--- |
| **Microsoft.ML.OnnxRuntime** | 1.19.2 | MIT | Yes | Hardware-accelerated neural network inference (CPU, DirectML, CUDA, TensorRT) |
| **Microsoft.CodeAnalysis.CSharp.Scripting** | 4.8.0 | MIT | Yes | Roslyn sandboxed C# compiler & runtime script host |
| **OpenCvSharp4** | 4.10.0 | Apache-2.0 | Yes | OpenCV computer vision wrappers, matrix manipulation, edge detection |
| **CommunityToolkit.Mvvm** | 8.3.2 | MIT | Yes | High-performance MVVM commands, messaging, observables |
| **Microsoft.Extensions.Hosting** | 8.0.1 | MIT | Yes | Application lifecycle management, dependency injection |
| **Serilog** | 4.1.0 | Apache-2.0 | Yes | Industrial structured diagnostic logging |

## 2. Deep Learning Models & Sidecar Frameworks

- **ONNX (Open Neural Network Exchange)**: Apache-2.0 license.
- **Model Package (.vsmodel)**: Open JSON metadata specification + ONNX binary model graph.
- **Sidecar Training Runtimes**: Permissive commercial licenses only (PyTorch BSD-3-Clause; torchvision BSD-3-Clause; timm Apache-2.0; anomalib Apache-2.0). Proprietary/GPL dual-licensed stacks are explicitly avoided.
