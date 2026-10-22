import { create } from 'zustand';

export interface ConsoleLogEntry {
  id: string;
  timestamp: string;
  level: 'info' | 'warn' | 'error' | 'success';
  message: string;
  source?: string;
}

export interface ProgramTab {
  id: string;
  name: string;
  type: 'MAIN' | 'MACRO';
  isDirty?: boolean;
}

interface DockState {
  // 1. Docks
  leftDock: {
    isOpen: boolean;
    activeTab: 'catalog' | 'explorer';
    width: number;
  };
  rightDock: {
    isOpen: boolean;
    activeTab: 'inspector' | 'preview';
    width: number;
  };
  bottomDock: {
    isOpen: boolean;
    activeTab: 'output' | 'errors' | 'performance';
    height: number;
  };
  filmstrip: {
    isOpen: boolean;
  };

  // 2. Program Tabs
  tabs: ProgramTab[];
  activeTabId: string;

  // 3. Workspaces & Themes
  currentWorkspace: string;
  theme: 'dark' | 'light';

  // 4. Command Palette
  isCommandPaletteOpen: boolean;

  // 5. Console & Diagnostics
  consoleLogs: ConsoleLogEntry[];

  // Central Canvas Split (Graph vs Image)
  centerSplitPercent: number;
  setCenterSplitPercent: (percent: number) => void;

  // Actions
  setLeftDock: (partial: Partial<DockState['leftDock']>) => void;
  setRightDock: (partial: Partial<DockState['rightDock']>) => void;
  setBottomDock: (partial: Partial<DockState['bottomDock']>) => void;
  toggleLeftDock: () => void;
  toggleRightDock: () => void;
  toggleBottomDock: () => void;
  setFilmstripOpen: (isOpen: boolean) => void;

  addTab: (name: string, type?: 'MAIN' | 'MACRO') => void;
  closeTab: (id: string) => void;
  setActiveTabId: (id: string) => void;

  setWorkspace: (name: string) => void;
  resetLayout: () => void;
  setTheme: (theme: 'dark' | 'light') => void;
  toggleTheme: () => void;

  setCommandPaletteOpen: (open: boolean) => void;
  addLog: (level: 'info' | 'warn' | 'error' | 'success', message: string, source?: string) => void;
  clearLogs: () => void;
}

const DEFAULT_LEFT = { isOpen: true, activeTab: 'catalog' as const, width: 230 };
const DEFAULT_RIGHT = { isOpen: true, activeTab: 'inspector' as const, width: 290 };
const DEFAULT_BOTTOM = { isOpen: true, activeTab: 'output' as const, height: 140 };

