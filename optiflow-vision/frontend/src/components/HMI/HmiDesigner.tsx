import React, { useState } from 'react';
import { useGraphStore } from '../../state/useGraphStore';
import { useViewerStore } from '../../state/useViewerStore';
import { useDockStore } from '../../state/useDockStore';
import { useSidecarStore } from '../../state/useSidecarStore';
import { 
  Play, Square, RefreshCw, CheckCircle2, XCircle, 
  Layers, BarChart3, Settings2, Sliders, ShieldCheck, 
  AlertTriangle, Plus, Trash2, Move, Maximize2, Monitor, 
  Eye, Code2, Save, LayoutGrid, Zap, Check, Sparkles
} from 'lucide-react';

export interface HmiWidget {
  id: string;
  type: 'lamp' | 'video' | 'counters' | 'digital_meter' | 'buttons' | 'spc_gauge';
  title: string;
  x: number;
  y: number;
  width: number;
  height: number;
  bindingNodeId?: string;
  bindingPort?: string;
  customLabel?: string;
}

const DEFAULT_WIDGETS: HmiWidget[] = [
  {
    id: 'w_banner',
    type: 'lamp',
    title: 'Trạng Thái Kiểm Định PASS / FAIL',
    x: 20,
    y: 20,
    width: 680,
    height: 120,
    bindingNodeId: 'node_decision',
    bindingPort: 'overall_pass',
  },
  {
    id: 'w_video',
    type: 'video',
    title: 'Khung Hình Trực Tiếp Camera Delta (HUD)',
    x: 20,
    y: 160,
    width: 680,
    height: 480,
    bindingNodeId: 'node_src',
    bindingPort: 'image',
  },
  {
    id: 'w_counters',
    type: 'counters',
    title: 'Bộ Đếm Sản Lượng Ca Sản Xuất',
    x: 720,
    y: 20,
    width: 380,
    height: 230,
    bindingNodeId: 'node_decision',
    bindingPort: 'overall_pass',
  },
  {
    id: 'w_meter',
    type: 'digital_meter',
    title: 'Tọa Độ Robot Bốc (X, Y, Theta)',
    x: 720,
    y: 270,
    width: 380,
    height: 170,
    bindingNodeId: 'node_centroid',
    bindingPort: 'centroid',
  },
  {
    id: 'w_spc',
    type: 'spc_gauge',
    title: 'Chỉ Số Năng Lực Quá Trình Cpk',
    x: 720,
    y: 460,
    width: 380,
    height: 180,
    bindingNodeId: 'node_spc',
    bindingPort: 'cpk',
  },
];

