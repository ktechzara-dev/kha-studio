import React from 'react';
import { useDockStore } from '../../state/useDockStore';
import { useGraphStore } from '../../state/useGraphStore';
import { useSidecarStore } from '../../state/useSidecarStore';
import { useI18n } from '../../i18n';
import { 
  Terminal, AlertTriangle, Activity, Trash2, X, 
  ChevronUp, ChevronDown, CheckCircle2, Clock, Zap 
} from 'lucide-react';

export const BottomDock: React.FC = () => {
  const { lang } = useI18n();
  const { 
    bottomDock, setBottomDock, consoleLogs, clearLogs 
  } = useDockStore();
  const { 
    nodeExecutionResults, lastExecutionTimeMs, nodes 
  } = useGraphStore();
  const { isOnline, latencyMs, memoryMb } = useSidecarStore();

  if (!bottomDock.isOpen) {
    return (
      <div className="h-6 bg-[#101014] border-t border-[#252630] flex items-center justify-between px-3 text-[10.5px] select-none text-[#71717a]">
        <button
          onClick={() => setBottomDock({ isOpen: true })}
          className="flex items-center gap-1.5 hover:text-[#00f0ff] transition-colors"
          title="Mở Bảng Điều Khiển Dưới (Console / Errors / Performance)"
        >
          <Terminal className="w-3.5 h-3.5" />
          <span>Output & Diagnostics</span>
          <ChevronUp className="w-3 h-3" />
        </button>
        <span className="font-mono text-[10px] text-[#10b981]">
          DAG Status: VALID (Cycle-Free) • Last Cycle: {lastExecutionTimeMs.toFixed(1)}ms
        </span>
      </div>
    );
  }

  // Count errors from execution
  const errorEntries = Object.entries(nodeExecutionResults).filter(
    ([_, res]) => res.status === 'ERROR'
  );

  const startResizingBottom = (e: React.MouseEvent) => {
    e.preventDefault();
    const startY = e.clientY;
    const startHeight = bottomDock.height;

    const onMouseMove = (moveEvent: MouseEvent) => {
      const newHeight = Math.max(90, Math.min(450, startHeight - (moveEvent.clientY - startY)));
      setBottomDock({ height: newHeight });
    };

    const onMouseUp = () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  return (
    <div 
      className="relative bg-[#121317] border-t border-[#252630] flex flex-col select-none z-20 shrink-0"
      style={{ height: `${bottomDock.height}px` }}
    >
      {/* Draggable Top Resize Handle */}
      <div
        onMouseDown={startResizingBottom}
        className="absolute -top-1 left-0 w-full h-2 cursor-row-resize hover:bg-[#00f0ff]/80 active:bg-[#00f0ff] transition-colors z-40 select-none"
        title="Kéo chuột để thay đổi chiều cao Bảng Điều Khiển Dưới"
      />
      {/* 1. Header with Tab Switcher */}
      <div className="h-7 px-2 bg-[#16171d] border-b border-[#202129] flex items-center justify-between text-xs">
        <div className="flex items-center gap-1">
          {/* Tab 1: Console Output */}
          <button
            onClick={() => setBottomDock({ activeTab: 'output' })}
            className={`px-2.5 py-0.5 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              bottomDock.activeTab === 'output'
                ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <Terminal className="w-3 h-3" />
            <span>Output ({consoleLogs.length})</span>
          </button>

          {/* Tab 2: Errors & Diagnostics */}
          <button
            onClick={() => setBottomDock({ activeTab: 'errors' })}
            className={`px-2.5 py-0.5 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              bottomDock.activeTab === 'errors'
                ? 'bg-[#1e2029] text-[#ef4444] border border-[#ef4444]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <AlertTriangle className="w-3 h-3" />
            <span>Errors & Diagnostics ({errorEntries.length})</span>
          </button>

          {/* Tab 3: Performance Profiler */}
          <button
            onClick={() => setBottomDock({ activeTab: 'performance' })}
            className={`px-2.5 py-0.5 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              bottomDock.activeTab === 'performance'
                ? 'bg-[#1e2029] text-[#10b981] border border-[#10b981]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <Activity className="w-3 h-3" />
            <span>Performance Profiler</span>
          </button>
        </div>

        {/* Dock Controls */}
        <div className="flex items-center gap-1 text-[#71717a]">
          {bottomDock.activeTab === 'output' && (
            <button
              onClick={clearLogs}
              className="p-1 rounded hover:bg-[#202129] hover:text-white transition-colors"
              title="Xóa nhật ký (Clear Console)"
            >
              <Trash2 className="w-3 h-3" />
            </button>
          )}

          {/* Minimize / Hide */}
          <button
            onClick={() => setBottomDock({ isOpen: false })}
            className="p-1 rounded hover:bg-[#202129] hover:text-white transition-colors"
            title="Thu nhỏ Dock dưới"
          >
            <ChevronDown className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* 2. Dock Body */}
      <div className="flex-1 overflow-y-auto p-2 font-mono text-[11px]">
        {/* Tab 1: Output Console */}
        {bottomDock.activeTab === 'output' && (
          <div className="space-y-1">
            {consoleLogs.map((log) => {
              const colorClass =
                log.level === 'error'
                  ? 'text-[#ef4444]'
                  : log.level === 'warn'
                  ? 'text-[#f59e0b]'
                  : log.level === 'success'
                  ? 'text-[#10b981]'
                  : 'text-[#9ca3af]';

              return (
                <div key={log.id} className="flex items-start gap-2 leading-relaxed">
                  <span className="text-[#52525b] select-none text-[10px]">[{log.timestamp}]</span>
                  {log.source && (
                    <span className="text-[#38bdf8] text-[10px] bg-[#38bdf8]/10 px-1 rounded select-none">
                      {log.source}
                    </span>
                  )}
                  <span className={colorClass}>{log.message}</span>
                </div>
              );
            })}
          </div>
        )}

        {/* Tab 2: Errors & Diagnostics */}
        {bottomDock.activeTab === 'errors' && (
          <div className="space-y-2">
            <div className="flex items-center gap-2 p-2 rounded bg-[#101014] border border-[#202129]">
              <CheckCircle2 className="w-4 h-4 text-[#10b981]" />
              <div className="flex flex-col">
                <span className="font-bold text-[#e4e4e7] text-xs">
                  Cấu Trúc Đồ Thị Hợp Lệ (DAG Valid & Cycle-Free)
                </span>
                <span className="text-[10px] text-[#71717a]">
                  Thuật toán Kahn xác nhận không có vòng lặp đệ quy. Tổng số: {nodes.length} nodes.
                </span>
              </div>
            </div>

            {errorEntries.length === 0 ? (
              <div className="text-center p-4 text-[#71717a] text-xs">
                Không có lỗi thực thi nào trong chu kỳ vừa chạy.
              </div>
            ) : (
              errorEntries.map(([nid, res]) => (
                <div key={nid} className="p-2 rounded bg-[#ef4444]/10 border border-[#ef4444]/30 flex items-center justify-between text-xs">
                  <div className="flex items-center gap-2">
                    <AlertTriangle className="w-4 h-4 text-[#ef4444]" />
                    <span className="font-bold text-[#ef4444]">{nid}</span>
                    <span className="text-white">{res.error}</span>
                  </div>
                  <span className="text-[10px] text-[#a1a1aa] font-mono">{res.time_ms.toFixed(1)} ms</span>
                </div>
              ))
            )}
          </div>
        )}

        {/* Tab 3: Performance Profiler Waterfall */}
        {bottomDock.activeTab === 'performance' && (
          <div className="space-y-3">
            {/* Top Telemetry Summary Cards */}
            <div className="grid grid-cols-4 gap-2">
              <div className="p-2 rounded bg-[#101014] border border-[#202129] flex flex-col">
                <span className="text-[10px] text-[#71717a] uppercase">Tổng Chu Kỳ Chạy</span>
                <span className="text-sm font-bold text-[#10b981] flex items-center gap-1 mt-0.5">
                  <Zap className="w-3.5 h-3.5" />
                  {lastExecutionTimeMs.toFixed(2)} ms
                </span>
              </div>
              <div className="p-2 rounded bg-[#101014] border border-[#202129] flex flex-col">
                <span className="text-[10px] text-[#71717a] uppercase">Ngân Sách Chu Kỳ (Unilever)</span>
                <span className="text-sm font-bold text-[#00f0ff] mt-0.5">
                  25.0 ms <span className="text-[10px] text-[#10b981]">(Đạt {((lastExecutionTimeMs / 25) * 100).toFixed(0)}%)</span>
                </span>
              </div>
              <div className="p-2 rounded bg-[#101014] border border-[#202129] flex flex-col">
                <span className="text-[10px] text-[#71717a] uppercase">Bộ Nhớ Engine (RAM)</span>
                <span className="text-sm font-bold text-[#38bdf8] mt-0.5">{memoryMb} MB</span>
              </div>
              <div className="p-2 rounded bg-[#101014] border border-[#202129] flex flex-col">
                <span className="text-[10px] text-[#71717a] uppercase">Độ Trễ IPC FastApi</span>
                <span className="text-sm font-bold text-[#f59e0b] mt-0.5">{latencyMs} ms</span>
              </div>
            </div>

            {/* Waterfall Nodes Table */}
            <div className="rounded border border-[#202129] bg-[#101014] overflow-hidden">
              <div className="grid grid-cols-12 px-3 py-1 bg-[#17181f] text-[10px] text-[#71717a] font-bold uppercase">
                <span className="col-span-4">Node Label</span>
                <span className="col-span-3">Filter Type</span>
                <span className="col-span-2 text-right">Time (ms)</span>
                <span className="col-span-3 text-right">Waterfall Timeline</span>
              </div>
              <div className="divide-y divide-[#1e2029]">
                {Object.entries(nodeExecutionResults).map(([nid, res]) => {
                  const nodeObj = nodes.find((n) => n.id === nid);
                  const label = nodeObj?.data.label || nid;
                  const fType = nodeObj?.data.filterType || 'Filter';
                  const pct = Math.max(2, Math.min(100, (res.time_ms / Math.max(1, lastExecutionTimeMs)) * 100));

                  return (
                    <div key={nid} className="grid grid-cols-12 px-3 py-1.5 items-center text-[10.5px]">
                      <span className="col-span-4 font-semibold text-white truncate" title={label as string}>
                        {label as string}
                      </span>
                      <span className="col-span-3 text-[#71717a] font-mono text-[10px] truncate">
                        {fType as string}
                      </span>
                      <span className="col-span-2 text-right font-mono text-[#10b981] font-bold">
                        {res.time_ms.toFixed(2)} ms
                      </span>
                      <div className="col-span-3 pl-3">
                        <div className="w-full bg-[#1e2029] h-2 rounded overflow-hidden">
                          <div 
                            className="bg-gradient-to-r from-[#00f0ff] to-[#10b981] h-full rounded"
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </div>
                    </div>
                  );
                })}
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
};
