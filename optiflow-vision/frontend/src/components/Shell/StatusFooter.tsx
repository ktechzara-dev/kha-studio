import React from 'react';
import { useI18n } from '../../i18n';
import { useSidecarStore } from '../../state/useSidecarStore';
import { useViewerStore } from '../../state/useViewerStore';
import { Cpu, HardDrive, Compass, Info, CheckCircle2 } from 'lucide-react';

export const StatusFooter: React.FC = () => {
  const { t } = useI18n();
  const { isOnline, pid, memoryMb, latencyMs } = useSidecarStore();
  const { activeImageMeta, cursorPos, probeResult, scale } = useViewerStore();

  return (
    <footer className="h-7 bg-[#0e0e11] border-t border-[#26262c] flex items-center justify-between px-3 text-[11px] font-mono text-[#a1a1aa] select-none z-40">
      {/* LEFT: Engine & IPC Health */}
      <div className="flex items-center gap-3">
        <div className="flex items-center gap-1.5">
          <span className={`w-2 h-2 rounded-full ${isOnline ? 'bg-[#10b981] shadow-[0_0_8px_#10b981]' : 'bg-[#ef4444]'}`} />
          <span className={`font-semibold ${isOnline ? 'text-[#10b981]' : 'text-[#ef4444]'}`}>
            {isOnline ? t('status.engine_online') : t('status.engine_offline')}
          </span>
        </div>

        {isOnline && (
          <>
            <span className="text-[#3f3f46]">|</span>
            <div className="flex items-center gap-1 text-[#71717a]">
              <Cpu className="w-3 h-3 text-[#00f0ff]" />
              <span>PID: {pid || '--'}</span>
            </div>
            <span className="text-[#3f3f46]">|</span>
            <div className="flex items-center gap-1 text-[#71717a]">
              <HardDrive className="w-3 h-3 text-[#38bdf8]" />
              <span>RAM: {memoryMb} MB</span>
            </div>
          </>
        )}
      </div>

      {/* CENTER: Feature Summary */}
      <div className="hidden lg:flex items-center gap-1.5 text-[#00f0ff] font-sans font-medium text-[10.5px]">
        <CheckCircle2 className="w-3 h-3 text-[#10b981]" />
        <span>OptiFlow Vision Studio • DAG Topological Flow • 25 Filters • 12MP Inspection • FB_CognexCamera SERT</span>
      </div>

      {/* RIGHT: Pixel Probe & Coordinates HUD */}
      <div className="flex items-center gap-3">
        {activeImageMeta && (
          <div className="flex items-center gap-2">
            <span className="text-[#e4e4e7] font-semibold">
              {activeImageMeta.width}x{activeImageMeta.height}
            </span>
            <span className="px-1.5 py-0.2 rounded bg-[#26262c] text-[10px] text-[#00f0ff]">
              {activeImageMeta.color_space} ({activeImageMeta.dtype})
            </span>
          </div>
        )}

        <span className="text-[#3f3f46]">|</span>

        {/* Cursor Coordinates */}
        <div className="flex items-center gap-1.5 min-w-[120px]">
          <Compass className="w-3 h-3 text-[#f59e0b]" />
          <span>
            {cursorPos 
              ? `X: ${String(cursorPos.x).padStart(4, '0')} Y: ${String(cursorPos.y).padStart(4, '0')}` 
              : 'X: ---- Y: ----'}
          </span>
        </div>

        <span className="text-[#3f3f46]">|</span>

        {/* Probe Value */}
        <div className="min-w-[110px] text-[#00f0ff]">
          {probeResult?.in_bounds 
            ? probeResult.intensity_text 
            : '--'}
        </div>
      </div>
    </footer>
  );
};
