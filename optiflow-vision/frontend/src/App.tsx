import React, { useEffect } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import { TopMenuBar } from './components/Shell/TopMenuBar';
import { ToolbarRibbon } from './components/Shell/ToolbarRibbon';
import { StatusFooter } from './components/Shell/StatusFooter';

import { LeftDock } from './components/Docks/LeftDock';
import { RightDock } from './components/Docks/RightDock';
import { BottomDock } from './components/Docks/BottomDock';
import { ProgramTabBar } from './components/GraphEditor/ProgramTabBar';
import { GraphCanvas } from './components/GraphEditor/GraphCanvas';
import { Filmstrip } from './components/ImageViewer/Filmstrip';
import { ImageViewer } from './components/ImageViewer/ImageViewer';
import { CommandPalette } from './components/CommandPalette/CommandPalette';
import { NewProjectModal } from './components/ProjectModal/NewProjectModal';
import { OperatorDashboard } from './components/HMI/OperatorDashboard';
import { HmiDesigner } from './components/HMI/HmiDesigner';

import { useSidecarStore } from './state/useSidecarStore';
import { useViewerStore } from './state/useViewerStore';
import { useProjectStore } from './state/useProjectStore';
import { useGraphStore } from './state/useGraphStore';
import { useDockStore } from './state/useDockStore';

