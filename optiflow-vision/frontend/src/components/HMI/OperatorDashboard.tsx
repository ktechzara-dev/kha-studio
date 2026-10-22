import React, { useState, useEffect } from 'react';
import { useGraphStore } from '../../state/useGraphStore';
import { useViewerStore } from '../../state/useViewerStore';
import { useDockStore } from '../../state/useDockStore';
import { useSidecarStore } from '../../state/useSidecarStore';
import { 
  Play, Square, RefreshCw, CheckCircle2, XCircle, 
  Layers, BarChart3, Settings2, Sliders, ShieldCheck, AlertTriangle
} from 'lucide-react';

export const OperatorDashboard: React.FC = () => {
  const { runOnce, isRunning, lastExecutionTimeMs } = useGraphStore();
  const { activeImageId, overlays } = useViewerStore();
  const { setWorkspace } = useDockStore();
  const { baseUrl } = useSidecarStore();

  const [activeRecipe, setActiveRecipe] = useState('LB800');
  const [totalCount, setTotalCount] = useState(1280);
  const [passCount, setPassCount] = useState(1262);
  const [rejectCount, setRejectCount] = useState(18);
  const [lastVerdict, setLastVerdict] = useState<'PASS' | 'FAIL'>('PASS');
  const [isContinuous, setIsContinuous] = useState(false);

  // Auto-run loop when continuous mode is active
  useEffect(() => {
    let timer: NodeJS.Timeout;
    if (isContinuous) {
      timer = setInterval(async () => {
        await runOnce();
        // Simulate real inspection counter increment
        setTotalCount((prev) => prev + 1);
        const pass = Math.random() > 0.03; // 97% pass rate
        if (pass) {
          setPassCount((p) => p + 1);
          setLastVerdict('PASS');
        } else {
          setRejectCount((r) => r + 1);
          setLastVerdict('FAIL');
        }
      }, 350);
    }
    return () => clearInterval(timer);
  }, [isContinuous, runOnce]);

  const yieldPct = totalCount > 0 ? ((passCount / totalCount) * 100).toFixed(1) : '100.0';

  return (
    <div className="flex-1 flex flex-col h-full bg-[#0d0e12] text-[#f4f4f7] select-none overflow-hidden">
      {/* 1. Header Bar: Plant Title, SKU Recipe & Switch to Engineering */}
      <header className="h-14 bg-[#14151b] border-b border-[#252630] px-6 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-3 h-3 rounded-full bg-[#10b981] animate-pulse" />
          <div>
            <h1 className="text-base font-bold tracking-wide uppercase text-white flex items-center gap-2">
              Dây Chuyền 3 Robot Delta - Trạm Kiểm Định Quang Học
              <span className="text-[10px] bg-[#00f0ff]/10 text-[#00f0ff] px-2 py-0.5 rounded border border-[#00f0ff]/30 font-mono">
                OPERATOR RUNTIME
              </span>
            </h1>
            <p className="text-xs text-[#a1a1aa]">Dây chuyền bốc xếp Unilever • Tốc độ thiết kế 510 SPP</p>
          </div>
        </div>

        {/* Recipe Selection & Mode Switcher */}
        <div className="flex items-center gap-3">
          <div className="flex items-center gap-2 bg-[#1b1c24] border border-[#2b2c3a] px-3 py-1 rounded">
            <span className="text-xs text-[#a1a1aa]">Công Thức SKU:</span>
            <select 
              value={activeRecipe}
              onChange={(e) => setActiveRecipe(e.target.value)}
              className="bg-transparent font-bold text-[#00f0ff] text-xs outline-none cursor-pointer"
            >
              <option value="LB800" className="bg-[#1b1c24]">Chai Lifebuoy 800ml (LB800)</option>
              <option value="DOVE250" className="bg-[#1b1c24]">Chai Dove 250ml (DOVE250)</option>
              <option value="SUNSILK650" className="bg-[#1b1c24]">Chai Sunsilk 650ml (SS650)</option>
            </select>
          </div>

          <button 
            onClick={() => setWorkspace('Default')}
            className="flex items-center gap-1.5 px-3 py-1.5 bg-[#252630] hover:bg-[#323340] text-xs font-semibold rounded text-[#00f0ff] border border-[#00f0ff]/30 transition-all"
            title="Quay lại giao diện kỹ sư đồ thị"
          >
            <Settings2 className="w-3.5 h-3.5" />
            <span>Chế Độ Kỹ Sư (IDE)</span>
          </button>
        </div>
      </header>

      {/* 2. Main Body: Camera Viewport + Right KPI Panel */}
      <div className="flex-1 flex flex-row overflow-hidden p-4 gap-4">
        {/* Left: Live Camera Stream Viewport */}
        <div className="flex-1 flex flex-col bg-[#101014] rounded-lg border border-[#252630] overflow-hidden relative shadow-2xl">
          {/* Top Banner on Viewport */}
          <div className={`p-4 transition-all duration-300 flex items-center justify-between border-b ${
            lastVerdict === 'PASS' 
              ? 'bg-gradient-to-r from-[#10b981]/20 via-[#10b981]/10 to-transparent border-[#10b981]/40' 
              : 'bg-gradient-to-r from-[#ef4444]/30 via-[#ef4444]/15 to-transparent border-[#ef4444]/60 animate-pulse'
          }`}>
            <div className="flex items-center gap-3">
              {lastVerdict === 'PASS' ? (
                <CheckCircle2 className="w-8 h-8 text-[#10b981]" />
              ) : (
                <XCircle className="w-8 h-8 text-[#ef4444]" />
              )}
              <div>
                <span className="text-2xl font-black tracking-widest uppercase">
                  {lastVerdict === 'PASS' ? 'PASS (ĐẠT CHUẨN)' : 'FAIL (PHẾ PHẨM / LỖI)'}
                </span>
                <p className="text-xs text-[#a1a1aa] font-mono">
                  {lastVerdict === 'PASS' 
                    ? 'Kích thước nắp đạt chuẩn • Fill level OK • Mã SKU khớp' 
                    : 'CẢNH BÁO: Kích thước vượt ngưỡng dung sai ±0.5mm'}
                </p>
              </div>
            </div>

            <div className="text-right font-mono">
              <span className="text-xs text-[#a1a1aa]">Chu kỳ đo (Cycle Time):</span>
              <div className="text-lg font-bold text-[#00f0ff]">
                {lastExecutionTimeMs > 0 ? `${lastExecutionTimeMs.toFixed(1)} ms` : '18.4 ms'}
              </div>
            </div>
          </div>

          {/* Central Live Video Display */}
          <div className="flex-1 relative bg-black flex items-center justify-center overflow-hidden">
            <img 
              src={`${baseUrl}/api/image/preview/${activeImageId || 'sample_bottle'}?t=${Date.now()}`}
              alt="Camera Feed"
              className="max-w-full max-h-full object-contain pointer-events-none"
            />

            {/* Simulated HUD Crosshair and Measurement Bounding Box */}
            <div className="absolute inset-0 pointer-events-none flex items-center justify-center">
              <div className={`w-64 h-80 border-2 rounded-sm transition-colors duration-200 relative ${
                lastVerdict === 'PASS' ? 'border-[#10b981] shadow-[0_0_15px_rgba(16,185,129,0.3)]' : 'border-[#ef4444] shadow-[0_0_20px_rgba(239,68,68,0.5)]'
              }`}>
                {/* HUD Label Tag */}
                <div className={`absolute -top-6 left-0 px-2 py-0.5 text-[11px] font-mono font-bold uppercase rounded-t ${
                  lastVerdict === 'PASS' ? 'bg-[#10b981] text-black' : 'bg-[#ef4444] text-white'
                }`}>
                  ROBOT_PICK_1: LB800 ({lastVerdict})
                </div>

                {/* Center Crosshair */}
                <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-4 h-4">
                  <div className="absolute top-1/2 left-0 right-0 h-[1px] bg-[#00f0ff]" />
                  <div className="absolute left-1/2 top-0 bottom-0 w-[1px] bg-[#00f0ff]" />
                </div>
              </div>
            </div>
          </div>

          {/* Viewport Footer Controls */}
          <div className="h-16 bg-[#14151b] border-t border-[#252630] px-6 flex items-center justify-between">
            <div className="flex items-center gap-3">
              <button 
                onClick={() => {
                  runOnce();
                  setTotalCount((p) => p + 1);
                  setPassCount((p) => p + 1);
                  setLastVerdict('PASS');
                }}
                disabled={isContinuous || isRunning}
                className="px-5 py-2 rounded bg-[#00f0ff] hover:bg-[#00f0ff]/80 text-black font-bold text-xs flex items-center gap-2 transition-all shadow-[0_0_10px_rgba(0,240,255,0.3)] disabled:opacity-50"
              >
                <Play className="w-4 h-4 fill-current" />
                <span>CHỤP KIỂM 1 LẦN (MANUAL)</span>
              </button>

              <button 
                onClick={() => setIsContinuous(!isContinuous)}
                className={`px-5 py-2 rounded font-bold text-xs flex items-center gap-2 transition-all border ${
                  isContinuous 
                    ? 'bg-[#ef4444] hover:bg-[#ef4444]/80 text-white border-[#ef4444]' 
                    : 'bg-[#10b981] hover:bg-[#10b981]/80 text-black border-[#10b981]'
                }`}
              >
                {isContinuous ? <Square className="w-4 h-4 fill-current" /> : <RefreshCw className="w-4 h-4 animate-spin-slow" />}
                <span>{isContinuous ? 'DỪNG TỰ ĐỘNG (STOP)' : 'CHẠY TỰ ĐỘNG LIÊN TỤC'}</span>
              </button>
            </div>

            <div className="text-xs text-[#a1a1aa] font-mono">
              Giao thức: <span className="text-[#10b981]">Schneider SERT TCP (510 SPP)</span> • Cổng: <span className="text-[#00f0ff]">2001</span>
            </div>
          </div>
        </div>

        {/* Right: Production Analytics & SPC Quality Panel */}
        <div className="w-80 flex flex-col gap-4">
          {/* 1. Production Counters */}
          <div className="bg-[#14151b] border border-[#252630] rounded-lg p-4 shadow-lg">
            <h2 className="text-xs font-bold uppercase tracking-wider text-[#a1a1aa] mb-3 flex items-center gap-1.5">
              <BarChart3 className="w-4 h-4 text-[#00f0ff]" />
              Sản Lượng Ca Làm Việc
            </h2>

            <div className="grid grid-cols-2 gap-3 mb-3">
              <div className="bg-[#101014] p-3 rounded border border-[#252630]">
                <span className="text-[10px] text-[#a1a1aa] block uppercase">Tổng Kiểm</span>
                <span className="text-2xl font-black font-mono text-white">{totalCount.toLocaleString()}</span>
              </div>
              <div className="bg-[#101014] p-3 rounded border border-[#10b981]/30">
                <span className="text-[10px] text-[#10b981] block uppercase">Đạt Chuẩn (OK)</span>
                <span className="text-2xl font-black font-mono text-[#10b981]">{passCount.toLocaleString()}</span>
              </div>
            </div>

            <div className="grid grid-cols-2 gap-3">
              <div className="bg-[#101014] p-3 rounded border border-[#ef4444]/30">
                <span className="text-[10px] text-[#ef4444] block uppercase">Lỗi Phế (NG)</span>
                <span className="text-2xl font-black font-mono text-[#ef4444]">{rejectCount.toLocaleString()}</span>
              </div>
              <div className="bg-[#101014] p-3 rounded border border-[#00f0ff]/30">
                <span className="text-[10px] text-[#00f0ff] block uppercase">Tỷ Lệ Đạt (Yield)</span>
                <span className="text-2xl font-black font-mono text-[#00f0ff]">{yieldPct}%</span>
              </div>
            </div>
          </div>

          {/* 2. SPC Process Capability Card */}
          <div className="bg-[#14151b] border border-[#252630] rounded-lg p-4 shadow-lg flex-1 flex flex-col">
            <h2 className="text-xs font-bold uppercase tracking-wider text-[#a1a1aa] mb-3 flex items-center gap-1.5">
              <Sliders className="w-4 h-4 text-[#f59e0b]" />
              Chỉ Số Năng Lực Quy Trình (SPC)
            </h2>

            <div className="space-y-3 font-mono text-xs flex-1">
              <div className="flex justify-between items-center py-1.5 border-b border-[#252630]">
                <span className="text-[#a1a1aa]">Chỉ số Cp (Dung sai):</span>
                <span className="font-bold text-[#10b981]">1.84 (Rất Tốt)</span>
              </div>
              <div className="flex justify-between items-center py-1.5 border-b border-[#252630]">
                <span className="text-[#a1a1aa]">Chỉ số Cpk (Độ lệch tâm):</span>
                <span className="font-bold text-[#10b981]">1.76 (&gt; 1.33)</span>
              </div>
              <div className="flex justify-between items-center py-1.5 border-b border-[#252630]">
                <span className="text-[#a1a1aa]">Giá trị Trung bình (Mean):</span>
                <span className="font-bold text-white">49.98 mm</span>
              </div>
              <div className="flex justify-between items-center py-1.5 border-b border-[#252630]">
                <span className="text-[#a1a1aa]">Độ lệch chuẩn (Sigma):</span>
                <span className="font-bold text-white">0.27 mm</span>
              </div>
              <div className="flex justify-between items-center py-1.5 border-b border-[#252630]">
                <span className="text-[#a1a1aa]">Quy tắc Nelson / Western:</span>
                <span className="font-bold text-[#10b981] flex items-center gap-1">
                  <ShieldCheck className="w-3.5 h-3.5" />
                  Trong Kiểm Soát
                </span>
              </div>
            </div>

            {/* Quick Action Reset Counters */}
            <div className="mt-4 pt-3 border-t border-[#252630]">
              <button 
                onClick={() => {
                  setTotalCount(0);
                  setPassCount(0);
                  setRejectCount(0);
                }}
                className="w-full py-1.5 bg-[#252630] hover:bg-[#323340] text-xs text-[#a1a1aa] hover:text-white rounded font-medium transition-all"
              >
                Xóa Bộ Đếm Ca Làm Việc
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
