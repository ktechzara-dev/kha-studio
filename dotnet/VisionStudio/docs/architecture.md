# VisionStudio Architecture Documentation

## 1. Overview
**VisionStudio** is a high-performance Windows desktop application designed for researching, prototyping, and executing industrial machine-vision pipelines in C# / .NET 8 / WPF.

It offers an open, highly modular alternative to Cognex VisionPro, Keyence, and HALCON HDevelop, operating in two primary modes:
- **Design Mode**: Visual interactive DAG editor, step-by-step pipeline execution, intermediate image inspection, parameter tuning, sub-pixel overlays.
- **Run Mode**: Live/triggered continuous execution, industrial PLC/robot handshake, statistics logging, defect image archival.

---

## 2. Core Engine Architecture (`VisionStudio.Core`)

### 2.1 `ImageFrame` Lifecycle & Memory Ownership
`ImageFrame` encapsulates native OpenCV `Mat` buffers with strict lifecycle ownership:
- **Producer Ownership**: The creating component (Camera or VisionTool) owns the underlying unmanaged buffer.
- **Deep Copying**: Call `.Clone()` when passing images across asynchronous tasks or keeping intermediate cached copies to guarantee isolated native memory.
- **Disposal**: Explicitly implements `IDisposable`. Once disposed, any access to `.Mat` immediately throws `ObjectDisposedException`.

### 2.2 `IVisionTool` & Parameter Descriptors
Every vision algorithm implements `IVisionTool` and is decorated with `[VisionTool]`:
- **Auto-Generated UI**: Declares typed `ToolParameter` instances with min/max, step, unit, and enum constraints. The WPF property inspector dynamically generates controls without per-tool UI code.
- **Typed Ports**: `ToolPort` specifies flow direction (`Input`/`Output`) and port type (`Image`, `Number`, `Boolean`, `Point`, `Region`, `Contour`, `Pose`, `Any`).

### 2.3 Directed Acyclic Graph (DAG) & Invalidation Engine
- **Cycle Detection**: Verified via 3-color Depth-First Search (`PipelineGraph.HasCycle()`).
- **Topological Execution**: Kahn's algorithm resolves deterministic execution ordering.
- **Per-Node Caching & Downstream Invalidation**:
  - Clean nodes reuse `node.CachedResult` without re-running.
  - Modifying a tool parameter triggers `node.NodeDirtied`, automatically invalidating only downstream reachable nodes while keeping upstream cached results intact.
- **Breakpoints & Step-Through**: Execution halts cleanly at flagged nodes for debugging.

### 2.4 Serialization & Migration
Pipelines are persisted to human-readable JSON via `PipelineSerializer` with a versioned schema (`schemaVersion: 1.0.0`). The `ToolRegistry` activates tool instances by ID during restoration.
