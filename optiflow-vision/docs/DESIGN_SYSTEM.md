# OptiFlow Vision Studio - Original Design System Specification

> **Document Version:** 1.0.0  
> **Target Environment:** Industrial Machine Vision Desktop IDE  
> **Compliance:** 100% Original Intellectual Property. Zero vendor cloning.

---

## 1. Brand & Aesthetic Philosophy

OptiFlow Vision Studio is designed for machine vision engineers, automation specialists, and robotics integrators. It combines the rigorous precision of industrial automation with the fluid, responsive power of modern reactive user interfaces.

### Core Visual Pillars:
1. **Engineered Precision**: Crisp geometry, clean borders, strict typography alignment, and high contrast for factory floor displays.
2. **Instant Status Comprehension**: Color is strictly semantic. Statuses, data types, and execution phases are legible from across a control room.
3. **Low Fatigue Dark & Studio Light Themes**: Deep Obsidian dark theme prevents optical strain in dark camera enclosures; Studio Light theme provides maximum clarity under factory lighting.
4. **Dockable Modularity**: Windows, panels, docks, and filmstrips can be docked, tabbed, floated, or hidden to fit multi-monitor vision rigs.

---

## 2. Color Palette & Semantic Tokens

### 2.1 Dark Theme (Obsidian Industrial)
| Token Name | Hex Code | Description & Usage |
| :--- | :--- | :--- |
| `--opti-bg-root` | `#0b0c10` | App root window background |
| `--opti-bg-dock` | `#131418` | Docking panel and sidebar background |
| `--opti-bg-card` | `#1a1b21` | Tool cards, node bodies, and HUD panels |
| `--opti-bg-card-hover` | `#22242c` | Interactive card hover state |
| `--opti-bg-input` | `#0f1013` | Text inputs, dropdowns, and sliders |
| `--opti-border-subtle` | `#252630` | Panel dividers and dock boundaries |
| `--opti-border-focus` | `#3c3e4f` | Active tab and focused boundaries |
| `--opti-text-primary` | `#f4f4f7` | High-contrast body and titles |
| `--opti-text-secondary` | `#9ca3af` | Descriptions, port labels, captions |
| `--opti-text-muted` | `#606474` | Timestamps, disabled text, hotkeys |

### 2.2 Light Theme (Studio Silver)
| Token Name | Hex Code | Description & Usage |
| :--- | :--- | :--- |
| `--opti-light-bg-root` | `#f1f3f7` | App root window background |
| `--opti-light-bg-dock` | `#ffffff` | Docking panel and sidebar background |
| `--opti-light-bg-card` | `#f8fafc` | Tool cards, node bodies, and panels |
| `--opti-light-border` | `#cbd5e1` | Dividing lines and dock boundaries |
| `--opti-light-text-pri` | `#0f172a` | High-contrast dark charcoal text |
| `--opti-light-text-sec` | `#475569` | Secondary captions and descriptions |

### 2.3 Semantic Accents & Status Indicators
| Accent Role | Dark Token | Light Token | Usage |
| :--- | :--- | :--- | :--- |
| **Primary Cyan** | `#00f0ff` | `#0284c7` | Brand identity, primary buttons, Image wires |
| **Success Emerald** | `#10b981` | `#059669` | Pass verdict, OK status, Region data type |
| **Warning Amber** | `#f59e0b` | `#d97706` | Approaching tolerance limits, Point2D wires |
| **Danger Crimson** | `#ef4444` | `#dc2626` | Inspection FAIL, Error status, broken connections |
| **Logic Violet** | `#a855f7` | `#7c3aed` | Boolean variables, decision branches, gates |

---

## 3. Data-Flow Port Type Color System

In OptiFlow data-flow graphs, ports and connecting bezier wires are strictly typed and colored according to domain type semantics:

