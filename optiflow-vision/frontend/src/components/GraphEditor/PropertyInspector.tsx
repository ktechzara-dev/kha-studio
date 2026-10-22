import React, { useState, useEffect } from 'react';
import { useI18n } from '../../i18n';
import { useGraphStore } from '../../state/useGraphStore';
import { useViewerStore } from '../../state/useViewerStore';
import { 
  Sliders, Trash2, Eye, RotateCcw, 
  Info, CheckCircle, Code, Layers, Clock,
  Target, Crosshair, Award, Zap, Plus, Check, Sparkles, Box, Maximize2
} from 'lucide-react';

export const PropertyInspector: React.FC = () => {
  const { lang } = useI18n();
  const { 
    nodes, selectedNodeId, filterDefs, 
    updateNodeInput, deleteNode, nodeExecutionResults, runOnce 
  } = useGraphStore();

  const {
    userRoi, setUserRoi, cursorPos, activeTool, setActiveTool,
    pickingTarget, setPickingTarget, trainedTemplate, setTrainedTemplate
  } = useViewerStore();

  const [activeTab, setActiveTab] = useState<'params' | 'teaching' | 'outputs'>('params');
  const [newParamKey, setNewParamKey] = useState('');
  const [newParamVal, setNewParamVal] = useState('');
  const [showAddParam, setShowAddParam] = useState(false);

  const selectedNode = nodes.find((n) => n.id === selectedNodeId);

  // Synchronize interactive ROI / Point picking from image into selected node inputs
  useEffect(() => {
    if (!selectedNode) return;

    if (pickingTarget === 'roi' && userRoi.active && userRoi.width > 5 && userRoi.height > 5) {
      updateNodeInput(selectedNode.id, 'roi_x', userRoi.x);
      updateNodeInput(selectedNode.id, 'roi_y', userRoi.y);
      updateNodeInput(selectedNode.id, 'roi_width', userRoi.width);
      updateNodeInput(selectedNode.id, 'roi_height', userRoi.height);
    }

    if (pickingTarget === 'point' && cursorPos) {
      updateNodeInput(selectedNode.id, 'point_x', cursorPos.x);
      updateNodeInput(selectedNode.id, 'point_y', cursorPos.y);
    }
  }, [userRoi.x, userRoi.y, userRoi.width, userRoi.height, userRoi.active, cursorPos, pickingTarget, selectedNode?.id]);

  if (!selectedNode) {
    return (
      <div className="w-full h-full bg-[#131418] flex flex-col items-center justify-center p-6 text-center text-[#71717a] select-none">
        <Sliders className="w-8 h-8 stroke-1 text-[#3f3f46] mb-2" />
        <span className="font-semibold text-xs text-[#a1a1aa]">
          {lang === 'vi' ? 'Chưa chọn bộ lọc nào' : 'No Filter Selected'}
        </span>
        <span className="text-[11px] mt-1 max-w-[200px]">
          {lang === 'vi' ? 'Bấm vào một khối node trên đồ thị để tinh chỉnh tham số & dạy mẫu.' : 'Click on any node in the graph canvas to inspect and configure.'}
        </span>
      </div>
    );
  }

  const filterType = selectedNode.data.filterType as string;
  const def = filterDefs[filterType];
  const nodeInputs = (selectedNode.data.inputs as Record<string, any>) || {};
  const execResult = nodeExecutionResults[selectedNode.id];

  // Quick action: Start ROI picking on image
  const handleStartRoiPicking = () => {
    setActiveTool('roi');
    setPickingTarget('roi');
  };

  // Quick action: Start Point picking on image
  const handleStartPointPicking = () => {
    setActiveTool('probe');
    setPickingTarget('point');
  };

  // Quick action: Teach Model
  const handleTeachTemplate = () => {
    const rx = nodeInputs.roi_x || userRoi.x || 340;
    const ry = nodeInputs.roi_y || userRoi.y || 340;
    const rw = nodeInputs.roi_width || userRoi.width || 240;
    const rh = nodeInputs.roi_height || userRoi.height || 520;

    const tmpl = {
      isTrained: true,
      name: `${selectedNode.data.label}_Template`,
      roi: { x: rx, y: ry, width: rw, height: rh },
      minScore: nodeInputs.min_score || 0.85,
      angleRange: nodeInputs.angle_range || 15,
      posToleranceMm: nodeInputs.pos_tolerance || 5.0,
      trainedAt: new Date().toLocaleTimeString(),
    };
    setTrainedTemplate(tmpl);
    updateNodeInput(selectedNode.id, 'is_trained', true);
    updateNodeInput(selectedNode.id, 'template_name', tmpl.name);
    setPickingTarget(null);
  };

  // Quick action: Add custom parameter
  const handleAddCustomParam = () => {
    if (!newParamKey.trim()) return;
    let parsedVal: any = newParamVal;
    if (parsedVal === 'true') parsedVal = true;
    else if (parsedVal === 'false') parsedVal = false;
    else if (!isNaN(Number(parsedVal)) && parsedVal !== '') parsedVal = Number(parsedVal);

    updateNodeInput(selectedNode.id, newParamKey.trim(), parsedVal);
    setNewParamKey('');
    setNewParamVal('');
    setShowAddParam(false);
  };

  return (
    <div className="w-full h-full bg-[#131418] flex flex-col select-none z-10 text-xs">
      {/* 1. Node Header */}
      <div className="p-3 border-b border-[#26262c] bg-[#18181b] flex flex-col gap-2">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-1.5">
            <span className="text-[10px] font-bold uppercase tracking-wider text-[#00f0ff] bg-[#00f0ff]/10 px-2 py-0.5 rounded border border-[#00f0ff]/30">
              {selectedNode.data.category as string}
            </span>
            <span className="font-mono text-[9.5px] text-[#71717a]">
              ID: {selectedNode.id}
            </span>
          </div>
          <div className="flex items-center gap-1">
            <button
              onClick={() => runOnce()}
              className="p-1 rounded text-[#10b981] hover:bg-[#10b981]/20 transition-colors"
              title="Chạy thử ngay khối này (Test Run Node)"
            >
              <Zap className="w-3.5 h-3.5" />
            </button>
            <button
              onClick={() => deleteNode(selectedNode.id)}
              className="p-1 rounded text-[#ef4444] hover:bg-[#ef4444]/20 transition-colors"
              title="Xóa bộ lọc (Delete Node)"
            >
              <Trash2 className="w-3.5 h-3.5" />
            </button>
          </div>
        </div>

        {/* Node Label Input */}
        <div>
          <label className="text-[10px] text-[#71717a] font-semibold block mb-1">
            TÊN BỘ LỌC (LABEL)
          </label>
          <input
            type="text"
            value={selectedNode.data.label as string}
            onChange={(e) => {
              useGraphStore.setState((state) => ({
                nodes: state.nodes.map((n) =>
                  n.id === selectedNode.id
                    ? { ...n, data: { ...n.data, label: e.target.value } }
                    : n
                ),
              }));
            }}
            className="w-full bg-[#101013] border border-[#26262c] focus:border-[#00f0ff] rounded px-2.5 py-1 text-xs text-white font-bold outline-none"
          />
        </div>

        <p className="text-[10.5px] text-[#a1a1aa] leading-relaxed line-clamp-2">
          {def?.description || 'Khối xử lý thị giác máy tính & đo kiểm công nghiệp.'}
        </p>

        {/* Tab Switcher: Params / Teaching / Outputs */}
        <div className="grid grid-cols-3 gap-1 bg-[#101013] p-0.5 rounded border border-[#26262c] mt-1 font-semibold text-[10.5px]">
          <button
            onClick={() => setActiveTab('params')}
            className={`py-1 rounded transition-all text-center ${
              activeTab === 'params'
                ? 'bg-[#26262c] text-[#00f0ff] shadow-sm font-bold'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            Tham Số
          </button>
          <button
            onClick={() => setActiveTab('teaching')}
            className={`py-1 rounded transition-all text-center flex items-center justify-center gap-1 ${
              activeTab === 'teaching'
                ? 'bg-[#26262c] text-[#f59e0b] shadow-sm font-bold'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <Award className="w-3 h-3 text-[#f59e0b]" />
            <span>Teaching</span>
          </button>
          <button
            onClick={() => setActiveTab('outputs')}
            className={`py-1 rounded transition-all text-center flex items-center justify-center gap-1 ${
              activeTab === 'outputs'
                ? 'bg-[#26262c] text-[#10b981] shadow-sm font-bold'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <span>Kết Quả</span>
            {execResult && (
              <span className="w-1.5 h-1.5 rounded-full bg-[#10b981]" />
            )}
          </button>
        </div>
      </div>

      {/* 2. Content Area */}
      <div className="flex-1 overflow-y-auto p-3 space-y-3">
        {/* TAB 1: PARAMETERS */}
        {activeTab === 'params' && (
          <div className="space-y-3">
            {/* Quick Interactive Image Picking Bar */}
            <div className="p-2.5 rounded bg-[#101014] border border-[#26262c] space-y-2">
              <span className="text-[10px] text-[#a1a1aa] font-bold uppercase tracking-wider block">
                Thao Tác Trực Tiếp Trên Ảnh (Live Tools)
              </span>
              <div className="grid grid-cols-2 gap-1.5">
                <button
                  onClick={handleStartRoiPicking}
                  className={`px-2 py-1.5 rounded text-[11px] font-semibold flex items-center justify-center gap-1.5 border transition-all ${
                    pickingTarget === 'roi'
                      ? 'bg-[#f59e0b] text-black border-[#f59e0b] shadow-[0_0_12px_rgba(245,158,11,0.5)] animate-pulse'
                      : 'bg-[#18181f] text-[#f59e0b] border-[#f59e0b]/40 hover:bg-[#252530]'
                  }`}
                  title="Nhấp kéo vẽ vùng ROI trực tiếp trên cửa sổ ảnh"
                >
                  <Target className="w-3.5 h-3.5" />
                  <span>{pickingTarget === 'roi' ? 'Đang Vẽ ROI...' : 'Chọn Vùng ROI'}</span>
                </button>

                <button
                  onClick={handleStartPointPicking}
                  className={`px-2 py-1.5 rounded text-[11px] font-semibold flex items-center justify-center gap-1.5 border transition-all ${
                    pickingTarget === 'point'
                      ? 'bg-[#00f0ff] text-black border-[#00f0ff] shadow-[0_0_12px_rgba(0,240,255,0.5)] animate-pulse'
                      : 'bg-[#18181f] text-[#00f0ff] border-[#00f0ff]/40 hover:bg-[#252530]'
                  }`}
                  title="Nhấp chuột chọn điểm tọa độ trực tiếp trên cửa sổ ảnh"
                >
                  <Crosshair className="w-3.5 h-3.5" />
                  <span>{pickingTarget === 'point' ? 'Nhấp Điểm...' : 'Chọn Điểm'}</span>
                </button>
              </div>

              {/* Bound ROI Badge */}
              {nodeInputs.roi_width && (
                <div className="p-1.5 rounded bg-[#181820] border border-[#2a2b38] flex items-center justify-between text-[10px] font-mono text-[#f59e0b]">
                  <span>Vùng ROI: ({nodeInputs.roi_x}, {nodeInputs.roi_y}, {nodeInputs.roi_width}x{nodeInputs.roi_height})</span>
                  <Check className="w-3 h-3 text-[#10b981]" />
                </div>
              )}

              {/* Bound Point Badge */}
              {nodeInputs.point_x !== undefined && (
                <div className="p-1.5 rounded bg-[#181820] border border-[#2a2b38] flex items-center justify-between text-[10px] font-mono text-[#00f0ff]">
                  <span>Điểm: ({nodeInputs.point_x}, {nodeInputs.point_y}) px</span>
                  <Check className="w-3 h-3 text-[#10b981]" />
                </div>
              )}
            </div>

            {/* Standard Defined Inputs */}
            {def?.inputs.map((inp) => {
              const val = nodeInputs[inp.name] !== undefined ? nodeInputs[inp.name] : inp.default_value;

              // Image and Array port handles
              if (inp.data_type === 'Image' || (inp.data_type === 'Array' && !inp.options)) {
                return (
                  <div key={inp.name} className="p-2 rounded bg-[#101013] border border-[#26262c]">
                    <div className="flex items-center justify-between text-[11px]">
                      <span className="font-semibold text-[#e4e4e7]">{inp.name}</span>
                      <span className="text-[9.5px] text-[#00f0ff] font-mono px-1.5 py-0.2 rounded bg-[#00f0ff]/10">
                        {inp.data_type} Port
                      </span>
                    </div>
                    <span className="text-[10px] text-[#71717a] mt-0.5 block">
                      {inp.description || 'Kết nối qua chân cắm (Handle) trên đồ thị.'}
                    </span>
                  </div>
                );
              }

              return (
                <div key={inp.name} className="p-2.5 rounded bg-[#101013] border border-[#26262c] space-y-1.5">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-[#e4e4e7] text-[11px]">{inp.name}</span>
                    <span className="text-[9.5px] text-[#71717a] font-mono">{inp.data_type}</span>
                  </div>

                  {/* Dropdown Options */}
                  {inp.options ? (
                    <select
                      value={val || inp.options[0]}
                      onChange={(e) => updateNodeInput(selectedNode.id, inp.name, e.target.value)}
                      className="w-full bg-[#18181b] border border-[#26262c] rounded px-2 py-1 text-white text-xs outline-none focus:border-[#00f0ff]"
                    >
                      {inp.options.map((opt) => (
                        <option key={opt} value={opt}>
                          {opt}
                        </option>
                      ))}
                    </select>
                  ) : inp.data_type === 'Boolean' ? (
                    /* Checkbox */
                    <label className="flex items-center gap-2 cursor-pointer mt-1">
                      <input
                        type="checkbox"
                        checked={Boolean(val)}
                        onChange={(e) => updateNodeInput(selectedNode.id, inp.name, e.target.checked)}
                        className="rounded border-[#26262c] text-[#00f0ff] focus:ring-0 bg-[#18181b] w-4 h-4 cursor-pointer"
                      />
                      <span className="text-[11px] text-[#a1a1aa] font-medium">Kích hoạt (Enable)</span>
                    </label>
                  ) : inp.data_type === 'Integer' || inp.data_type === 'Number' ? (
                    /* Slider + Number input */
                    <div className="space-y-1">
                      <div className="flex items-center gap-2">
                        <input
                          type="range"
                          min={inp.min_value !== undefined ? inp.min_value : 0}
                          max={inp.max_value !== undefined ? inp.max_value : 255}
                          step={inp.data_type === 'Integer' ? 1 : 0.1}
                          value={val !== undefined ? val : 0}
                          onChange={(e) =>
                            updateNodeInput(
                              selectedNode.id,
                              inp.name,
                              inp.data_type === 'Integer'
                                ? parseInt(e.target.value, 10)
                                : parseFloat(e.target.value)
                            )
                          }
                          className="flex-1 accent-[#00f0ff] cursor-pointer"
                        />
                        <input
                          type="number"
                          value={val !== undefined ? val : 0}
                          onChange={(e) =>
                            updateNodeInput(
                              selectedNode.id,
                              inp.name,
                              inp.data_type === 'Integer'
                                ? parseInt(e.target.value, 10)
                                : parseFloat(e.target.value)
                            )
                          }
                          className="w-16 bg-[#18181b] border border-[#26262c] rounded px-1.5 py-0.5 text-center font-mono text-white text-[11px]"
                        />
                      </div>
                    </div>
                  ) : (
                    /* Text Input */
                    <input
                      type="text"
                      value={val || ''}
                      onChange={(e) => updateNodeInput(selectedNode.id, inp.name, e.target.value)}
                      className="w-full bg-[#18181b] border border-[#26262c] rounded px-2 py-1 text-white text-xs outline-none focus:border-[#00f0ff] font-mono"
                    />
                  )}

                  {inp.description && (
                    <span className="text-[10px] text-[#71717a] block">
                      {inp.description}
                    </span>
                  )}
                </div>
              );
            })}

            {/* Custom Extra Parameters (ROI, Min Area, Max Area, Thresh...) */}
            {Object.entries(nodeInputs).map(([k, v]) => {
              if (def?.inputs.some((inp) => inp.name === k)) return null; // already rendered
              return (
                <div key={k} className="p-2 rounded bg-[#101013] border border-[#26262c] space-y-1">
                  <div className="flex items-center justify-between">
                    <span className="font-bold text-[#f59e0b] text-[11px] font-mono">{k}</span>
                    <button
                      onClick={() => {
                        const copy = { ...nodeInputs };
                        delete copy[k];
                        useGraphStore.setState((s) => ({
                          nodes: s.nodes.map((n) => n.id === selectedNode.id ? { ...n, data: { ...n.data, inputs: copy } } : n)
                        }));
                      }}
                      className="text-[#ef4444] hover:text-white text-[10px]"
                      title="Xóa tham số"
                    >
                      Xóa
                    </button>
                  </div>
                  <input
                    type="text"
                    value={String(v)}
                    onChange={(e) => updateNodeInput(selectedNode.id, k, e.target.value)}
                    className="w-full bg-[#18181b] border border-[#26262c] rounded px-2 py-1 text-white text-xs outline-none focus:border-[#00f0ff] font-mono"
                  />
                </div>
              );
            })}

            {/* Add Custom Parameter Button */}
            {showAddParam ? (
              <div className="p-2.5 rounded bg-[#181922] border border-[#00f0ff]/40 space-y-2">
                <span className="text-[10.5px] font-bold text-[#00f0ff]">Thêm Tham Số Tùy Chỉnh (Custom Field):</span>
                <input
                  type="text"
                  placeholder="Tên tham số (VD: min_area, tolerance)"
                  value={newParamKey}
                  onChange={(e) => setNewParamKey(e.target.value)}
                  className="w-full bg-[#101014] border border-[#2b2c3a] rounded px-2 py-1 text-xs text-white font-mono outline-none"
                />
                <input
                  type="text"
                  placeholder="Giá trị (VD: 1500, true, 0.85)"
                  value={newParamVal}
                  onChange={(e) => setNewParamVal(e.target.value)}
                  className="w-full bg-[#101014] border border-[#2b2c3a] rounded px-2 py-1 text-xs text-white font-mono outline-none"
                />
                <div className="flex items-center gap-1.5 justify-end">
                  <button
                    onClick={() => setShowAddParam(false)}
                    className="px-2 py-1 rounded bg-[#252630] text-[#a1a1aa] hover:text-white text-[10.5px]"
                  >
                    Hủy
                  </button>
                  <button
                    onClick={handleAddCustomParam}
                    className="px-2.5 py-1 rounded bg-[#00f0ff] text-black font-bold text-[10.5px]"
                  >
                    Lưu Tham Số
                  </button>
                </div>
              </div>
            ) : (
              <button
                onClick={() => setShowAddParam(true)}
                className="w-full py-1.5 rounded border border-dashed border-[#2b2c3a] hover:border-[#00f0ff] text-[#71717a] hover:text-[#00f0ff] text-[11px] font-semibold flex items-center justify-center gap-1 transition-all"
              >
                <Plus className="w-3.5 h-3.5" />
                <span>Thêm Tham Số Tùy Chỉnh</span>
              </button>
            )}
          </div>
        )}

        {/* TAB 2: TEACHING & PATTERN TRAINING */}
        {activeTab === 'teaching' && (
          <div className="space-y-3">
            {/* Golden Template Card */}
            <div className="p-3 rounded-lg bg-[#14151e] border border-[#f59e0b]/40 space-y-2.5">
              <div className="flex items-center justify-between">
                <div className="flex items-center gap-1.5">
                  <Award className="w-4 h-4 text-[#f59e0b]" />
                  <span className="font-bold text-[#e4e4e7] text-[11.5px]">Huấn Luyện Mẫu Vàng (Teaching)</span>
                </div>
                <span className="px-1.5 py-0.5 rounded text-[9px] font-mono font-bold bg-[#10b981]/20 text-[#10b981] border border-[#10b981]/40">
                  {trainedTemplate?.isTrained ? 'ĐÃ HỌC MẪU' : 'CHƯA HỌC'}
                </span>
              </div>

              <p className="text-[10px] text-[#a1a1aa] leading-relaxed">
                Định vị mẫu phôi chuẩn (Golden Master Pattern) để so sánh khớp mẫu xoay 360°, phát hiện dị vật, khuyết tật và đo sai lệch vị trí.
              </p>

              {/* Teaching Action Buttons */}
              <div className="grid grid-cols-2 gap-2 pt-1">
                <button
                  onClick={handleStartRoiPicking}
                  className="px-2.5 py-2 rounded bg-[#1c1d27] hover:bg-[#252636] border border-[#f59e0b]/50 text-[#f59e0b] font-bold text-[11px] flex items-center justify-center gap-1.5 transition-all"
                >
                  <Target className="w-3.5 h-3.5" />
                  <span>Vẽ Khung Mẫu</span>
                </button>

                <button
                  onClick={handleTeachTemplate}
                  className="px-2.5 py-2 rounded bg-[#f59e0b] hover:bg-[#d97706] text-black font-extrabold text-[11px] flex items-center justify-center gap-1.5 transition-all shadow-[0_0_15px_rgba(245,158,11,0.4)]"
                >
                  <Award className="w-3.5 h-3.5" />
                  <span>Học Mẫu Ngay</span>
                </button>
              </div>

              {/* Trained Info Display */}
              {trainedTemplate && (
                <div className="p-2 rounded bg-[#0d0e14] border border-[#262734] space-y-1.5 text-[10.5px] font-mono text-[#a1a1aa]">
                  <div className="flex justify-between">
                    <span className="text-[#71717a]">Tên Mẫu:</span>
                    <span className="text-white font-bold">{trainedTemplate.name}</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-[#71717a]">Kích Thước Mẫu:</span>
                    <span className="text-[#00f0ff]">{trainedTemplate.roi.width} x {trainedTemplate.roi.height} px</span>
                  </div>
                  <div className="flex justify-between">
                    <span className="text-[#71717a]">Thời Gian Học:</span>
                    <span className="text-[#10b981]">{trainedTemplate.trainedAt}</span>
                  </div>
                </div>
              )}

              {/* Nominal Match Parameters */}
              <div className="space-y-2 pt-1">
                <div>
                  <div className="flex justify-between text-[10.5px]">
                    <span className="text-[#e4e4e7] font-semibold">Độ Tương Đồng Tối Thiểu (Min Score):</span>
                    <span className="text-[#f59e0b] font-mono font-bold">
                      {nodeInputs.min_score !== undefined ? nodeInputs.min_score : 0.85}
                    </span>
                  </div>
                  <input
                    type="range"
                    min="0.5"
                    max="0.99"
                    step="0.01"
                    value={nodeInputs.min_score !== undefined ? nodeInputs.min_score : 0.85}
                    onChange={(e) => updateNodeInput(selectedNode.id, 'min_score', parseFloat(e.target.value))}
                    className="w-full accent-[#f59e0b] cursor-pointer"
                  />
                </div>

                <div>
                  <div className="flex justify-between text-[10.5px]">
                    <span className="text-[#e4e4e7] font-semibold">Dung Sai Góc Xoay (Angle Range):</span>
                    <span className="text-[#00f0ff] font-mono font-bold">
                      ±{nodeInputs.angle_range !== undefined ? nodeInputs.angle_range : 15}°
                    </span>
                  </div>
                  <input
                    type="range"
                    min="0"
                    max="180"
                    step="1"
                    value={nodeInputs.angle_range !== undefined ? nodeInputs.angle_range : 15}
                    onChange={(e) => updateNodeInput(selectedNode.id, 'angle_range', parseInt(e.target.value, 10))}
                    className="w-full accent-[#00f0ff] cursor-pointer"
                  />
                </div>

                <div>
                  <div className="flex justify-between text-[10.5px]">
                    <span className="text-[#e4e4e7] font-semibold">Dung Sai Vị Trí Cho Phép (Tolerance):</span>
                    <span className="text-[#10b981] font-mono font-bold">
                      ±{nodeInputs.pos_tolerance !== undefined ? nodeInputs.pos_tolerance : 5.0} mm
                    </span>
                  </div>
                  <input
                    type="range"
                    min="0.5"
                    max="50"
                    step="0.5"
                    value={nodeInputs.pos_tolerance !== undefined ? nodeInputs.pos_tolerance : 5.0}
                    onChange={(e) => updateNodeInput(selectedNode.id, 'pos_tolerance', parseFloat(e.target.value))}
                    className="w-full accent-[#10b981] cursor-pointer"
                  />
                </div>
              </div>
            </div>
          </div>
        )}

        {/* TAB 3: OUTPUTS */}
        {activeTab === 'outputs' && (
          <div className="space-y-2">
            {execResult ? (
              <>
                <div className="p-2.5 rounded bg-[#101013] border border-[#26262c] flex items-center justify-between text-[11px] font-mono">
                  <span className="text-[#a1a1aa]">Thời gian thực thi:</span>
                  <span className="text-[#10b981] font-bold">{execResult.time_ms.toFixed(2)} ms</span>
                </div>

                {Object.entries(execResult.outputs).map(([k, v]) => (
                  <div key={k} className="p-2.5 rounded bg-[#101013] border border-[#26262c] space-y-1">
                    <span className="font-bold text-[#00f0ff] text-[11px] block">{k}</span>
                    <pre className="bg-[#18181b] p-2 rounded text-[10.5px] font-mono text-[#f4f4f7] overflow-x-auto whitespace-pre-wrap max-h-36">
                      {typeof v === 'object' ? JSON.stringify(v, null, 2) : String(v)}
                    </pre>
                  </div>
                ))}
              </>
            ) : (
              <div className="text-center p-6 text-[#71717a]">
                <Clock className="w-6 h-6 mx-auto mb-2 text-[#3f3f46]" />
                <span className="text-xs">Chưa có kết quả thực thi.</span>
                <p className="text-[10px] mt-1">Bấm nút "Chạy Thử Khối (⚡)" hoặc F5 để tính toán kết quả.</p>
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
