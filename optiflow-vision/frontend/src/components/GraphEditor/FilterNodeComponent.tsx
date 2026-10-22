import React, { memo } from 'react';
import { Handle, Position, NodeProps } from '@xyflow/react';
import { useGraphStore } from '../../state/useGraphStore';
import { 
  Sliders, Target, Ruler, Cpu, Layers, Camera, Radio, 
  CheckCircle2, AlertCircle, Clock, EyeOff
} from 'lucide-react';

const CATEGORY_COLORS: Record<string, { border: string; bg: string; text: string; glow: string }> = {
  Processing: { border: '#00f0ff', bg: 'rgba(0, 240, 255, 0.1)', text: '#00f0ff', glow: 'rgba(0, 240, 255, 0.3)' },
  Analysis: { border: '#10b981', bg: 'rgba(16, 185, 129, 0.1)', text: '#10b981', glow: 'rgba(16, 185, 129, 0.3)' },
  Measurement: { border: '#f59e0b', bg: 'rgba(245, 158, 11, 0.1)', text: '#f59e0b', glow: 'rgba(245, 158, 11, 0.3)' },
  Logic: { border: '#a855f7', bg: 'rgba(168, 85, 247, 0.1)', text: '#a855f7', glow: 'rgba(168, 85, 247, 0.3)' },
  Overlays: { border: '#ec4899', bg: 'rgba(236, 72, 153, 0.1)', text: '#ec4899', glow: 'rgba(236, 72, 153, 0.3)' },
  Acquisition: { border: '#0284c7', bg: 'rgba(2, 132, 199, 0.1)', text: '#38bdf8', glow: 'rgba(2, 132, 199, 0.3)' },
  Communication: { border: '#f97316', bg: 'rgba(249, 115, 22, 0.1)', text: '#fb923c', glow: 'rgba(249, 115, 22, 0.3)' },
};

const TYPE_COLORS: Record<string, string> = {
  Image: '#00f0ff',
  Region: '#10b981',
  Point2D: '#eab308',
  Rectangle2D: '#f59e0b',
  Circle2D: '#84cc16',
  Number: '#38bdf8',
  Integer: '#60a5fa',
  Boolean: '#a855f7',
  String: '#94a3b8',
  Array: '#f97316',
  Any: '#cbd5e1',
};

const ICON_MAP: Record<string, any> = {
  Processing: Sliders,
  Analysis: Target,
  Measurement: Ruler,
  Logic: Cpu,
  Overlays: Layers,
  Acquisition: Camera,
  Communication: Radio,
};

