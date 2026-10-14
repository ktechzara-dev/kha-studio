# VisionStudio Headless Industrial Runtime Guide

This document describes the deployment, configuration, orchestration, and monitoring of **VisionStudio.Runtime**, the headless industrial execution engine designed for Cognex VisionPro / Zebra Aurora Vision-class factory floors.

---

## 1. Overview & Architecture

`VisionStudio.Runtime` is a standalone daemon running on Windows (as a Windows Service) or Linux (as a systemd service or Docker container).

Key characteristics:
- **Zero GUI Overhead**: Pure headless execution with maximum CPU/GPU efficiency for sub-millisecond vision cycles.
- **Multi-Station Orchestration**: Runs multiple independent vision heads (stations) concurrently, each with its own camera feed, pipeline DAG, recipe set, and state machine.
- **Package-Based Deployment (`.vspkg`)**: Bundles pipeline graphs, deep learning `.vsmodel` / ONNX weights, recipes, and calibration files into cryptographically signed zip archives.
- **Hot-Reloading**: Seamlessly deploys and switches pipeline versions without interrupting ongoing line cycles or restarting the process.
- **IIoT & Cloud Connectivity**: Embedded REST API, OpenAPI/Swagger docs, OPC UA Server, MQTT Sparkplug B client, and standard Prometheus metrics.

---

## 2. Docker & Container Deployment

### Running with Docker

```bash
# Build the image
docker build -t visionstudio-runtime:latest -f src/VisionStudio.Runtime/Dockerfile .

# Run the container mapping HTTP (5000) and OPC UA (4840)
docker run -d \
  --name vs-runtime \
  --restart unless-stopped \
  -p 5000:5000 \
  -p 4840:4840 \
  -v /opt/visionstudio/data:/data \
  visionstudio-runtime:latest
```

### Docker Compose (`docker-compose.yml`)

```yaml
version: '3.8'

services:
  vision-runtime:
    image: visionstudio-runtime:latest
    container_index: 1
    ports:
      - "5000:5000"
      - "4840:4840"
    volumes:
      - ./data:/data
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - VISIONSTUDIO_DATA_DIR=/data
    restart: always

  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
```

---

## 3. Windows Service Deployment

To install `VisionStudio.Runtime` as a native Windows Service starting automatically at boot:

```cmd
:: 1. Publish Release binaries
dotnet publish src\VisionStudio.Runtime\VisionStudio.Runtime.csproj -c Release -o C:\VisionStudio\Runtime

:: 2. Register Windows Service
sc create VisionStudioRuntime binPath= "C:\VisionStudio\Runtime\VisionStudio.Runtime.exe" start= auto

:: 3. Start Service
sc start VisionStudioRuntime
```

---

## 4. REST Management API Reference

### Health & Readiness
- `GET /health`: Basic liveness probe returning HTTP 200 OK.
- `GET /ready`: Readiness probe indicating whether stations and packages are loaded.
- `GET /metrics`: Standard Prometheus metrics scrape format.
- `GET /api/docs`: Interactive Swagger / OpenAPI documentation UI.

### Stations & Inspections
- `GET /api/v1/stations`: List all active inspection stations and their statistical counters.
- `GET /api/v1/stations/{id}`: Detailed information and throughput of a specific station.
- `POST /api/v1/stations/{id}/trigger`: Software trigger to execute one inspection cycle.
  - Body (optional JSON):
    ```json
    {
      "recipeName": "Job_02",
      "imageBase64": "..."
    }
    ```
- `GET /api/v1/stations/{id}/result`: Fetch last inspection outcome (`InspectionRecord`).
- `GET /api/v1/stations/{id}/image`: Fetch last captured image in JPEG format (`image/jpeg`).
- `POST /api/v1/stations/{id}/recipe`: Switch the default recipe of a station at runtime.

### Project & Package Deployment
- `POST /api/v1/projects/deploy`: Upload a `.vspkg` deployment archive via multipart form. The runtime automatically verifies SHA-256 integrity, calculates a diff against the running version, and reloads station pipelines gracefully.

---

## 5. Prometheus Scrape Configuration

Add the following scrape job to your factory's `prometheus.yml`:

```yaml
scrape_configs:
  - job_name: 'visionstudio_runtime'
    scrape_interval: 2s
    static_configs:
      - targets: ['localhost:5000']
    metrics_path: '/metrics'
```

### Metrics Exported:
| Metric | Type | Description |
|---|---|---|
| `visionstudio_uptime_seconds` | Gauge | Runtime uptime in seconds |
| `visionstudio_stations_count` | Gauge | Number of active camera stations |
| `visionstudio_inspections_total` | Counter | Total inspections executed per station |
| `visionstudio_inspections_pass` | Counter | Total inspections passed per station |
| `visionstudio_inspections_fail` | Counter | Total inspections failed per station |
| `visionstudio_cycle_time_ms` | Gauge | Execution time of the last inspection |
| `visionstudio_cycle_time_avg_ms` | Gauge | Rolling average cycle time in ms |
| `visionstudio_fps` | Gauge | Inspection throughput (frames per second) |
| `visionstudio_yield_percentage` | Gauge | Pass rate yield percentage (0 - 100%) |
