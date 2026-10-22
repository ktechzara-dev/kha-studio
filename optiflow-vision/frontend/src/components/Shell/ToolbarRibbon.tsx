import React from 'react';
import { useI18n } from '../../i18n';
import { useViewerStore } from '../../state/useViewerStore';
import { useGraphStore } from '../../state/useGraphStore';
import { useDockStore } from '../../state/useDockStore';
import { WORKFLOW_PRESETS } from '../../state/sampleWorkflows';
import { 
  Play, Repeat, Square, StepForward, 
  Maximize, ZoomIn, ZoomOut, RotateCcw, 
  LayoutGrid, Layers, Crosshair, BarChart2,
  Zap, PanelLeft, PanelRight, Terminal, Film, LayoutDashboard,
  Sparkles, Monitor, Palette, Columns, Eye, GitBranch, Image as ImageIcon
} from 'lucide-react';

export const ToolbarRibbon: React.FC = () => {
  const { t, lang } = useI18n();
  const { 
    scale, setScale, resetView,
    activeImageId, setActiveImageId,
    showHistogram, setShowHistogram,
    showProbe, setShowProbe,
    showOverlays, setShowOverlays
  } = useViewerStore();

  const {
    runOnce,
    stepOnce,
    undo,
    redo,
    canUndo,
    canRedo,
    isRunning,
    isContinuous,
    continuousFps,
    continuousCycleCount,
    toggleContinuous,
    stopAll,
    lastExecutionTimeMs,
    nodes,
    loadPresetWorkflow,
    viewLayout,
    setViewLayout,
  } = useGraphStore();

  const { 
    leftDock, setLeftDock, 
    rightDock, setRightDock, 
    bottomDock, setBottomDock, 
    filmstrip, setFilmstripOpen,
    currentWorkspace, setWorkspace, resetLayout 
  } = useDockStore();

  React.useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'F5') {
        e.preventDefault();
        runOnce();
      } else if (e.key === 'F10') {
        e.preventDefault();
        stepOnce();
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'z') {
        e.preventDefault();
        if (e.shiftKey) {
          redo();
        } else {
          undo();
        }
      } else if ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'y') {
        e.preventDefault();
        redo();
      }
    };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [runOnce, stepOnce, undo, redo]);

  return (
    <div className="h-10 bg-[#15161c] border-b border-[#252630] flex items-center justify-between px-2 text-xs select-none z-30 overflow-x-auto overflow-y-hidden scrollbar-none w-full max-w-full shrink-0">
      {/* 1. Primary Execution Group */}
      <div className="flex items-center gap-1.5 pr-2 border-r border-[#252630] shrink-0">
        {/* Run Once */}
        <button 
          onClick={runOnce}
          disabled={isRunning}
          className={`btn-industrial btn-primary flex items-center gap-1.5 transition-all shadow-[0_0_15px_rgba(0,240,255,0.25)] ${
            isRunning ? 'animate-pulse cursor-wait' : ''
          }`}
          title="Chạy 1 lần đồ thị theo thứ tự DAG (F5)"
        >
          <Play className={`w-3.5 h-3.5 fill-current ${isRunning ? 'animate-spin' : ''}`} />
          <span className="font-bold">{isRunning ? 'Running...' : 'Run Once'}</span>
          <span className="text-[9px] bg-black/30 px-1 py-0.2 rounded font-mono ml-0.5">F5</span>
        </button>

        {/* Execution Time Badge */}
        {lastExecutionTimeMs > 0 && (
          <span className="flex items-center gap-1 px-1.5 py-0.5 rounded bg-[#10b981]/15 text-[#10b981] font-mono text-[11px] font-bold border border-[#10b981]/30">
            <Zap className="w-3 h-3" />
            <span>{lastExecutionTimeMs.toFixed(1)}ms</span>
          </span>
        )}

        {/* Continuous Run */}
        <button 
          onClick={toggleContinuous}
          className={`btn-industrial flex items-center gap-1.5 transition-all ${
            isContinuous 
              ? 'bg-[#10b981] text-black font-extrabold border-[#10b981] shadow-[0_0_16px_rgba(16,185,129,0.7)] animate-pulse' 
              : 'hover:border-[#10b981] hover:text-[#10b981]'
          }`}
          title="Chạy liên tục theo chu kỳ camera (Bấm để Bật / Dừng)"
        >
          <Repeat className={`w-3.5 h-3.5 ${isContinuous ? 'animate-spin' : ''}`} />
          <span>{isContinuous ? `Continuous (${continuousFps})` : 'Continuous'}</span>
        </button>

        {/* Step Forward */}
        <button 
          onClick={stepOnce}
          disabled={isRunning}
          className="btn-industrial hover:border-[#f59e0b] hover:text-[#f59e0b]"
          title="Chạy từng bước khối kế tiếp (F10)"
        >
          <StepForward className="w-3.5 h-3.5" />
          <span className="hidden xl:inline">Step</span>
          <span className="text-[9px] bg-[#252630] px-1 rounded font-mono hidden xl:inline">F10</span>
        </button>

        {/* Stop */}
        <button 
          onClick={stopAll}
          className="btn-industrial hover:border-[#ef4444] hover:text-[#ef4444] hover:bg-[#ef4444]/20"
          title="Dừng toàn bộ luồng thực thi (Shift+F5)"
        >
          <Square className="w-3.5 h-3.5 fill-current" />
        </button>
      </div>

      {/* 2. View Mode Switcher: Split / Image View / Graph View */}
      <div className="flex items-center gap-1 pr-2 border-r border-[#252630] shrink-0">
        <div className="flex items-center bg-[#101014] border border-[#2b2c3a] p-0.5 rounded">
          <button
            onClick={() => setViewLayout('split')}
            className={`px-1.5 py-0.5 rounded text-[11px] font-semibold flex items-center gap-1 transition-all ${
              viewLayout === 'split' ? 'bg-[#00f0ff] text-black shadow-sm font-bold' : 'text-[#71717a] hover:text-white'
            }`}
            title="Chia đôi: Lưu Đồ DAG + Cửa Sổ Ảnh Live"
          >
            <Columns className="w-3 h-3" />
            <span className="hidden lg:inline">Chia Đôi</span>
          </button>
          <button
            onClick={() => setViewLayout('image_only')}
            className={`px-1.5 py-0.5 rounded text-[11px] font-semibold flex items-center gap-1 transition-all ${
              viewLayout === 'image_only' ? 'bg-[#00f0ff] text-black shadow-sm font-bold' : 'text-[#71717a] hover:text-white'
            }`}
            title="Chỉ hiển thị Cửa Sổ Ảnh Thị Giác"
          >
            <Eye className="w-3 h-3" />
            <span className="hidden lg:inline">Ảnh Live</span>
          </button>
          <button
            onClick={() => setViewLayout('graph_only')}
            className={`px-1.5 py-0.5 rounded text-[11px] font-semibold flex items-center gap-1 transition-all ${
              viewLayout === 'graph_only' ? 'bg-[#00f0ff] text-black shadow-sm font-bold' : 'text-[#71717a] hover:text-white'
            }`}
            title="Chỉ hiển thị Lưu Đồ Khối DAG"
          >
            <GitBranch className="w-3 h-3" />
            <span className="hidden lg:inline">Lưu Đồ</span>
          </button>
        </div>
      </div>

      {/* 3. Sample Images Dropdown */}
      <div className="flex items-center gap-1 pr-2 border-r border-[#252630] shrink-0">
        <div className="flex items-center gap-1 bg-[#101014] border border-[#2b2c3a] px-1.5 py-0.5 rounded hover:border-[#f59e0b] transition-colors">
          <ImageIcon className="w-3.5 h-3.5 text-[#f59e0b]" />
          <select
            value={activeImageId}
            onChange={(e) => setActiveImageId(e.target.value)}
            className="bg-transparent font-bold text-xs text-[#f59e0b] outline-none cursor-pointer max-w-[145px] truncate"
            title="Chọn tập hình ảnh mẫu công nghiệp"
          >
            <option value="sample_bottle" className="bg-[#181920]">🧴 Chai & Nắp Unilever</option>
            <option value="sample_pcb" className="bg-[#181920]">📟 PCB & IC STM32</option>
            <option value="sample_calib" className="bg-[#181920]">🏁 Bàn Cờ Calib</option>
            <option value="sample_pharma" className="bg-[#181920]">💊 Vỉ Dược Phẩm</option>
            <option value="sample_barcode" className="bg-[#181920]">🏷️ Thùng Mã QR</option>
            <option value="sample_gear" className="bg-[#181920]">⚙️ Bánh Răng Caliper</option>
          </select>
        </div>
      </div>

      {/* 4. Presets Dropdown */}
      <div className="flex items-center gap-1 pr-2 border-r border-[#252630] shrink-0">
        <div className="flex items-center gap-1 bg-[#101014] border border-[#2b2c3a] px-1.5 py-0.5 rounded hover:border-[#00f0ff] transition-colors">
          <Sparkles className="w-3.5 h-3.5 text-[#00f0ff]" />
          <select
            onChange={(e) => {
              if (e.target.value) {
                loadPresetWorkflow(e.target.value);
                e.target.value = '';
              }
            }}
            defaultValue=""
            className="bg-transparent font-bold text-xs text-[#00f0ff] outline-none cursor-pointer max-w-[160px] truncate"
            title="Nạp quy trình mẫu công nghiệp có sẵn"
          >
            <option value="" disabled className="bg-[#181920]">-- Mẫu Dự Án --</option>
            {WORKFLOW_PRESETS.map((p) => (
              <option key={p.id} value={p.id} className="bg-[#181920]">
                {p.title}
              </option>
            ))}
          </select>
        </div>
      </div>

      {/* 3. Graph Canvas Actions: Align / Zoom */}
      <div className="flex items-center gap-1.5 pr-2 border-r border-[#252630] shrink-0">
        {/* Zoom Controls */}
        <div className="flex items-center bg-[#101014] border border-[#252630] rounded p-0.5">
          <button 
            onClick={() => setScale(scale / 1.25)}
            className="p-1 hover:bg-[#252630] rounded text-[#a1a1aa] hover:text-white"
            title="Zoom Out (-)"
          >
            <ZoomOut className="w-3.5 h-3.5" />
          </button>
          <span className="px-1.5 font-mono text-[11px] text-[#00f0ff] min-w-[40px] text-center">
            {Math.round(scale * 100)}%
          </span>
          <button 
            onClick={() => setScale(scale * 1.25)}
            className="p-1 hover:bg-[#252630] rounded text-[#a1a1aa] hover:text-white"
            title="Zoom In (+)"
          >
            <ZoomIn className="w-3.5 h-3.5" />
          </button>
          <button 
            onClick={resetView}
            className="p-1 hover:bg-[#252630] rounded text-[#a1a1aa] hover:text-white border-l border-[#252630]"
            title="Vừa Khung Hình (Fit to View)"
          >
            <Maximize className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* 4. HMI Designer Quick Button & Workspace Selector */}
      <div className="flex items-center gap-1.5 shrink-0">
        {/* Design HMI Button */}
        <button
          onClick={() => setWorkspace(currentWorkspace === 'HMI Designer' ? 'Default' : 'HMI Designer')}
          className={`px-2.5 py-1 rounded text-xs font-bold flex items-center gap-1.5 transition-all border ${
            currentWorkspace === 'HMI Designer'
              ? 'bg-[#00f0ff] text-[#0a0b0e] border-[#00f0ff] shadow-[0_0_15px_rgba(0,240,255,0.4)]'
              : 'bg-[#181922] hover:bg-[#222330] text-[#00f0ff] border-[#00f0ff]/40'
          }`}
          title="Mở giao diện thiết kế màn hình HMI kéo thả cho công nhân"
        >
          <Monitor className="w-3.5 h-3.5" />
          <span>Thiết Kế HMI</span>
        </button>

        {/* Workspace Quick Dropdown */}
        <div className="flex items-center gap-1 bg-[#101014] border border-[#252630] rounded px-1.5 py-0.5">
          <LayoutDashboard className="w-3.5 h-3.5 text-[#a1a1aa]" />
          <select
            value={currentWorkspace}
            onChange={(e) => setWorkspace(e.target.value)}
            className="bg-transparent text-white text-[11px] font-semibold outline-none cursor-pointer pr-1"
            title="Chọn Không Gian Làm Việc (Workspace)"
          >
            <option value="Default" className="bg-[#181920]">Default IDE</option>
            <option value="HMI Designer" className="bg-[#181920]">HMI Designer</option>
            <option value="Operator View" className="bg-[#181920]">Operator View</option>
            <option value="Inspection & Tuning" className="bg-[#181920]">Inspection & Tuning</option>
            <option value="Graph Engineering" className="bg-[#181920]">Graph Engineering</option>
          </select>
        </div>

        {/* Reset Layout */}
        <button
          onClick={resetLayout}
          className="btn-industrial hover:border-[#00f0ff] hover:text-[#00f0ff]"
          title="Khôi phục kích thước dock về tỷ lệ vàng mặc định"
        >
          <RotateCcw className="w-3 h-3" />
          <span className="hidden lg:inline">Reset</span>
        </button>

        <div className="h-4 w-px bg-[#252630] mx-0.5" />

        {/* Dock Toggle Buttons */}
        <button
          onClick={() => setLeftDock({ isOpen: !leftDock.isOpen })}
          className={`p-1.5 rounded transition-all ${
            leftDock.isOpen 
              ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/40 shadow-sm' 
              : 'text-[#71717a] hover:text-white hover:bg-[#1e2029]'
          }`}
          title="Bật/Tắt Dock Trái (Catalog & Explorer)"
        >
          <PanelLeft className="w-3.5 h-3.5" />
        </button>

        <button
          onClick={() => setBottomDock({ isOpen: !bottomDock.isOpen })}
          className={`p-1.5 rounded transition-all ${
            bottomDock.isOpen 
              ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/40 shadow-sm' 
              : 'text-[#71717a] hover:text-white hover:bg-[#1e2029]'
          }`}
          title="Bật/Tắt Dock Dưới (Output Console / Diagnostics / Profiler)"
        >
          <Terminal className="w-3.5 h-3.5" />
        </button>

        <button
          onClick={() => setFilmstripOpen(!filmstrip.isOpen)}
          className={`p-1.5 rounded transition-all ${
            filmstrip.isOpen 
              ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/40 shadow-sm' 
              : 'text-[#71717a] hover:text-white hover:bg-[#1e2029]'
          }`}
          title="Bật/Tắt Băng Hình Ảnh (Filmstrip)"
        >
          <Film className="w-3.5 h-3.5" />
        </button>

        <button
          onClick={() => setRightDock({ isOpen: !rightDock.isOpen })}
          className={`p-1.5 rounded transition-all ${
            rightDock.isOpen 
              ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/40 shadow-sm' 
              : 'text-[#71717a] hover:text-white hover:bg-[#1e2029]'
          }`}
          title="Bật/Tắt Dock Phải (Properties & Data Preview)"
        >
          <PanelRight className="w-3.5 h-3.5" />
        </button>
      </div>
    </div>
  );
};