export const App: React.FC = () => {
  const { checkHealth, isOnline } = useSidecarStore();
  const { fetchImagesList } = useViewerStore();
  const { fetchCurrentProject, saveProjectToFile, setNewProjectModalOpen } = useProjectStore();
  const { fetchFilterDefs, loadGraphFromProject, runOnce, selectedNodeId, deleteNode, nodes, viewLayout } = useGraphStore();
  const { setCommandPaletteOpen, theme, currentWorkspace, centerSplitPercent, setCenterSplitPercent } = useDockStore();
  const splitAreaRef = React.useRef<HTMLDivElement>(null);

  const startResizingCenter = (e: React.MouseEvent) => {
    e.preventDefault();
    if (!splitAreaRef.current) return;
    const rect = splitAreaRef.current.getBoundingClientRect();

    const onMouseMove = (moveEvent: MouseEvent) => {
      const offsetX = moveEvent.clientX - rect.left;
      const percent = Math.max(15, Math.min(85, (offsetX / rect.width) * 100));
      setCenterSplitPercent(percent);
    };

    const onMouseUp = () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  useEffect(() => {
    // Immediate canvas initialization so graph is NEVER empty
    loadGraphFromProject();

    let initialized = false;
    const initApp = async () => {
      const ok = await checkHealth();
      if (ok && !initialized) {
        initialized = true;
        await fetchImagesList();
        await fetchFilterDefs();
        await fetchCurrentProject();
        loadGraphFromProject();
      }
    };

    initApp();

    // Periodic heartbeat (every 2.5s)
    const interval = setInterval(() => {
      initApp();
    }, 2500);

    return () => clearInterval(interval);
  }, []);

  // Global IDE Keyboard Shortcuts Handler
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      // Ignore if user is currently typing inside an input or textarea
      if (
        ['INPUT', 'TEXTAREA', 'SELECT'].includes(
          (e.target as HTMLElement)?.tagName || ''
        )
      ) {
        if (e.key === 'Escape') {
          (e.target as HTMLElement).blur();
        }
        return;
      }

      // F5: Run Once
      if (e.key === 'F5') {
        e.preventDefault();
        runOnce();
      }

      // F10: Step Forward
      if (e.key === 'F10') {
        e.preventDefault();
        runOnce();
      }

      // Ctrl + P: Command Palette
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'p') {
        e.preventDefault();
        setCommandPaletteOpen(true);
      }

      // Ctrl + S: Save Project
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 's') {
        e.preventDefault();
        saveProjectToFile();
      }

      // Ctrl + N: New Project
      if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'n') {
        e.preventDefault();
        setNewProjectModalOpen(true);
      }

      // Delete or Backspace: Remove selected node
      if (e.key === 'Delete') {
        if (selectedNodeId) {
          e.preventDefault();
          deleteNode(selectedNodeId);
        }
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [runOnce, selectedNodeId, deleteNode, saveProjectToFile, setCommandPaletteOpen, setNewProjectModalOpen]);

  return (
    <div className={`flex flex-col w-screen h-screen max-w-full max-h-full overflow-hidden select-none ${
      theme === 'dark' ? 'bg-[#0b0c10] text-[#f4f4f7]' : 'bg-[#f1f3f7] text-[#0f172a]'
    }`}>
      {/* 1. Header & System Menu Bar */}
      <TopMenuBar />

      {/* 2. Operations & Execution Ribbon */}
      <ToolbarRibbon />

      {/* 3. Central Multi-Dock IDE Layout or Full HMI Operator Dashboard */}
      {currentWorkspace === 'Operator View' ? (
        <OperatorDashboard />
      ) : currentWorkspace === 'HMI Designer' ? (
        <HmiDesigner />
      ) : (
        <main className="flex-1 min-w-0 min-h-0 relative w-full overflow-hidden flex flex-row">
          {/* Left Dock: Filter Catalog & Project Explorer */}
          <LeftDock />

          {/* Center Canvas Area: Tabs + React Flow Graph + Filmstrip + Bottom Dock */}
          <div className="flex-1 min-w-0 min-h-0 relative h-full flex flex-col overflow-hidden bg-[#09090c]">
            {/* Program / Macro Tabs */}
            <ProgramTabBar />

            {/* Program Flow DAG Canvas & Live Vision Inspection Window */}
            <div ref={splitAreaRef} className="flex-1 min-w-0 min-h-0 relative w-full overflow-hidden flex flex-row">
              {viewLayout !== 'image_only' && (
                <div 
                  style={{ width: viewLayout === 'split' ? `${centerSplitPercent}%` : '100%' }}
                  className="h-full min-w-0 relative overflow-hidden"
                >
                  <GraphCanvas />
                </div>
              )}

              {viewLayout === 'split' && (
                <div
                  onMouseDown={startResizingCenter}
                  onDoubleClick={() => setCenterSplitPercent(50)}
                  className="w-1.5 h-full bg-[#181920] hover:bg-cyan-500 active:bg-cyan-400 cursor-col-resize relative group transition-colors flex items-center justify-center z-20 shrink-0 select-none border-x border-[#252630]"
                  title="Kéo chuột để thay đổi kích thước Đồ Thị / Ảnh (Nhấp đúp để đặt lại 50%)"
                >
                  <div className="w-0.5 h-8 bg-zinc-600 group-hover:bg-cyan-200 rounded-full transition-colors" />
                  
                  {/* Quick Collapse Arrow Helpers */}
                  <div className="absolute top-3 flex flex-col gap-1 opacity-0 group-hover:opacity-100 transition-opacity bg-[#12131a] border border-[#2b2d3d] rounded p-0.5 shadow-xl z-30">
                    <button
                      onClick={(e) => { e.stopPropagation(); setCenterSplitPercent(20); }}
                      className="p-1 hover:bg-cyan-500/20 text-zinc-400 hover:text-cyan-400 rounded"
                      title="Thu nhỏ Đồ thị sang trái (20%)"
                    >
                      <ChevronLeft size={12} />
                    </button>
                    <button
                      onClick={(e) => { e.stopPropagation(); setCenterSplitPercent(80); }}
                      className="p-1 hover:bg-cyan-500/20 text-zinc-400 hover:text-cyan-400 rounded"
                      title="Thu nhỏ Ảnh sang phải (80% đồ thị)"
                    >
                      <ChevronRight size={12} />
                    </button>
                  </div>
                </div>
              )}

              {viewLayout !== 'graph_only' && (
                <div 
                  style={{ width: viewLayout === 'split' ? `${100 - centerSplitPercent}%` : '100%' }}
                  className="h-full min-w-0 relative overflow-hidden bg-[#09090b]"
                >
                  <ImageViewer />
                </div>
              )}
            </div>

            {/* Filmstrip Strip for Dataset Image Inspection */}
            <Filmstrip />

            {/* Bottom Dock: Output Console / Diagnostics / Performance Profiler */}
            <BottomDock />
          </div>

          {/* Right Dock: Properties Inspector & Live Data Preview */}
          <RightDock />
        </main>
      )}

      {/* 4. Industrial Telemetry Status Footer */}
      <StatusFooter />

      {/* Global Modals & Command Palette */}
      <NewProjectModal />
      <CommandPalette />
    </div>
  );
};

export default App;
