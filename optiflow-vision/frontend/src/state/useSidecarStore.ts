import { create } from 'zustand';

interface SidecarState {
  baseUrl: string;
  isOnline: boolean;
  pid: number | null;
  uptimeSec: number;
  memoryMb: number;
  cpuPercent: number;
  latencyMs: number;
  cachedImagesCount: number;
  lastPingTime: number;
  setBaseUrl: (url: string) => void;
  checkHealth: () => Promise<boolean>;
}

export const useSidecarStore = create<SidecarState>((set, get) => ({
  baseUrl: 'http://127.0.0.1:5200',
  isOnline: false,
  pid: null,
  uptimeSec: 0,
  memoryMb: 0,
  cpuPercent: 0,
  latencyMs: 0,
  cachedImagesCount: 0,
  lastPingTime: 0,

  setBaseUrl: (baseUrl: string) => set({ baseUrl }),

  checkHealth: async () => {
    const { baseUrl } = get();
    const t0 = performance.now();
    try {
      const resp = await fetch(`${baseUrl}/api/health`, { method: 'GET' });
      const latency = Math.round(performance.now() - t0);
      if (resp.ok) {
        const data = await resp.json();
        set({
          isOnline: true,
          pid: data.pid,
          uptimeSec: data.uptime_sec,
          memoryMb: data.memory_mb,
          cpuPercent: data.cpu_percent,
          cachedImagesCount: data.cached_images_count,
          latencyMs: latency,
          lastPingTime: Date.now(),
        });
        return true;
      }
    } catch {
      // offline
    }
    set({ isOnline: false, latencyMs: 0 });
    return false;
  },
}));