export const FilterNodeComponent: React.FC<NodeProps> = memo(({ id, data, selected }) => {
  const { filterDefs, nodeExecutionResults, selectedNodeId, setSelectedNodeId } = useGraphStore();

  const filterType = data.filterType as string;
  const def = filterDefs[filterType];
  const category = (data.category as string) || 'Processing';
  const colors = CATEGORY_COLORS[category] || CATEGORY_COLORS.Processing;
  const IconComponent = ICON_MAP[category] || Sliders;

  const execResult = nodeExecutionResults[id];
  const isDisabled = Boolean(data.is_disabled);

  return (
    <div
      onClick={() => setSelectedNodeId(id)}
      className={`min-w-[240px] rounded-lg bg-[#141417]/95 backdrop-blur-md text-xs select-none transition-all duration-150 ${
        selected || selectedNodeId === id
          ? 'ring-2 ring-[#00f0ff] shadow-[0_0_20px_rgba(0,240,255,0.35)]'
          : 'border border-[#26262c] shadow-xl hover:border-[#3f3f46]'
      } ${isDisabled ? 'opacity-50' : ''}`}
    >
      {/* 1. Header with Category Bar */}
      <div 
        className="px-3 py-2 rounded-t-lg flex items-center justify-between border-b border-[#26262c]"
        style={{ backgroundColor: colors.bg }}
      >
        <div className="flex items-center gap-2">
          <div 
            className="w-5 h-5 rounded flex items-center justify-center border"
            style={{ borderColor: colors.border, color: colors.text }}
          >
            <IconComponent className="w-3.5 h-3.5" />
          </div>
          <div className="flex flex-col">
            <span className="font-bold text-[12px] text-[#f4f4f7] truncate max-w-[150px]">
              {data.label as string}
            </span>
            <span 
              className="text-[9px] font-semibold uppercase tracking-wider"
              style={{ color: colors.text }}
            >
              {category}
            </span>
          </div>
        </div>

        {isDisabled && (
          <EyeOff className="w-3.5 h-3.5 text-[#71717a]" title="Node is disabled" />
        )}
      </div>

      {/* 2. Ports Container */}
      <div className="p-2.5 flex justify-between gap-4">
        {/* Left: Inputs */}
        <div className="flex flex-col gap-2 flex-1">
          {def?.inputs.map((inp) => {
            const dotColor = TYPE_COLORS[inp.data_type] || '#cbd5e1';
            return (
              <div key={inp.name} className="relative flex items-center gap-2 text-[10.5px]">
                <Handle
                  type="target"
                  position={Position.Left}
                  id={inp.name}
                  style={{
                    backgroundColor: dotColor,
                    borderColor: '#0e0e11',
                    borderWidth: 2,
                    width: 10,
                    height: 10,
                    left: -15,
                  }}
                  title={`${inp.name} (${inp.data_type})`}
                />
                <span 
                  className="w-1.5 h-1.5 rounded-full" 
                  style={{ backgroundColor: dotColor }} 
                />
                <span className="text-[#a1a1aa] font-medium truncate max-w-[90px]" title={inp.name}>
                  {inp.name}
                </span>
              </div>
            );
          })}
        </div>

        {/* Right: Outputs */}
        <div className="flex flex-col gap-2 items-end flex-1">
          {def?.outputs.map((out) => {
            const dotColor = TYPE_COLORS[out.data_type] || '#cbd5e1';
            return (
              <div key={out.name} className="relative flex items-center gap-2 text-[10.5px]">
                <span className="text-[#e4e4e7] font-medium truncate max-w-[90px]" title={out.name}>
                  {out.name}
                </span>
                <span 
                  className="w-1.5 h-1.5 rounded-full" 
                  style={{ backgroundColor: dotColor }} 
                />
                <Handle
                  type="source"
                  position={Position.Right}
                  id={out.name}
                  style={{
                    backgroundColor: dotColor,
                    borderColor: '#0e0e11',
                    borderWidth: 2,
                    width: 10,
                    height: 10,
                    right: -15,
                  }}
                  title={`${out.name} (${out.data_type})`}
                />
              </div>
            );
          })}
        </div>
      </div>

      {/* 3. Bottom Execution Status Ribbon */}
      {execResult && (
        <div className="px-3 py-1 bg-[#101013] border-t border-[#26262c] rounded-b-lg flex items-center justify-between text-[10px] font-mono">
          <div className="flex items-center gap-1">
            {execResult.status === 'SUCCESS' ? (
              <>
                <CheckCircle2 className="w-3 h-3 text-[#10b981]" />
                <span className="text-[#10b981] font-semibold">OK</span>
              </>
            ) : execResult.status === 'ERROR' ? (
              <>
                <AlertCircle className="w-3 h-3 text-[#ef4444]" />
                <span className="text-[#ef4444] font-semibold">ERROR</span>
              </>
            ) : (
              <span className="text-[#71717a]">SKIPPED</span>
            )}
          </div>

          <div className="flex items-center gap-1 text-[#71717a]">
            <Clock className="w-2.5 h-2.5" />
            <span>{execResult.time_ms.toFixed(1)} ms</span>
          </div>
        </div>
      )}
    </div>
  );
});
