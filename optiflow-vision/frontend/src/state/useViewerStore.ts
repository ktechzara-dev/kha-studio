import { create } from 'zustand';
import { useSidecarStore } from './useSidecarStore';

export interface ImageMeta {
  id: string;
  width: number;
  height: number;
  channels: number;
  color_space: string;
  dtype: string;
  file_path?: string;
  memory_size_bytes: number;
}

export interface PixelProbeResult {
  x: number;
  y: number;
  in_bounds: boolean;
  channels: number[];
  intensity_text: string;
  neighborhood_7x7: number[][];
}

export interface HistogramData {
  image_id: string;
  bins: number[];
  min_val: number;
  max_val: number;
  mean_val: number;
  std_val: number;
  percentile_25: number;
  percentile_50: number;
  percentile_75: number;
}

export type ViewerTool = 'pan' | 'measure' | 'roi' | 'caliper' | 'probe';

export interface MeasurementData {
  p1: { x: number; y: number } | null;
  p2: { x: number; y: number } | null;
  distPx: number;
  distMm: number;
  deltaX: number;
  deltaY: number;
  angleDeg: number;
}

export interface UserRoiData {
  x: number;
  y: number;
  width: number;
  height: number;
  active: boolean;
}

export interface CaliperRayData {
  p1: { x: number; y: number } | null;
  p2: { x: number; y: number } | null;
  edgePos: { x: number; y: number } | null;
  contrast: number;
}

interface ViewerState {
  activeImageId: string;
  activeImageMeta: ImageMeta | null;
  scale: number;
  offset: { x: number; y: number };
  cursorPos: { x: number; y: number } | null;
  probeResult: PixelProbeResult | null;
  histogram: HistogramData | null;
  showHistogram: boolean;
  showProbe: boolean;
  showOverlays: boolean;
  activeTool: ViewerTool;
  pipelineOverlays: any[];
  availableImages: ImageMeta[];

  // Interactive Tools Data
  measurement: MeasurementData;
  userRoi: UserRoiData;
  caliperRay: CaliperRayData;
  pickingTarget: 'roi' | 'point' | 'template' | null;
  trainedTemplate: {
    isTrained: boolean;
    name: string;
    roi: { x: number; y: number; width: number; height: number };
    minScore: number;
    angleRange: number;
    posToleranceMm: number;
    trainedAt: string;
  } | null;

  setScale: (scale: number) => void;
  setOffset: (offset: { x: number; y: number }) => void;
  setCursorPos: (pos: { x: number; y: number } | null) => void;
  setShowHistogram: (show: boolean) => void;
  setShowProbe: (show: boolean) => void;
  setShowOverlays: (show: boolean) => void;
  setActiveTool: (tool: ViewerTool) => void;
  setPipelineOverlays: (overlays: any[]) => void;
  setPickingTarget: (target: 'roi' | 'point' | 'template' | null) => void;
  setTrainedTemplate: (tmpl: any) => void;
  setActiveImageId: (id: string) => void;
  setMeasurement: (m: MeasurementData) => void;
  setUserRoi: (roi: UserRoiData) => void;
  setCaliperRay: (ray: CaliperRayData) => void;
  clearInteractiveTools: () => void;

  fetchImagesList: () => Promise<void>;
  probePixel: (x: number, y: number) => Promise<void>;
  fetchHistogram: () => Promise<void>;
  resetView: () => void;
  fitToView: (viewportWidth: number, viewportHeight: number) => void;
}

const DEFAULT_SAMPLE_IMAGES: ImageMeta[] = [
  { id: 'sample_bottle', width: 1936, height: 1216, channels: 1, color_space: 'GRAY', dtype: 'uint8', memory_size_bytes: 2354176 },
  { id: 'sample_pcb', width: 1600, height: 1200, channels: 3, color_space: 'BGR', dtype: 'uint8', memory_size_bytes: 5760000 },
  { id: 'sample_calib', width: 1936, height: 1216, channels: 1, color_space: 'GRAY', dtype: 'uint8', memory_size_bytes: 2354176 },
  { id: 'sample_pharma', width: 1920, height: 1080, channels: 3, color_space: 'BGR', dtype: 'uint8', memory_size_bytes: 6220800 },
  { id: 'sample_barcode', width: 1920, height: 1080, channels: 3, color_space: 'BGR', dtype: 'uint8', memory_size_bytes: 6220800 },
  { id: 'sample_gear', width: 1920, height: 1080, channels: 1, color_space: 'GRAY', dtype: 'uint8', memory_size_bytes: 2073600 },
];

