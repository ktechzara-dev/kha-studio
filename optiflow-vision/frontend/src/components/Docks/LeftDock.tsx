import React, { useState } from 'react';
import { useDockStore } from '../../state/useDockStore';
import { useGraphStore } from '../../state/useGraphStore';
import { useViewerStore } from '../../state/useViewerStore';
import { useProjectStore } from '../../state/useProjectStore';
import { useI18n } from '../../i18n';
import { FilterLibraryDrawer } from '../GraphEditor/FilterLibraryDrawer';
import { 
  FolderTree, Sparkles, Folder, FileCode, Camera, Database, 
  Cpu, Monitor, Variable, ChevronRight, ChevronDown, Plus, 
  PanelLeftClose, RefreshCw, CheckCircle2 
} from 'lucide-react';

export const LeftDock: React.FC = () => {
  const { lang } = useI18n();
  const { leftDock, setLeftDock } = useDockStore();
  const { project, filePath } = useProjectStore();
  const { availableImages, activeImageId, setActiveImageId } = useViewerStore();
  const { nodes, edges } = useGraphStore();

  const [expandedSections, setExpandedSections] = useState<Record<string, boolean>>({
    programs: true,
    vars: true,
    cameras: true,
    datasets: true,
    fieldbus: true,
  });

  const toggleSection = (key: string) => {
    setExpandedSections((prev) => ({ ...prev, [key]: !prev[key] }));
  };

  const startResizingLeft = (e: React.MouseEvent) => {
    e.preventDefault();
    const startX = e.clientX;
    const startWidth = leftDock.width;

    const onMouseMove = (moveEvent: MouseEvent) => {
      const newWidth = Math.max(180, Math.min(550, startWidth + (moveEvent.clientX - startX)));
      setLeftDock({ width: newWidth });
    };

    const onMouseUp = () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  if (!leftDock.isOpen) {
    return (
      <aside className="w-8 bg-[#131418] border-r border-[#252630] flex flex-col items-center py-2 select-none z-20 shrink-0 gap-3">
        <button
          onClick={() => setLeftDock({ isOpen: true })}
          className="p-1.5 rounded hover:bg-[#252630] text-[#00f0ff] hover:text-white transition-colors"
          title="Mở rộng Dock Trái (Bộ Lọc & Dự Án)"
        >
          <ChevronRight className="w-4 h-4" />
        </button>
        <div className="h-4 w-px bg-[#252630]" />
        <button
          onClick={() => setLeftDock({ isOpen: true, activeTab: 'catalog' })}
          className="p-1 rounded text-[#a1a1aa] hover:text-[#00f0ff] hover:bg-[#252630]"
          title="Mở Thư Viện Bộ Lọc"
        >
          <Sparkles className="w-4 h-4" />
        </button>
        <button
          onClick={() => setLeftDock({ isOpen: true, activeTab: 'explorer' })}
          className="p-1 rounded text-[#a1a1aa] hover:text-[#00f0ff] hover:bg-[#252630]"
          title="Mở Cây Dự Án"
        >
          <FolderTree className="w-4 h-4" />
        </button>
      </aside>
    );
  }

  return (
    <aside 
      className="relative bg-[#131418] border-r border-[#252630] flex flex-col h-full select-none z-20 shrink-0 group/dock"
      style={{ width: `${leftDock.width}px`, minWidth: `${leftDock.width}px`, maxWidth: `${leftDock.width}px` }}
    >
      {/* Draggable Resize Handle */}
      <div
        onMouseDown={startResizingLeft}
        className="absolute top-0 -right-1 w-2 h-full cursor-col-resize hover:bg-[#00f0ff]/80 active:bg-[#00f0ff] transition-colors z-40"
        title="Kéo chuột để thay đổi độ rộng Dock Trái"
      />
      {/* 1. Dock Tab Switcher Header */}
      <div className="h-8 px-2 bg-[#17181f] border-b border-[#252630] flex items-center justify-between text-xs">
        <div className="flex items-center gap-1">
          {/* Tab 1: Filter Catalog */}
          <button
            onClick={() => setLeftDock({ activeTab: 'catalog' })}
            className={`px-2.5 py-1 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              leftDock.activeTab === 'catalog'
                ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <Sparkles className="w-3.5 h-3.5" />
            <span>{lang === 'vi' ? 'Bộ Lọc' : 'Filters'}</span>
          </button>

          {/* Tab 2: Project Explorer */}
          <button
            onClick={() => setLeftDock({ activeTab: 'explorer' })}
            className={`px-2.5 py-1 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              leftDock.activeTab === 'explorer'
                ? 'bg-[#1e2029] text-[#10b981] border border-[#10b981]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <FolderTree className="w-3.5 h-3.5" />
            <span>{lang === 'vi' ? 'Dự Án' : 'Explorer'}</span>
          </button>
        </div>

        {/* Close Left Dock */}
        <button
          onClick={() => setLeftDock({ isOpen: false })}
          className="p-1 rounded hover:bg-[#252630] text-[#71717a] hover:text-white transition-colors"
          title="Thu gọn Dock trái"
        >
          <PanelLeftClose className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* 2. Dock Body */}
      <div className="flex-1 overflow-hidden flex flex-col">
        {leftDock.activeTab === 'catalog' ? (
          <div className="w-full h-full flex flex-col">
            <FilterLibraryDrawer />
          </div>
        ) : (
          /* Project Explorer Tree View */
          <div className="flex-1 overflow-y-auto p-2 space-y-1 text-xs font-sans">
            {/* Root Project Node */}
            <div className="p-2 rounded bg-[#181920] border border-[#252630] mb-2 flex items-center justify-between">
              <div className="flex items-center gap-2 truncate">
                <FileCode className="w-4 h-4 text-[#00f0ff]" />
                <span className="font-bold text-[#f4f4f7] truncate">
                  {filePath ? filePath.split(/[\\/]/).pop() : (project?.metadata.name || 'Inspection.vsproj')}
                </span>
              </div>
              <span className="text-[10px] text-[#71717a] font-mono">v1.0</span>
            </div>

            {/* Folder 1: Programs & Macros */}
            <div className="rounded border border-[#202129] bg-[#101013] overflow-hidden">
              <button
                onClick={() => toggleSection('programs')}
                className="w-full px-2 py-1.5 bg-[#16171d] hover:bg-[#1c1d24] flex items-center justify-between text-left transition-colors"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#e4e4e7]">
                  <Folder className="w-3.5 h-3.5 text-[#00f0ff]" />
                  <span>Quy Trình & Macro ({useDockStore.getState().tabs.length})</span>
                </div>
                {expandedSections.programs ? <ChevronDown className="w-3.5 h-3.5 text-[#71717a]" /> : <ChevronRight className="w-3.5 h-3.5 text-[#71717a]" />}
              </button>
              {expandedSections.programs && (
                <div className="p-1.5 space-y-1 pl-4">
                  {useDockStore.getState().tabs.map((tab) => (
                    <div 
                      key={tab.id}
                      onClick={() => useDockStore.getState().setActiveTabId(tab.id)}
                      className={`p-1 rounded cursor-pointer flex items-center justify-between text-[11px] ${
                        useDockStore.getState().activeTabId === tab.id
                          ? 'bg-[#00f0ff]/15 text-[#00f0ff] font-semibold'
                          : 'text-[#9ca3af] hover:bg-[#1a1b22] hover:text-white'
                      }`}
                    >
                      <span className="truncate">{tab.name}</span>
                      <span className="text-[9px] font-mono text-[#71717a]">{nodes.length} nodes</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Folder 2: Cameras & Hardware Sensors */}
            <div className="rounded border border-[#202129] bg-[#101013] overflow-hidden">
              <button
                onClick={() => toggleSection('cameras')}
                className="w-full px-2 py-1.5 bg-[#16171d] hover:bg-[#1c1d24] flex items-center justify-between text-left transition-colors"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#e4e4e7]">
                  <Camera className="w-3.5 h-3.5 text-[#10b981]" />
                  <span>Camera Công Nghiệp (1)</span>
                </div>
                {expandedSections.cameras ? <ChevronDown className="w-3.5 h-3.5 text-[#71717a]" /> : <ChevronRight className="w-3.5 h-3.5 text-[#71717a]" />}
              </button>
              {expandedSections.cameras && (
                <div className="p-1.5 space-y-1 pl-4 text-[10.5px]">
                  <div className="p-1 rounded bg-[#16171d] flex items-center justify-between">
                    <span className="text-[#10b981] font-semibold">Cam01: Basler acA1920</span>
                    <span className="text-[9px] text-[#71717a]">Mono8</span>
                  </div>
                  <div className="text-[9.5px] text-[#71717a] pl-1 font-mono">
                    1936x1216 @ 60 FPS • GenICam
                  </div>
                </div>
              )}
            </div>

            {/* Folder 3: Image Datasets */}
            <div className="rounded border border-[#202129] bg-[#101013] overflow-hidden">
              <button
                onClick={() => toggleSection('datasets')}
                className="w-full px-2 py-1.5 bg-[#16171d] hover:bg-[#1c1d24] flex items-center justify-between text-left transition-colors"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#e4e4e7]">
                  <Database className="w-3.5 h-3.5 text-[#f59e0b]" />
                  <span>Tập Dữ Liệu Ảnh ({availableImages.length})</span>
                </div>
                {expandedSections.datasets ? <ChevronDown className="w-3.5 h-3.5 text-[#71717a]" /> : <ChevronRight className="w-3.5 h-3.5 text-[#71717a]" />}
              </button>
              {expandedSections.datasets && (
                <div className="p-1.5 space-y-1 pl-4">
                  {availableImages.map((img) => (
                    <div
                      key={img.id}
                      onClick={() => setActiveImageId(img.id)}
                      className={`p-1 rounded cursor-pointer flex items-center justify-between text-[11px] ${
                        img.id === activeImageId
                          ? 'bg-[#f59e0b]/20 text-[#f59e0b] font-semibold'
                          : 'text-[#9ca3af] hover:bg-[#1a1b22] hover:text-white'
                      }`}
                    >
                      <span className="truncate">{img.id}</span>
                      <span className="text-[9px] font-mono text-[#71717a]">{img.width}x{img.height}</span>
                    </div>
                  ))}
                </div>
              )}
            </div>

            {/* Folder 4: Global Variables */}
            <div className="rounded border border-[#202129] bg-[#101013] overflow-hidden">
              <button
                onClick={() => toggleSection('vars')}
                className="w-full px-2 py-1.5 bg-[#16171d] hover:bg-[#1c1d24] flex items-center justify-between text-left transition-colors"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#e4e4e7]">
                  <Variable className="w-3.5 h-3.5 text-[#a855f7]" />
                  <span>Biến Toàn Cục (3)</span>
                </div>
                {expandedSections.vars ? <ChevronDown className="w-3.5 h-3.5 text-[#71717a]" /> : <ChevronRight className="w-3.5 h-3.5 text-[#71717a]" />}
              </button>
              {expandedSections.vars && (
                <div className="p-1.5 space-y-1 pl-4 font-mono text-[10.5px]">
                  <div className="flex justify-between py-0.5 border-b border-[#1c1d24]">
                    <span className="text-[#a1a1aa]">g_ToleranceMm:</span>
                    <span className="text-[#a855f7]">0.35</span>
                  </div>
                  <div className="flex justify-between py-0.5 border-b border-[#1c1d24]">
                    <span className="text-[#a1a1aa]">g_MinArea:</span>
                    <span className="text-[#38bdf8]">500.0</span>
                  </div>
                  <div className="flex justify-between py-0.5">
                    <span className="text-[#a1a1aa]">g_RobotQueue:</span>
                    <span className="text-[#10b981]">Array[16]</span>
                  </div>
                </div>
              )}
            </div>

            {/* Folder 5: Fieldbus & PLC */}
            <div className="rounded border border-[#202129] bg-[#101013] overflow-hidden">
              <button
                onClick={() => toggleSection('fieldbus')}
                className="w-full px-2 py-1.5 bg-[#16171d] hover:bg-[#1c1d24] flex items-center justify-between text-left transition-colors"
              >
                <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#e4e4e7]">
                  <Cpu className="w-3.5 h-3.5 text-[#ec4899]" />
                  <span>Giao Tiếp PLC / Robot (1)</span>
                </div>
                {expandedSections.fieldbus ? <ChevronDown className="w-3.5 h-3.5 text-[#71717a]" /> : <ChevronRight className="w-3.5 h-3.5 text-[#71717a]" />}
              </button>
              {expandedSections.fieldbus && (
                <div className="p-1.5 space-y-1 pl-4 text-[10.5px]">
                  <div className="p-1 rounded bg-[#16171d] flex items-center justify-between">
                    <span className="text-[#ec4899] font-semibold">Schneider SERT Protocol</span>
                    <span className="text-[9px] text-[#10b981] font-bold">READY</span>
                  </div>
                  <div className="text-[9.5px] text-[#71717a] font-mono">
                    TCP Socket 192.168.1.50:502
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </aside>
  );
};
