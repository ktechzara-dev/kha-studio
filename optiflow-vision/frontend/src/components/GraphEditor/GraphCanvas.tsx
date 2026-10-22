import React, { useCallback, useMemo } from 'react';
import {
  ReactFlow,
  Background,
  Controls,
  MiniMap,
  BackgroundVariant,
  ConnectionLineType,
  Node,
  Edge,
  Panel,
} from '@xyflow/react';
import '@xyflow/react/dist/style.css';

import { useGraphStore } from '../../state/useGraphStore';
import { useI18n } from '../../i18n';
import { FilterNodeComponent } from './FilterNodeComponent';
import { WORKFLOW_PRESETS } from '../../state/sampleWorkflows';
import { 
  Play, Sparkles, LayoutGrid, RotateCcw, 
  CheckCircle, Zap, Cpu, ArrowRight, FolderPlus,
  Maximize2, Target, BarChart2, Check
} from 'lucide-react';

const nodeTypes = {
  filterNode: FilterNodeComponent,
};

export const GraphCanvas: React.FC = () => {
  const { t, lang } = useI18n();
  const {
    nodes,
    edges,
    onNodesChange,
    onEdgesChange,
    onConnect,
    runOnce,
    isRunning,
    lastExecutionTimeMs,
    loadPresetWorkflow,
    syncGraphToProject,
  } = useGraphStore();

  // Clear Graph
  const handleClearGraph = useCallback(() => {
    if (confirm(lang === 'vi' ? 'Bạn có chắc chắn muốn xóa toàn bộ đồ thị hiện tại?' : 'Clear entire graph?')) {
      useGraphStore.setState({ nodes: [], edges: [], selectedNodeId: null });
      syncGraphToProject();
    }
  }, [lang, syncGraphToProject]);

  // Auto Layout
  const handleAutoLayout = useCallback(() => {
    const updated = nodes.map((node, idx) => ({
      ...node,
      position: {
        x: 80 + (idx % 4) * 310,
        y: 120 + Math.floor(idx / 4) * 200,
      },
    }));
    useGraphStore.setState({ nodes: updated });
    syncGraphToProject();
  }, [nodes, syncGraphToProject]);

  return (
    <div className="w-full h-full relative bg-[#09090c] overflow-hidden">
      <ReactFlow
        nodes={nodes}
        edges={edges}
        onNodesChange={onNodesChange}
        onEdgesChange={onEdgesChange}
        onConnect={onConnect}
        nodeTypes={nodeTypes}
        connectionLineType={ConnectionLineType.SmoothStep}
        connectionLineStyle={{ stroke: '#00f0ff', strokeWidth: 2 }}
        fitView
        fitViewOptions={{ padding: 0.2 }}
        minZoom={0.1}
        maxZoom={2.5}
        defaultEdgeOptions={{
          animated: true,
          style: { stroke: '#00f0ff', strokeWidth: 2 },
        }}
        proOptions={{ hideAttribution: true }}
      >
        <Background
          variant={BackgroundVariant.Dots}
          gap={20}
          size={1.2}
          color="#22232c"
          className="bg-[#09090c]"
        />

        <Controls
          showInteractive={false}
          className="bg-[#121319]/90 border border-[#262734] rounded-lg p-1 text-[#f4f4f7] shadow-xl"
        />

        <MiniMap
          nodeStrokeColor="#00f0ff"
          nodeColor="#1a1b24"
          nodeBorderRadius={4}
          maskColor="rgba(9, 9, 12, 0.75)"
          className="bg-[#101117] border border-[#262734] rounded-lg overflow-hidden shadow-2xl"
          style={{ width: 140, height: 95 }}
        />

        {/* Top Floating Control Bar */}
        <Panel position="top-left" className="m-3">
          <div className="flex items-center gap-2 bg-[#121319]/90 backdrop-blur-md px-3 py-1.5 rounded-lg border border-[#252634] shadow-2xl">
            {/* Quick Run */}
            <button
              onClick={runOnce}
              disabled={isRunning}
              className={`p-1.5 rounded bg-[#00f0ff] hover:bg-[#00d0df] text-[#0a0b0e] font-bold transition-all flex items-center gap-1.5 shadow-[0_0_15px_rgba(0,240,255,0.3)] ${
                isRunning ? 'animate-pulse' : ''
              }`}
              title="Chạy 1 lần quy trình DAG (F5)"
            >
              <Play className="w-3.5 h-3.5 fill-current" />
              <span className="text-[11px] font-black uppercase">Run Once</span>
            </button>

            {/* Execution timing badge */}
            {lastExecutionTimeMs > 0 && (
              <div className="flex items-center gap-1 px-2 py-1 rounded bg-[#10b981]/15 border border-[#10b981]/30 text-[#10b981] font-mono text-[11px] font-bold">
                <Zap className="w-3 h-3" />
                <span>{lastExecutionTimeMs.toFixed(1)} ms</span>
              </div>
            )}

            <div className="h-4 w-px bg-[#26262c]" />

            {/* Auto Layout */}
            <button
              onClick={handleAutoLayout}
              disabled={nodes.length === 0}
              className="p-1.5 rounded hover:bg-[#1f1f24] text-[#a1a1aa] hover:text-[#00f0ff] transition-colors flex items-center gap-1.5 font-medium text-xs"
              title="Tự động căn chỉnh node theo luồng DAG"
            >
              <LayoutGrid className="w-3.5 h-3.5" />
              <span className="hidden sm:inline">Căn Chỉnh</span>
            </button>

            {/* Preset Pipelines Quick Select Dropdown */}
            <div className="flex items-center gap-1.5 bg-[#171822] border border-[#2b2c3a] px-2 py-1 rounded">
              <Sparkles className="w-3.5 h-3.5 text-[#00f0ff]" />
              <select
                onChange={(e) => {
                  if (e.target.value) {
                    loadPresetWorkflow(e.target.value);
                    e.target.value = '';
                  }
                }}
                defaultValue=""
                className="bg-transparent font-bold text-xs text-[#00f0ff] outline-none cursor-pointer max-w-[200px] truncate"
                title="Tải quy trình mẫu công nghiệp"
              >
                <option value="" disabled className="bg-[#181920]">-- Thư Viện Mẫu (6 Presets) --</option>
                {WORKFLOW_PRESETS.map((p) => (
                  <option key={p.id} value={p.id} className="bg-[#181920]">
                    {p.title}
                  </option>
                ))}
              </select>
            </div>

            <div className="h-4 w-px bg-[#26262c]" />

            {/* Clear Graph */}
            <button
              onClick={handleClearGraph}
              disabled={nodes.length === 0}
              className="p-1.5 rounded hover:bg-[#ef4444]/20 text-[#71717a] hover:text-[#ef4444] transition-colors"
              title="Xóa toàn bộ đồ thị hiện tại"
            >
              <RotateCcw className="w-3.5 h-3.5" />
            </button>

            {/* Graph Stats */}
            <span className="text-[10px] text-[#71717a] font-mono hidden md:inline">
              {nodes.length} Nodes • {edges.length} Links
            </span>
          </div>
        </Panel>

        {/* Empty Canvas Prompt with 6 Beautiful Preset Cards */}
        {nodes.length === 0 && (
          <div className="absolute inset-0 flex items-center justify-center p-6 pointer-events-none z-10 overflow-y-auto">
            <div className="hud-panel p-8 max-w-4xl w-full text-center pointer-events-auto border border-[#00f0ff]/30 shadow-[0_0_80px_rgba(0,0,0,0.9)] bg-[#0d0e14]/95 backdrop-blur-md rounded-xl my-auto">
              <div className="w-12 h-12 rounded-full bg-[#00f0ff]/10 border border-[#00f0ff]/40 flex items-center justify-center mx-auto mb-3 text-[#00f0ff]">
                <Cpu className="w-6 h-6" />
              </div>
              <h2 className="font-black text-lg text-white mb-1 tracking-wide">
                THƯ VIỆN QUY TRÌNH THỊ GIÁC CÔNG NGHIỆP CÓ SẴN
              </h2>
              <p className="text-xs text-[#a1a1aa] mb-6 max-w-xl mx-auto leading-relaxed">
                Chọn một mẫu quy trình được cấu hình sẵn chuẩn công nghiệp bên dưới để nạp đồ thị DAG và bắt đầu kiểm tra tức thì:
              </p>

              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3.5 text-left">
                {WORKFLOW_PRESETS.map((preset) => (
                  <div
                    key={preset.id}
                    onClick={() => loadPresetWorkflow(preset.id)}
                    className="p-3.5 rounded-lg bg-[#14151e] border border-[#252636] hover:border-[#00f0ff] hover:bg-[#1a1c28] cursor-pointer transition-all duration-200 group flex flex-col justify-between shadow-lg"
                  >
                    <div>
                      <div className="flex items-center justify-between mb-2">
                        <span 
                          className="text-[9px] font-bold px-2 py-0.5 rounded border font-mono tracking-wider"
                          style={{ borderColor: preset.badgeColor, color: preset.badgeColor, backgroundColor: `${preset.badgeColor}15` }}
                        >
                          {preset.badge}
                        </span>
                        <ArrowRight className="w-3.5 h-3.5 text-[#71717a] group-hover:text-[#00f0ff] group-hover:translate-x-1 transition-all" />
                      </div>
                      <h4 className="text-xs font-bold text-white group-hover:text-[#00f0ff] transition-colors mb-1">
                        {preset.title}
                      </h4>
                      <p className="text-[11px] text-[#8e8e9e] line-clamp-2 leading-relaxed">
                        {preset.subtitle}
                      </p>
                    </div>

                    <div className="mt-3 pt-2.5 border-t border-[#222330] flex items-center justify-between text-[10px] text-[#71717a] font-mono">
                      <span>{preset.nodes.length} khối xử lý</span>
                      <span className="text-[#00f0ff] font-semibold group-hover:underline">Nạp quy trình &rarr;</span>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          </div>
        )}
      </ReactFlow>
    </div>
  );
};