export const useViewerStore = create<ViewerState>((set, get) => ({
  activeImageId: 'sample_bottle',
  activeImageMeta: DEFAULT_SAMPLE_IMAGES[0],
  scale: 0.65,
  offset: { x: 40, y: 30 },
  cursorPos: null,
  probeResult: null,
  histogram: null,
  showHistogram: true,
  showProbe: true,
  showOverlays: true,
  activeTool: 'pan',
  pipelineOverlays: [],
  availableImages: DEFAULT_SAMPLE_IMAGES,

  measurement: { p1: null, p2: null, distPx: 0, distMm: 0, deltaX: 0, deltaY: 0, angleDeg: 0 },
  userRoi: { x: 300, y: 200, width: 800, height: 600, active: false },
  caliperRay: { p1: null, p2: null, edgePos: null, contrast: 0 },

  pickingTarget: null,
  trainedTemplate: {
    isTrained: true,
    name: 'Unilever_Cap_Master_SKU510',
    roi: { x: 400, y: 340, width: 140, height: 180 },
    minScore: 0.85,
    angleRange: 15,
    posToleranceMm: 5.0,
    trainedAt: '17/10/2026 22:30:00',
  },

  setScale: (scale: number) => set({ scale: Math.max(0.05, Math.min(25.0, scale)) }),
  setOffset: (offset: { x: number; y: number }) => set({ offset }),
  setCursorPos: (cursorPos) => set({ cursorPos }),
  setShowHistogram: (showHistogram) => set({ showHistogram }),
  setShowProbe: (showProbe) => set({ showProbe }),
  setShowOverlays: (showOverlays) => set({ showOverlays }),
  setActiveTool: (activeTool: ViewerTool) => set({ activeTool }),
  setPipelineOverlays: (pipelineOverlays) => set({ pipelineOverlays }),
  setPickingTarget: (pickingTarget) => set({ pickingTarget }),
  setTrainedTemplate: (trainedTemplate) => set({ trainedTemplate }),
  setMeasurement: (measurement) => set({ measurement }),
  setUserRoi: (userRoi) => set({ userRoi }),
  setCaliperRay: (caliperRay) => set({ caliperRay }),
  clearInteractiveTools: () => set({
    measurement: { p1: null, p2: null, distPx: 0, distMm: 0, deltaX: 0, deltaY: 0, angleDeg: 0 },
    caliperRay: { p1: null, p2: null, edgePos: null, contrast: 0 },
    userRoi: { x: 300, y: 200, width: 800, height: 600, active: false },
    pickingTarget: null,
  }),

  setActiveImageId: (id: string) => {
    set({ activeImageId: id });
    const { availableImages } = get();
    const meta = availableImages.find((img) => img.id === id) || {
      id,
      width: 1920,
      height: 1080,
      channels: 3,
      color_space: 'BGR',
      dtype: 'uint8',
      memory_size_bytes: 6220800,
    };
    set({ activeImageMeta: meta, probeResult: null });
    get().fetchHistogram();
  },

  fetchImagesList: async () => {
    const { baseUrl } = useSidecarStore.getState();
    try {
      const resp = await fetch(`${baseUrl}/api/images`);
      if (resp.ok) {
        const data = await resp.json();
        const images: ImageMeta[] = data.images;
        const currentActive = get().activeImageId;
        const meta = images.find((m) => m.id === currentActive) || images[0] || null;
        set({
          availableImages: images,
          activeImageId: meta ? meta.id : currentActive,
          activeImageMeta: meta,
        });
        if (meta) {
          get().fetchHistogram();
        }
      }
    } catch (e) {
      console.error('Failed to fetch images:', e);
    }
  },

  probePixel: async (x: number, y: number) => {
    const { baseUrl } = useSidecarStore.getState();
    const { activeImageId } = get();
    if (!activeImageId) return;

    try {
      const resp = await fetch(
        `${baseUrl}/api/image/probe?image_id=${encodeURIComponent(activeImageId)}&x=${x}&y=${y}`
      );
      if (resp.ok) {
        const data: PixelProbeResult = await resp.json();
        set({ probeResult: data });
      }
    } catch {
      // ignore
    }
  },

  fetchHistogram: async () => {
    const { baseUrl } = useSidecarStore.getState();
    const { activeImageId } = get();
    if (!activeImageId) return;

    try {
      const resp = await fetch(
        `${baseUrl}/api/image/histogram/${encodeURIComponent(activeImageId)}`
      );
      if (resp.ok) {
        const data: HistogramData = await resp.json();
        set({ histogram: data });
      }
    } catch {
      // ignore
    }
  },

  resetView: () => {
    set({ scale: 1.0, offset: { x: 0, y: 0 } });
  },

  fitToView: (viewportWidth: number, viewportHeight: number) => {
    const { activeImageMeta } = get();
    if (!activeImageMeta || viewportWidth <= 0 || viewportHeight <= 0) return;

    const pad = 30;
    const availW = viewportWidth - pad * 2;
    const availH = viewportHeight - pad * 2;

    const scaleX = availW / activeImageMeta.width;
    const scaleY = availH / activeImageMeta.height;
    const fitScale = Math.min(scaleX, scaleY);

    const centerX = (viewportWidth - activeImageMeta.width * fitScale) / 2;
    const centerY = (viewportHeight - activeImageMeta.height * fitScale) / 2;

    set({ scale: fitScale, offset: { x: centerX, y: centerY } });
  },
}));
