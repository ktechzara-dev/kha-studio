# VisionStudio HMI & Web HMI Architecture

## 1. Overview
The **VisionStudio.Hmi** module provides industrial operator screen design and real-time Web HMI serving for factory deployments. It enables operators and engineers to interact with active inspection stations both on local IPC touch screens and remotely on tablets or control room browsers.

---

## 2. Architecture & Components

### 2.1 Domain Model (`VisionStudio.Hmi.Model`)
- **`HmiProject`**: Top-level project model serialized to `.vshmi` (JSON schema `1.0.0`).
- **`HmiPage`**: Multi-page layout (e.g. Overview, SPC Quality, Engineering Setup) with a responsive 12-column grid system.
- **`HmiWidget`**: Individual UI widgets placed on the grid:
  - `LiveImage`: Real-time camera stream and vision tool defect overlays.
  - `OkNgBadge`: Pass/Fail status banner with visual pulse animation.
  - `NumericDisplay`: Large industrial digital readout with unit and formatting.
  - `Gauge`: Radial/dial tolerance gauge with low/high threshold alarms.
  - `TrendChart`: Real-time measurement trend line with SPC tolerance limits.
  - `Histogram`: Measurement frequency distribution bars.
  - `Table`: Recent product inspection and defect records.
  - `Button`: Operator trigger, reject ack, recipe switch, counter reset.
  - `TextInput`: Barcode/lot scanning, operator comments, parameter override.
  - `RecipeSelector`: Active job/recipe changer.
  - `UserLogin`: Role elevation (Operator, Technician, Engineer, Admin).
  - `LogViewer`: Real-time terminal diagnostic log viewer.

### 2.2 Security & Role-Based Access Control (RBAC)
Widgets and entire pages support minimum role restrictions:
- **Operator**: Standard monitoring, pass/fail review, live camera feed, manual trigger.
- **Technician**: Recipe switching, tolerance adjustments, alarm acknowledgement.
- **Engineer**: Full designer access, binding configuration, camera gain/exposure settings.
- **Admin**: Counter reset, system calibration, user management.

---

## 3. Web HMI Server (`VisionStudio.Hmi.Web`)
The Web HMI is hosted by an embedded ASP.NET Core Kestrel web server:
- **Port**: Configurable (default `5050` or `http://0.0.0.0:5050`).
- **SignalR Duplex Hub (`/hmiHub`)**: Real-time push notifications of page snapshots, widget telemetry, and button triggers without browser polling.
- **REST Endpoints**:
  - `GET /`: Responsive HTML5/CSS3 Single-Page Application (touch-friendly, fully offline compliant).
  - `GET /health`: Liveness and readiness health probe.
  - `GET /api/hmi/layout`: Returns current `HmiProject` structure.
  - `GET /api/hmi/snapshot`: Returns real-time evaluation snapshot for specified `pageId`.
  - `POST /api/hmi/action`: Dispatches user actions (trigger, recipe change, reset) to the execution engine.

---

## 4. WPF HMI Designer (`VisionStudio.App`)
The desktop application provides a visual HMI designer window:
- **Design Mode**: Drag-and-drop / click-to-add widgets, resize column/row spans, configure bindings, low/high limits, and permissions.
- **Run Mode**: Live interactive operator view running in real time.
- **Web HMI Launcher**: One-click startup of the embedded Kestrel server with immediate browser launch.