export const HmiDesigner: React.FC = () => {
  const { runOnce, isRunning, lastExecutionTimeMs, nodes, nodeExecutionResults } = useGraphStore();
  const { activeImageId, overlays } = useViewerStore();
  const { setWorkspace } = useDockStore();
  const { baseUrl } = useSidecarStore();

  const [mode, setMode] = useState<'design' | 'runtime'>('design');
  const [widgets, setWidgets] = useState<HmiWidget[]>(DEFAULT_WIDGETS);
  const [selectedWidgetId, setSelectedWidgetId] = useState<string | null>('w_banner');

  // Runtime mock counters
  const [totalCount, setTotalCount] = useState(14850);
  const [passCount, setPassCount] = useState(14822);
  const [rejectCount, setRejectCount] = useState(28);
  const [lastVerdict, setLastVerdict] = useState<'PASS' | 'FAIL'>('PASS');
  const [isContinuous, setIsContinuous] = useState(false);

  // Trigger Run Once
  const handleTriggerRun = async () => {
    await runOnce();
    setTotalCount((t) => t + 1);
    const pass = Math.random() > 0.02;
    if (pass) {
      setPassCount((p) => p + 1);
      setLastVerdict('PASS');
    } else {
      setRejectCount((r) => r + 1);
      setLastVerdict('FAIL');
    }
  };

  // Add Widget
  const addWidget = (type: HmiWidget['type']) => {
    const id = `w_${Date.now()}`;
    const titles: Record<HmiWidget['type'], string> = {
      lamp: 'Đèn Báo PASS / FAIL',
      video: 'Màn Hình Camera Trực Tiếp',
      counters: 'Thống Kê Sản Lượng',
      digital_meter: 'Đồng Hồ Đo Số Liệu',
      buttons: 'Bảng Nút Thao Tác',
      spc_gauge: 'Đồng Hồ Đo Năng Lực Cpk',
    };
    const newW: HmiWidget = {
      id,
      type,
      title: titles[type] || 'Widget Mới',
      x: 60 + (widgets.length % 4) * 40,
      y: 60 + (widgets.length % 4) * 40,
      width: type === 'video' ? 500 : 340,
      height: type === 'video' ? 360 : 160,
    };
    setWidgets([...widgets, newW]);
    setSelectedWidgetId(id);
  };

  // Remove Widget
  const removeWidget = (id: string) => {
    setWidgets(widgets.filter((w) => w.id !== id));
    if (selectedWidgetId === id) setSelectedWidgetId(null);
  };

  // Update selected widget prop
  const updateSelectedWidget = (key: keyof HmiWidget, value: any) => {
    if (!selectedWidgetId) return;
    setWidgets(
      widgets.map((w) => (w.id === selectedWidgetId ? { ...w, [key]: value } : w))
    );
  };

  const selectedWidget = widgets.find((w) => w.id === selectedWidgetId);
  const yieldPct = totalCount > 0 ? ((passCount / totalCount) * 100).toFixed(2) : '100.00';

  return (
    <div className="flex-1 flex flex-col h-full bg-[#0a0b0e] text-[#f4f4f7] select-none overflow-hidden font-sans">
      {/* 1. Header & Mode Switcher */}
      <header className="h-12 bg-[#121318] border-b border-[#252630] px-4 flex items-center justify-between z-20">
        <div className="flex items-center gap-3">
          <div className="w-2.5 h-2.5 rounded-full bg-[#00f0ff] animate-pulse" />
          <div className="flex items-center gap-2">
            <span className="font-bold text-sm tracking-wide text-white uppercase flex items-center gap-2">
              <Monitor className="w-4 h-4 text-[#00f0ff]" />
              HMI Designer & Operator Builder
            </span>
            <span className="text-[10px] bg-[#00f0ff]/10 text-[#00f0ff] px-2 py-0.5 rounded border border-[#00f0ff]/30 font-mono font-semibold">
              WYSIWYG CANVAS
            </span>
          </div>
        </div>

        {/* Toolbar: Switch Design / Runtime & Add Widgets */}
        <div className="flex items-center gap-2">
          {/* Mode Switcher */}
          <div className="flex items-center bg-[#181920] border border-[#2b2c38] rounded p-0.5">
            <button
              onClick={() => setMode('design')}
              className={`flex items-center gap-1.5 px-3 py-1 rounded text-xs font-semibold transition-all ${
                mode === 'design'
                  ? 'bg-[#00f0ff] text-[#0a0b0e] shadow-md'
                  : 'text-[#a1a1aa] hover:text-white'
              }`}
            >
              <Settings2 className="w-3.5 h-3.5" />
              <span>Thiết Kế (Design)</span>
            </button>
            <button
              onClick={() => setMode('runtime')}
              className={`flex items-center gap-1.5 px-3 py-1 rounded text-xs font-semibold transition-all ${
                mode === 'runtime'
                  ? 'bg-[#10b981] text-[#0a0b0e] shadow-md'
                  : 'text-[#a1a1aa] hover:text-white'
              }`}
            >
              <Eye className="w-3.5 h-3.5" />
              <span>Vận Hành (Runtime)</span>
            </button>
          </div>

          <div className="h-4 w-px bg-[#26262c] mx-1" />

          {/* Return to IDE */}
          <button
            onClick={() => setWorkspace('Default')}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-[#1f2029] hover:bg-[#2b2c38] text-xs font-semibold rounded text-[#00f0ff] border border-[#00f0ff]/30 transition-all"
            title="Quay lại đồ thị lập trình DAG"
          >
            <Code2 className="w-3.5 h-3.5" />
            <span>Trở Về IDE</span>
          </button>
        </div>
      </header>

      {/* 2. Main Workspace Layout */}
      <div className="flex-1 flex flex-row overflow-hidden relative">
        {/* Left Palette: In Design Mode only */}
        {mode === 'design' && (
          <aside className="w-64 bg-[#101116] border-r border-[#22232c] flex flex-col p-3 z-10 shadow-lg">
            <div className="text-[11px] font-bold uppercase tracking-wider text-[#71717a] mb-3 flex items-center gap-1.5">
              <Plus className="w-3.5 h-3.5 text-[#00f0ff]" />
              Thư Viện Widget HMI
            </div>

            <div className="flex flex-col gap-2">
              <button
                onClick={() => addWidget('lamp')}
                className="flex items-center gap-2.5 p-2.5 bg-[#171821] hover:bg-[#22232e] border border-[#262734] hover:border-[#10b981] rounded text-left transition-all group"
              >
                <div className="w-7 h-7 rounded bg-[#10b981]/10 border border-[#10b981]/40 flex items-center justify-center text-[#10b981]">
                  <CheckCircle2 className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-xs font-bold text-[#e4e4e7] group-hover:text-[#10b981]">Đèn PASS / FAIL</div>
                  <div className="text-[10px] text-[#71717a]">Banner hiển thị kết quả cực đại</div>
                </div>
              </button>

              <button
                onClick={() => addWidget('video')}
                className="flex items-center gap-2.5 p-2.5 bg-[#171821] hover:bg-[#22232e] border border-[#262734] hover:border-[#00f0ff] rounded text-left transition-all group"
              >
                <div className="w-7 h-7 rounded bg-[#00f0ff]/10 border border-[#00f0ff]/40 flex items-center justify-center text-[#00f0ff]">
                  <Monitor className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-xs font-bold text-[#e4e4e7] group-hover:text-[#00f0ff]">Khung Hình Camera</div>
                  <div className="text-[10px] text-[#71717a]">Xem trực tiếp ảnh + HUD Overlays</div>
                </div>
              </button>

              <button
                onClick={() => addWidget('counters')}
                className="flex items-center gap-2.5 p-2.5 bg-[#171821] hover:bg-[#22232e] border border-[#262734] hover:border-[#f59e0b] rounded text-left transition-all group"
              >
                <div className="w-7 h-7 rounded bg-[#f59e0b]/10 border border-[#f59e0b]/40 flex items-center justify-center text-[#f59e0b]">
                  <BarChart3 className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-xs font-bold text-[#e4e4e7] group-hover:text-[#f59e0b]">Bộ Đếm Sản Lượng</div>
                  <div className="text-[10px] text-[#71717a]">Tổng số, Pass, Reject, Yield %</div>
                </div>
              </button>

              <button
                onClick={() => addWidget('digital_meter')}
                className="flex items-center gap-2.5 p-2.5 bg-[#171821] hover:bg-[#22232e] border border-[#262734] hover:border-[#ec4899] rounded text-left transition-all group"
              >
                <div className="w-7 h-7 rounded bg-[#ec4899]/10 border border-[#ec4899]/40 flex items-center justify-center text-[#ec4899]">
                  <Maximize2 className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-xs font-bold text-[#e4e4e7] group-hover:text-[#ec4899]">Đồng Hồ Đo Số Liệu</div>
                  <div className="text-[10px] text-[#71717a]">Hiển thị kích thước, tọa độ, góc</div>
                </div>
              </button>

              <button
                onClick={() => addWidget('spc_gauge')}
                className="flex items-center gap-2.5 p-2.5 bg-[#171821] hover:bg-[#22232e] border border-[#262734] hover:border-[#8b5cf6] rounded text-left transition-all group"
              >
                <div className="w-7 h-7 rounded bg-[#8b5cf6]/10 border border-[#8b5cf6]/40 flex items-center justify-center text-[#8b5cf6]">
                  <ShieldCheck className="w-4 h-4" />
                </div>
                <div>
                  <div className="text-xs font-bold text-[#e4e4e7] group-hover:text-[#8b5cf6]">Đồng Hồ Cpk / SPC</div>
                  <div className="text-[10px] text-[#71717a]">Chỉ số năng lực 6-Sigma</div>
                </div>
              </button>
            </div>

            <div className="mt-auto pt-3 border-t border-[#22232c]">
              <button
                onClick={() => alert('Đã lưu cấu hình màn hình HMI vào dự án .vsproj!')}
                className="w-full flex items-center justify-center gap-2 p-2 bg-[#10b981] hover:bg-[#059669] text-[#0a0b0e] font-bold text-xs rounded transition-all shadow-md"
              >
                <Save className="w-3.5 h-3.5" />
                <span>Lưu Thiết Kế HMI</span>
              </button>
            </div>
          </aside>
        )}

        {/* Center: Canvas Area */}
        <div className="flex-1 relative overflow-auto p-6 bg-[#0a0b0e]">
          {/* Dot Grid Background for Design Mode */}
          {mode === 'design' && (
            <div 
              className="absolute inset-0 pointer-events-none opacity-20"
              style={{
                backgroundImage: 'radial-gradient(#00f0ff 1px, transparent 1px)',
                backgroundSize: '24px 24px',
              }}
            />
          )}

          {/* Render All Widgets */}
          <div className="relative min-w-[1100px] min-h-[700px]">
            {widgets.map((w) => {
              const isSelected = mode === 'design' && selectedWidgetId === w.id;

              return (
                <div
                  key={w.id}
                  onClick={() => mode === 'design' && setSelectedWidgetId(w.id)}
                  style={{
                    position: 'absolute',
                    left: `${w.x}px`,
                    top: `${w.y}px`,
                    width: `${w.width}px`,
                    height: `${w.height}px`,
                  }}
                  className={`rounded-lg bg-[#121319] border transition-all duration-150 flex flex-col overflow-hidden shadow-2xl ${
                    isSelected
                      ? 'border-[#00f0ff] ring-2 ring-[#00f0ff]/40 shadow-[0_0_25px_rgba(0,240,255,0.25)] z-30'
                      : 'border-[#22232d] hover:border-[#383946] z-10'
                  }`}
                >
                  {/* Widget Header in Design Mode */}
                  {mode === 'design' && (
                    <div className="h-7 bg-[#171822] border-b border-[#252634] px-3 flex items-center justify-between text-[11px] font-semibold text-[#a1a1aa] cursor-move">
                      <div className="flex items-center gap-1.5 truncate">
                        <Move className="w-3 h-3 text-[#00f0ff]" />
                        <span className="truncate">{w.title}</span>
                      </div>
                      <button
                        onClick={(e) => {
                          e.stopPropagation();
                          removeWidget(w.id);
                        }}
                        className="text-[#71717a] hover:text-[#ef4444] p-0.5 rounded"
                        title="Xóa widget này"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                    </div>
                  )}

                  {/* Widget Content Body */}
                  <div className="flex-1 p-3 flex flex-col justify-center items-center overflow-hidden">
                    {/* 1. Lamp / Verdict Banner */}
                    {w.type === 'lamp' && (
                      <div className={`w-full h-full rounded-md flex items-center justify-center gap-4 transition-all duration-200 border-2 ${
                        lastVerdict === 'PASS'
                          ? 'bg-[#10b981]/15 border-[#10b981] text-[#10b981]'
                          : 'bg-[#ef4444]/20 border-[#ef4444] text-[#ef4444] animate-pulse'
                      }`}>
                        {lastVerdict === 'PASS' ? (
                          <>
                            <CheckCircle2 className="w-12 h-12 stroke-[2.5]" />
                            <div className="text-left">
                              <div className="text-4xl font-black tracking-widest font-mono">PASS</div>
                              <div className="text-xs uppercase font-bold tracking-wider opacity-80">Sản Phẩm Đạt Chuẩn</div>
                            </div>
                          </>
                        ) : (
                          <>
                            <XCircle className="w-12 h-12 stroke-[2.5]" />
                            <div className="text-left">
                              <div className="text-4xl font-black tracking-widest font-mono">DEFECT / NG</div>
                              <div className="text-xs uppercase font-bold tracking-wider opacity-80">Phát Hiện Lỗi Bề Mặt</div>
                            </div>
                          </>
                        )}
                      </div>
                    )}

                    {/* 2. Video Viewport */}
                    {w.type === 'video' && (
                      <div className="w-full h-full bg-black rounded relative overflow-hidden flex items-center justify-center">
                        <img
                          src={`${baseUrl}/api/image/preview/sample_bottle?quality=85`}
                          alt="Live Camera"
                          className="max-w-full max-h-full object-contain"
                        />
                        <div className="absolute top-2 left-2 bg-black/60 px-2 py-0.5 rounded border border-white/10 text-[10px] font-mono text-[#00f0ff] flex items-center gap-1.5">
                          <span className="w-2 h-2 rounded-full bg-[#10b981] animate-ping" />
                          <span>LIVE 1920x1080 • 60 FPS</span>
                        </div>
                        {/* Overlaid bounding box HUD */}
                        <div className="absolute inset-0 pointer-events-none flex items-center justify-center">
                          <div className="w-48 h-64 border-2 border-[#10b981] rounded relative">
                            <span className="absolute -top-5 left-0 bg-[#10b981] text-black text-[9px] font-bold px-1.5 py-0.2 rounded font-mono">
                              BOTTLE_CAP [OK]
                            </span>
                            <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-4 h-4">
                              <div className="w-full h-0.5 bg-[#f59e0b] absolute top-1/2 -translate-y-1/2" />
                              <div className="h-full w-0.5 bg-[#f59e0b] absolute left-1/2 -translate-x-1/2" />
                            </div>
                          </div>
                        </div>
                      </div>
                    )}

                    {/* 3. Production Counters */}
                    {w.type === 'counters' && (
                      <div className="w-full h-full grid grid-cols-2 gap-2.5">
                        <div className="bg-[#181922] p-2.5 rounded border border-[#252632] flex flex-col justify-center">
                          <span className="text-[10px] text-[#71717a] uppercase font-bold">Tổng Đã Kiểm</span>
                          <span className="text-2xl font-black text-white font-mono">{totalCount.toLocaleString()}</span>
                        </div>
                        <div className="bg-[#181922] p-2.5 rounded border border-[#252632] flex flex-col justify-center">
                          <span className="text-[10px] text-[#10b981] uppercase font-bold">Tỷ Lệ Đạt (Yield)</span>
                          <span className="text-2xl font-black text-[#10b981] font-mono">{yieldPct}%</span>
                        </div>
                        <div className="bg-[#181922] p-2.5 rounded border border-[#252632] flex flex-col justify-center">
                          <span className="text-[10px] text-[#10b981] uppercase font-bold">Sản Lượng Đạt (PASS)</span>
                          <span className="text-xl font-bold text-[#10b981] font-mono">{passCount.toLocaleString()}</span>
                        </div>
                        <div className="bg-[#181922] p-2.5 rounded border border-[#252632] flex flex-col justify-center">
                          <span className="text-[10px] text-[#ef4444] uppercase font-bold">Lỗi Bị Loại (NG)</span>
                          <span className="text-xl font-bold text-[#ef4444] font-mono">{rejectCount.toLocaleString()}</span>
                        </div>
                      </div>
                    )}

                    {/* 4. Digital Meter */}
                    {w.type === 'digital_meter' && (
                      <div className="w-full h-full flex flex-col justify-between">
                        <div className="flex items-center justify-between border-b border-[#22232c] pb-1.5">
                          <span className="text-xs text-[#a1a1aa]">Tọa độ Robot X:</span>
                          <span className="font-mono font-bold text-base text-[#00f0ff]">+420.50 mm</span>
                        </div>
                        <div className="flex items-center justify-between border-b border-[#22232c] pb-1.5">
                          <span className="text-xs text-[#a1a1aa]">Tọa độ Robot Y:</span>
                          <span className="font-mono font-bold text-base text-[#00f0ff]">+185.30 mm</span>
                        </div>
                        <div className="flex items-center justify-between">
                          <span className="text-xs text-[#a1a1aa]">Góc Xoay Theta:</span>
                          <span className="font-mono font-bold text-base text-[#f59e0b]">+015.00 °</span>
                        </div>
                      </div>
                    )}

                    {/* 5. SPC Gauge */}
                    {w.type === 'spc_gauge' && (
                      <div className="w-full h-full flex flex-col items-center justify-center">
                        <div className="text-3xl font-black font-mono text-[#10b981] mb-1">1.67</div>
                        <div className="text-[11px] text-[#a1a1aa] uppercase font-bold tracking-wider mb-2">
                          Chỉ Số Cpk Đạt Chuẩn 6-Sigma
                        </div>
                        <div className="w-full bg-[#20212b] h-2 rounded-full overflow-hidden">
                          <div className="bg-[#10b981] h-full rounded-full" style={{ width: '85%' }} />
                        </div>
                        <div className="w-full flex justify-between text-[9px] text-[#71717a] mt-1 font-mono">
                          <span>LSL: 1.0</span>
                          <span>Target: 1.33</span>
                          <span>USL: 2.0</span>
                        </div>
                      </div>
                    )}
                  </div>
                </div>
              );
            })}
          </div>

          {/* Floating Action Controls in Runtime Mode */}
          {mode === 'runtime' && (
            <div className="fixed bottom-8 left-1/2 -translate-x-1/2 bg-[#14151c]/90 backdrop-blur-md p-2.5 rounded-xl border border-[#2b2c3a] flex items-center gap-3 shadow-2xl z-50">
              <button
                onClick={handleTriggerRun}
                disabled={isRunning}
                className="px-6 py-2.5 rounded-lg bg-[#00f0ff] hover:bg-[#00d0df] text-[#0a0b0e] font-black text-sm flex items-center gap-2 transition-all shadow-[0_0_20px_rgba(0,240,255,0.4)]"
              >
                <Play className="w-4 h-4 fill-current" />
                <span>CHỤP & KIỂM TRA (TRIGGER F5)</span>
              </button>

              <button
                onClick={() => setIsContinuous(!isContinuous)}
                className={`px-4 py-2.5 rounded-lg border text-xs font-bold flex items-center gap-2 transition-all ${
                  isContinuous
                    ? 'bg-[#10b981]/20 border-[#10b981] text-[#10b981]'
                    : 'bg-[#1e1f29] border-[#2e2f3d] text-[#e4e4e7] hover:border-[#10b981]'
                }`}
              >
                <RefreshCw className={`w-3.5 h-3.5 ${isContinuous ? 'animate-spin' : ''}`} />
                <span>{isContinuous ? 'DỪNG LIÊN TỤC' : 'CHẠY TỰ ĐỘNG'}</span>
              </button>

              <button
                onClick={() => {
                  setTotalCount(0);
                  setPassCount(0);
                  setRejectCount(0);
                }}
                className="px-3 py-2.5 rounded-lg bg-[#252632] hover:bg-[#323342] text-[#a1a1aa] hover:text-white text-xs font-semibold"
                title="Reset bộ đếm ca"
              >
                Reset Ca
              </button>
            </div>
          )}
        </div>

        {/* Right Inspector: In Design Mode only */}
        {mode === 'design' && selectedWidget && (
          <aside className="w-72 bg-[#101116] border-l border-[#22232c] flex flex-col p-4 z-10 shadow-lg overflow-y-auto">
            <div className="text-[11px] font-bold uppercase tracking-wider text-[#71717a] mb-4 flex items-center gap-1.5">
              <Sliders className="w-3.5 h-3.5 text-[#00f0ff]" />
              Thuộc Tính & Data Binding
            </div>

            <div className="flex flex-col gap-3.5 text-xs">
              <div>
                <label className="text-[11px] text-[#a1a1aa] mb-1 block">Tiêu Đề Widget:</label>
                <input
                  type="text"
                  value={selectedWidget.title}
                  onChange={(e) => updateSelectedWidget('title', e.target.value)}
                  className="w-full bg-[#181922] border border-[#262734] rounded px-2.5 py-1.5 text-white outline-none focus:border-[#00f0ff]"
                />
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="text-[10px] text-[#71717a] mb-1 block">Tọa Độ X (px):</label>
                  <input
                    type="number"
                    value={selectedWidget.x}
                    onChange={(e) => updateSelectedWidget('x', parseInt(e.target.value) || 0)}
                    className="w-full bg-[#181922] border border-[#262734] rounded px-2 py-1 text-white font-mono"
                  />
                </div>
                <div>
                  <label className="text-[10px] text-[#71717a] mb-1 block">Tọa Độ Y (px):</label>
                  <input
                    type="number"
                    value={selectedWidget.y}
                    onChange={(e) => updateSelectedWidget('y', parseInt(e.target.value) || 0)}
                    className="w-full bg-[#181922] border border-[#262734] rounded px-2 py-1 text-white font-mono"
                  />
                </div>
              </div>

              <div className="grid grid-cols-2 gap-2">
                <div>
                  <label className="text-[10px] text-[#71717a] mb-1 block">Rộng W (px):</label>
                  <input
                    type="number"
                    value={selectedWidget.width}
                    onChange={(e) => updateSelectedWidget('width', parseInt(e.target.value) || 50)}
                    className="w-full bg-[#181922] border border-[#262734] rounded px-2 py-1 text-white font-mono"
                  />
                </div>
                <div>
                  <label className="text-[10px] text-[#71717a] mb-1 block">Cao H (px):</label>
                  <input
                    type="number"
                    value={selectedWidget.height}
                    onChange={(e) => updateSelectedWidget('height', parseInt(e.target.value) || 50)}
                    className="w-full bg-[#181922] border border-[#262734] rounded px-2 py-1 text-white font-mono"
                  />
                </div>
              </div>

              {/* Data Binding Section */}
              <div className="pt-3 border-t border-[#22232c]">
                <div className="text-[11px] font-bold text-[#00f0ff] uppercase tracking-wider mb-2 flex items-center gap-1.5">
                  <Zap className="w-3.5 h-3.5" />
                  Liên Kết Dữ Liệu (Binding)
                </div>

                <div className="mb-2">
                  <label className="text-[10px] text-[#a1a1aa] mb-1 block">Liên Kết Với Node Đồ Thị:</label>
                  <select
                    value={selectedWidget.bindingNodeId || ''}
                    onChange={(e) => updateSelectedWidget('bindingNodeId', e.target.value)}
                    className="w-full bg-[#181922] border border-[#262734] rounded px-2 py-1.5 text-white outline-none focus:border-[#00f0ff]"
                  >
                    <option value="">-- Chưa liên kết --</option>
                    {nodes.map((n) => (
                      <option key={n.id} value={n.id}>
                        {n.data.label as string} ({n.id})
                      </option>
                    ))}
                  </select>
                </div>

                <div>
                  <label className="text-[10px] text-[#a1a1aa] mb-1 block">Cổng Kết Quả (Output Port):</label>
                  <input
                    type="text"
                    value={selectedWidget.bindingPort || ''}
                    onChange={(e) => updateSelectedWidget('bindingPort', e.target.value)}
                    placeholder="ví dụ: overall_pass, cpk..."
                    className="w-full bg-[#181922] border border-[#262734] rounded px-2.5 py-1.5 text-white font-mono text-xs outline-none focus:border-[#00f0ff]"
                  />
                </div>
              </div>

              {/* Action buttons for widget */}
              <div className="mt-4 pt-3 border-t border-[#22232c] flex flex-col gap-2">
                <button
                  onClick={() => removeWidget(selectedWidget.id)}
                  className="w-full flex items-center justify-center gap-1.5 p-2 bg-[#ef4444]/15 hover:bg-[#ef4444]/25 text-[#ef4444] rounded border border-[#ef4444]/40 font-semibold"
                >
                  <Trash2 className="w-3.5 h-3.5" />
                  <span>Xóa Widget Này</span>
                </button>
              </div>
            </div>
          </aside>
        )}
      </div>
    </div>
  );
};
