import React from 'react';
import { useI18n } from '../../i18n';
import { useViewerStore } from '../../state/useViewerStore';
import { BarChart2, X } from 'lucide-react';

export const HistogramOverlay: React.FC = () => {
  const { t } = useI18n();
  const { histogram, showHistogram, setShowHistogram } = useViewerStore();

  if (!showHistogram || !histogram || histogram.bins.length === 0) return null;

  const maxBin = Math.max(...histogram.bins, 1);
  const svgWidth = 256;
  const svgHeight = 70;

  // Build SVG path
  const points = histogram.bins.map((val, idx) => {
    const x = idx;
    const y = svgHeight - (val / maxBin) * (svgHeight - 4);
    return `${x},${y}`;
  });

  const pathD = `M 0,${svgHeight} L ${points.join(' L ')} L ${svgWidth},${svgHeight} Z`;

  return (
    <div className="absolute right-4 bottom-4 w-72 hud-panel p-3 text-xs select-none shadow-2xl z-20">
      {/* Header */}
      <div className="flex items-center justify-between pb-2 mb-2 border-b border-[#26262c]">
        <div className="flex items-center gap-1.5 text-[#f59e0b] font-bold text-[11px]">
          <BarChart2 className="w-3.5 h-3.5" />
          <span>{t('viewer.histogram_title')}</span>
        </div>
        <button 
          onClick={() => setShowHistogram(false)}
          className="text-[#71717a] hover:text-white p-0.5 rounded"
        >
          <X className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* SVG Curve */}
      <div className="bg-[#0e0e11] border border-[#26262c] rounded p-1 mb-2">
        <svg 
          viewBox={`0 0 ${svgWidth} ${svgHeight}`} 
          className="w-full h-16 overflow-visible"
        >
          {/* Fill Gradient */}
          <defs>
            <linearGradient id="histGrad" x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor="#f59e0b" stopOpacity="0.5" />
              <stop offset="100%" stopColor="#f59e0b" stopOpacity="0.05" />
            </linearGradient>
          </defs>
          <path d={pathD} fill="url(#histGrad)" />
          <polyline
            fill="none"
            stroke="#f59e0b"
            strokeWidth="1.2"
            points={points.join(' ')}
          />
        </svg>
        <div className="flex justify-between text-[9px] text-[#71717a] font-mono px-1">
          <span>0 (Đen)</span>
          <span>128</span>
          <span>255 (Trắng)</span>
        </div>
      </div>

      {/* Numerical Stats Grid */}
      <div className="grid grid-cols-4 gap-1 text-[10px] font-mono">
        <div className="bg-[#1b1b1f] p-1.5 rounded border border-[#26262c] text-center">
          <div className="text-[#71717a] text-[9px]">MIN</div>
          <div className="text-[#e4e4e7] font-bold">{Math.round(histogram.min_val)}</div>
        </div>
        <div className="bg-[#1b1b1f] p-1.5 rounded border border-[#26262c] text-center">
          <div className="text-[#71717a] text-[9px]">MAX</div>
          <div className="text-[#e4e4e7] font-bold">{Math.round(histogram.max_val)}</div>
        </div>
        <div className="bg-[#1b1b1f] p-1.5 rounded border border-[#26262c] text-center">
          <div className="text-[#71717a] text-[9px]">MEAN</div>
          <div className="text-[#00f0ff] font-bold">{histogram.mean_val.toFixed(1)}</div>
        </div>
        <div className="bg-[#1b1b1f] p-1.5 rounded border border-[#26262c] text-center">
          <div className="text-[#71717a] text-[9px]">STD</div>
          <div className="text-[#10b981] font-bold">{histogram.std_val.toFixed(1)}</div>
        </div>
      </div>
    </div>
  );
};
