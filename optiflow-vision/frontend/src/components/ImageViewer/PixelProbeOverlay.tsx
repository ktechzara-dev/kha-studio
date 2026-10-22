import React from 'react';
import { useI18n } from '../../i18n';
import { useViewerStore } from '../../state/useViewerStore';
import { Crosshair, X } from 'lucide-react';

export const PixelProbeOverlay: React.FC = () => {
  const { t } = useI18n();
  const { probeResult, showProbe, setShowProbe } = useViewerStore();

  if (!showProbe || !probeResult || !probeResult.in_bounds || probeResult.neighborhood_7x7.length === 0) {
    return null;
  }

  const grid = probeResult.neighborhood_7x7;

  return (
    <div className="absolute left-4 bottom-4 w-64 hud-panel p-3 text-xs select-none shadow-2xl z-20">
      {/* Header */}
      <div className="flex items-center justify-between pb-2 mb-2 border-b border-[#26262c]">
        <div className="flex items-center gap-1.5 text-[#10b981] font-bold text-[11px]">
          <Crosshair className="w-3.5 h-3.5" />
          <span>{t('viewer.pixel_probe_title')}</span>
        </div>
        <button 
          onClick={() => setShowProbe(false)}
          className="text-[#71717a] hover:text-white p-0.5 rounded"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* Coordinate & Value Badge */}
      <div className="flex items-center justify-between font-mono text-[11px] mb-2 px-1">
        <span className="text-[#a1a1aa]">({probeResult.x}, {probeResult.y})</span>
        <span className="text-[#00f0ff] font-bold bg-[#00f0ff]/10 px-2 py-0.5 rounded border border-[#00f0ff]/30">
          {probeResult.intensity_text}
        </span>
      </div>

      {/* 7x7 Grid */}
      <div className="grid grid-cols-7 gap-0.5 bg-[#0e0e11] p-1 rounded border border-[#26262c]">
        {grid.map((row, rIdx) =>
          row.map((val, cIdx) => {
            const isCenter = rIdx === 3 && cIdx === 3;
            return (
              <div
                key={`${rIdx}-${cIdx}`}
                className={`h-5 flex items-center justify-center font-mono text-[8px] rounded transition-all ${
                  isCenter
                    ? 'bg-[#00f0ff] text-black font-extrabold ring-2 ring-[#00f0ff] z-10'
                    : 'bg-[#1b1b1f] text-[#a1a1aa]'
                }`}
                style={{
                  backgroundColor: isCenter ? '#00f0ff' : `rgb(${val}, ${val}, ${val})`,
                  color: isCenter ? '#000' : (val > 128 ? '#000' : '#fff'),
                }}
                title={`(${probeResult.x + cIdx - 3}, ${probeResult.y + rIdx - 3}): ${val}`}
              >
                {val}
              </div>
            );
          })
        )}
      </div>
    </div>
  );
};