| Data Type | Handle Color | Description |
| :--- | :--- | :--- |
| **Image** | `#00f0ff` (Cyan) | Raw 2D/3D pixel buffers (Mono8, RGB, Float32) |
| **Region** | `#10b981` (Emerald) | Binary masks, Run-Length Encoded regions |
| **Point2D** | `#eab308` (Yellow) | Coordinate $(X, Y)$ vectors, centers |
| **Rectangle2D** | `#f59e0b` (Amber) | Bounding boxes $(X, Y, W, H)$ |
| **Circle2D** | `#84cc16` (Lime) | Circular features $(CX, CY, R)$ |
| **Number** | `#38bdf8` (Sky) | Floating-point engineering metrics (mm, deg) |
| **Integer** | `#60a5fa` (Blue) | Counts, indexes, hardware trigger registers |
| **Boolean** | `#a855f7` (Purple) | Pass/Fail flags, enable conditions |
| **String** | `#94a3b8` (Slate) | Barcode text, recipe names, PLC packets |
| **Array** | `#f97316` (Orange) | Collections of Blobs, Segments, or Points |
| **Any** | `#cbd5e1` (Light Gray) | Generic passthrough ports |

---

## 4. Typography & Spacing Grid

- **UI Interface Font**: `'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, sans-serif`
- **Technical & Telemetry Monospace**: `'JetBrains Mono', Consolas, 'Courier New', monospace`
- **Base Grid**: $4\text{ px}$ increments ($4, 8, 12, 16, 20, 24, 32\text{ px}$).
- **Panel Header Height**: $32\text{ px}$ (compact, maximizing viewport real estate).
- **Toolbar Ribbon Height**: $38\text{ px}$.
- **Status Footer Height**: $26\text{ px}$.

---

## 5. Docking & Workspace Layout Hierarchy

```
+------------------------------------------------------------------------------------+
| Top Menu Bar (File, Edit, View, Program, Tools, Workspaces, Help)        [Offline] |
+------------------------------------------------------------------------------------+
| Ribbon Toolbar (Run Once, Continuous, Step, Stop | Layouts | Snap | Zoom | Hotkeys)|
+-------------------+--------------------------------------------+-------------------+
| LEFT DOCK         | CENTRAL PROGRAM CANVAS                     | RIGHT DOCK        |
| [Catalog | Trees] | +----------------------------------------+ | [Inspect | Data]  |
|                   | | Tabs: Main [MAIN] | Macro 1 | Macro 2  | | |                   |
| - Filter Catalog  | +----------------------------------------+ | - Property Inputs |
| - Project Explorer| | React Flow DAG Canvas                  | | - Live Outputs    |
| - Camera Streams  | |                                        | | - Image Viewer    |
| - Global Vars     | | (Nodes with Typed Ports & OK Badges)   | | - Pixel Probe HUD |
|                   | +----------------------------------------+ | - 256-Bin Histo   |
|                   | Filmstrip: [Img 1] [Img 2] [Img 3] [+]   | |                   |
+-------------------+--------------------------------------------+-------------------+
| BOTTOM DOCK (Console Output | Errors & Diagnostics | Performance Profiler Waterfall)|
+------------------------------------------------------------------------------------+
| Status Footer (Latency: 0.1ms | Coords: X:1920 Y:1080 | Intensity: 242 | RAM: 92MB) |
+------------------------------------------------------------------------------------+
```

---

## 6. Industrial Hotkey Map

- `F5`: Run Once (Execute DAG Topological Order)
- `Shift+F5`: Stop Continuous Run
- `F10`: Step Next Node
- `Ctrl+P`: Instant Filter & Command Palette
- `Ctrl+S`: Save Project File (`.vsproj`)
- `Ctrl+O`: Open Existing Project File
- `Ctrl+N`: New Inspection Project
- `Ctrl+Z`: Undo Last Graph Change
- `Ctrl+Y`: Redo Last Graph Change
- `Ctrl+C` / `Ctrl+V`: Copy / Paste Selected Nodes
- `Delete` / `Backspace`: Remove Selected Node / Edge
