import { create } from 'zustand';
import { useSidecarStore } from './useSidecarStore';

export interface ProjectMetadata {
  name: str;
  description: string;
  author: string;
  version: string;
  tags: string[];
}

export interface ProjectSettings {
  target_fps: number;
  mode: 'FULL_EDITOR' | 'SIMPLE_MODE' | 'RUNTIME_PLAYER';
  default_language: string;
  stop_on_error: boolean;
  diagnostic_logging: boolean;
  active_program_id: string;
}

export interface OptiFlowProjectData {
  format_version: string;
  generator: string;
  created_at: number;
  updated_at: number;
  metadata: ProjectMetadata;
  settings: ProjectSettings;
  programs: any[];
  macros: any[];
  global_variables: Record<string, any>;
  operator_screens: any[];
}

interface ProjectState {
  filePath: string | null;
  project: OptiFlowProjectData | null;
  isDirty: boolean;
  isLoading: boolean;
  newProjectModalOpen: boolean;
  setNewProjectModalOpen: (open: boolean) => void;
  setProject: (project: OptiFlowProjectData, filePath?: string | null) => void;
  createNewProject: (name: string, language?: string) => Promise<boolean>;
  openProjectFromFile: (filePath: string) => Promise<boolean>;
  saveProjectToFile: (targetPath?: string) => Promise<boolean>;
  fetchCurrentProject: () => Promise<boolean>;
}

export const useProjectStore = create<ProjectState>((set, get) => ({
  filePath: null,
  project: null,
  isDirty: false,
  isLoading: false,
  newProjectModalOpen: false,

  setNewProjectModalOpen: (open: boolean) => set({ newProjectModalOpen: open }),

  setProject: (project: OptiFlowProjectData, filePath: string | null = null) =>
    set({ project, filePath, isDirty: false }),

  createNewProject: async (name: string, language: string = 'vi') => {
    const { baseUrl } = useSidecarStore.getState();
    set({ isLoading: true });
    try {
      const resp = await fetch(
        `${baseUrl}/api/project/new?name=${encodeURIComponent(name)}&language=${language}`,
        { method: 'POST' }
      );
      if (resp.ok) {
        const data = await resp.json();
        set({ project: data.project, filePath: null, isDirty: false, isLoading: false });
        return true;
      }
    } catch (e) {
      console.error('Failed to create new project:', e);
    }
    set({ isLoading: false });
    return false;
  },

  openProjectFromFile: async (filePath: string) => {
    const { baseUrl } = useSidecarStore.getState();
    set({ isLoading: true });
    try {
      const resp = await fetch(`${baseUrl}/api/project/open`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ file_path: filePath }),
      });
      if (resp.ok) {
        const data = await resp.json();
        set({ project: data.project, filePath: data.file_path, isDirty: false, isLoading: false });
        return true;
      }
    } catch (e) {
      console.error('Failed to open project:', e);
    }
    set({ isLoading: false });
    return false;
  },

  saveProjectToFile: async (targetPath?: string) => {
    const { baseUrl } = useSidecarStore.getState();
    const { project, filePath } = get();
    const savePath = targetPath || filePath;
    if (!savePath) return false;

    set({ isLoading: true });
    try {
      const resp = await fetch(`${baseUrl}/api/project/save`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ file_path: savePath, project_data: project }),
      });
      if (resp.ok) {
        const data = await resp.json();
        set({ filePath: data.file_path, isDirty: false, isLoading: false });
        return true;
      }
    } catch (e) {
      console.error('Failed to save project:', e);
    }
    set({ isLoading: false });
    return false;
  },

  fetchCurrentProject: async () => {
    const { baseUrl } = useSidecarStore.getState();
    try {
      const resp = await fetch(`${baseUrl}/api/project/current`);
      if (resp.ok) {
        const data = await resp.json();
        set({ project: data.project, filePath: data.file_path });
        return true;
      }
    } catch {
      // offline
    }
    return false;
  },
}));