export const useDockStore = create<DockState>((set, get) => ({
  leftDock: { ...DEFAULT_LEFT },
  rightDock: { ...DEFAULT_RIGHT },
  bottomDock: { ...DEFAULT_BOTTOM },
  filmstrip: { isOpen: true },

  tabs: [
    { id: 'tab_main', name: 'Main Inspection [MAIN]', type: 'MAIN' },
    { id: 'tab_macro_bottle', name: 'Macro: BottleCap Inspector', type: 'MACRO' },
  ],
  activeTabId: 'tab_main',

  currentWorkspace: 'Default',
  theme: 'dark',
  isCommandPaletteOpen: false,

  consoleLogs: [
    {
      id: 'log_init',
      timestamp: new Date().toLocaleTimeString(),
      level: 'info',
      message: 'OptiFlow Vision Studio IDE initialized successfully.',
      source: 'System',
    },
    {
      id: 'log_engine',
      timestamp: new Date().toLocaleTimeString(),
      level: 'success',
      message: 'FastAPI Computational Sidecar connected on 127.0.0.1:5200 (Latency: <1ms).',
      source: 'SidecarIPC',
    },
    {
      id: 'log_dag',
      timestamp: new Date().toLocaleTimeString(),
      level: 'info',
      message: 'Topological DAG pipeline ready with 25 basic inspection filters.',
      source: 'PipelineExecutor',
    },
  ],

  centerSplitPercent: 50,
  setCenterSplitPercent: (percent: number) => set({ centerSplitPercent: Math.max(15, Math.min(85, percent)) }),

  setLeftDock: (partial) =>
    set({ leftDock: { ...get().leftDock, ...partial } }),

  setRightDock: (partial) =>
    set({ rightDock: { ...get().rightDock, ...partial } }),

  setBottomDock: (partial) =>
    set({ bottomDock: { ...get().bottomDock, ...partial } }),

  toggleLeftDock: () =>
    set({ leftDock: { ...get().leftDock, isOpen: !get().leftDock.isOpen } }),

  toggleRightDock: () =>
    set({ rightDock: { ...get().rightDock, isOpen: !get().rightDock.isOpen } }),

  toggleBottomDock: () =>
    set({ bottomDock: { ...get().bottomDock, isOpen: !get().bottomDock.isOpen } }),

  setFilmstripOpen: (isOpen) =>
    set({ filmstrip: { isOpen } }),

  addTab: (name, type = 'MACRO') => {
    const id = `tab_${Date.now()}`;
    const newTab: ProgramTab = { id, name, type };
    set({ tabs: [...get().tabs, newTab], activeTabId: id });
    get().addLog('info', `Created new program tab: "${name}"`, 'Workspace');
  },

  closeTab: (id) => {
    const { tabs, activeTabId } = get();
    if (tabs.length <= 1) return; // Always keep at least 1 tab
    const nextTabs = tabs.filter((t) => t.id !== id);
    const nextActive = activeTabId === id ? nextTabs[0].id : activeTabId;
    set({ tabs: nextTabs, activeTabId: nextActive });
  },

  setActiveTabId: (id) => set({ activeTabId: id }),

  setWorkspace: (name) => {
    if (name === 'Default') {
      set({
        currentWorkspace: name,
        leftDock: { ...DEFAULT_LEFT },
        rightDock: { ...DEFAULT_RIGHT },
        bottomDock: { ...DEFAULT_BOTTOM },
        filmstrip: { isOpen: true },
      });
    } else if (name === 'Inspection & Tuning') {
      set({
        currentWorkspace: name,
        leftDock: { isOpen: false, activeTab: 'catalog', width: 280 },
        rightDock: { isOpen: true, activeTab: 'preview', width: 440 },
        bottomDock: { isOpen: true, activeTab: 'performance', height: 200 },
        filmstrip: { isOpen: true },
      });
    } else if (name === 'Graph Engineering') {
      set({
        currentWorkspace: name,
        leftDock: { isOpen: true, activeTab: 'catalog', width: 320 },
        rightDock: { isOpen: true, activeTab: 'inspector', width: 320 },
        bottomDock: { isOpen: false, activeTab: 'output', height: 180 },
        filmstrip: { isOpen: false },
      });
    } else if (name === 'Operator View') {
      set({
        currentWorkspace: name,
        leftDock: { isOpen: false, activeTab: 'catalog', width: 280 },
        rightDock: { isOpen: true, activeTab: 'preview', width: 500 },
        bottomDock: { isOpen: false, activeTab: 'output', height: 180 },
        filmstrip: { isOpen: true },
      });
    }
    get().addLog('info', `Switched layout workspace to: ${name}`, 'Workspace');
  },

  resetLayout: () => {
    set({
      currentWorkspace: 'Default',
      leftDock: { ...DEFAULT_LEFT },
      rightDock: { ...DEFAULT_RIGHT },
      bottomDock: { ...DEFAULT_BOTTOM },
      filmstrip: { isOpen: true },
    });
    get().addLog('info', 'Workspace layout reset to default golden ratio.', 'Layout');
  },

  setTheme: (theme) => set({ theme }),
  toggleTheme: () => set({ theme: get().theme === 'dark' ? 'light' : 'dark' }),

  setCommandPaletteOpen: (isCommandPaletteOpen) => set({ isCommandPaletteOpen }),

  addLog: (level, message, source = 'System') => {
    const newEntry: ConsoleLogEntry = {
      id: `log_${Date.now()}_${Math.random().toString(36).substring(2, 6)}`,
      timestamp: new Date().toLocaleTimeString(),
      level,
      message,
      source,
    };
    set({ consoleLogs: [newEntry, ...get().consoleLogs.slice(0, 99)] });
  },

  clearLogs: () => set({ consoleLogs: [] }),
}));
