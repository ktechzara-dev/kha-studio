import React, { useRef, useState, useEffect, useCallback } from 'react';
import { useI18n } from '../../i18n';
import { useSidecarStore } from '../../state/useSidecarStore';
import { useViewerStore, ViewerTool } from '../../state/useViewerStore';
import { useGraphStore } from '../../state/useGraphStore';
import { HistogramOverlay } from './HistogramOverlay';
import { PixelProbeOverlay } from './PixelProbeOverlay';
import { 
  Hand, Ruler, Box, Target, Search, Image as ImageIcon, 
  ZoomIn, ZoomOut, Maximize2, Layers, BarChart2, Download, 
  Trash2, Crosshair, CheckCircle, AlertTriangle
} from 'lucide-react';

export const ImageViewer: React.FC = () => {
  const { t } = useI18n();
  const { baseUrl } = useSidecarStore();
  const { selectedNodeId, nodes, updateNodeInput } = useGraphStore();
  const {
    activeImageId,
    setActiveImageId,
    activeImageMeta,
    scale,
    setScale,
    offset,
    setOffset,
    cursorPos,
    setCursorPos,
    probePixel,
    showOverlays,
    setShowOverlays,
    showHistogram,
    setShowHistogram,
    showProbe,
    setShowProbe,
    pipelineOverlays,
    fitToView,
    activeTool,
    setActiveTool,
    measurement,
    setMeasurement,
    userRoi,
    setUserRoi,
    caliperRay,
    setCaliperRay,
    clearInteractiveTools,
    pickingTarget,
    setPickingTarget,
    trainedTemplate,
    setTrainedTemplate,
  } = useViewerStore();

  const containerRef = useRef<HTMLDivElement>(null);
  const imgRef = useRef<HTMLImageElement>(null);

  const [isInteracting, setIsInteracting] = useState(false);
  const [panDragStart, setPanDragStart] = useState({ x: 0, y: 0 });
  const [toolStartPt, setToolStartPt] = useState<{ x: number; y: number } | null>(null);

  // Initial fit to view on load
  useEffect(() => {
    if (containerRef.current && activeImageMeta) {
      const rect = containerRef.current.getBoundingClientRect();
      fitToView(rect.width, rect.height);
    }
  }, [activeImageId, activeImageMeta?.width, activeImageMeta?.height]);

  // Handle Wheel Zoom Centered on Cursor
  const handleWheel = (e: React.WheelEvent) => {
    const zoomFactor = e.deltaY < 0 ? 1.15 : 0.87;
    const newScale = Math.max(0.05, Math.min(25.0, scale * zoomFactor));

    if (containerRef.current) {
      const rect = containerRef.current.getBoundingClientRect();
      const mouseX = e.clientX - rect.left;
      const mouseY = e.clientY - rect.top;

      // Adjust offset so zoom centers on cursor
      const newOffsetX = mouseX - (mouseX - offset.x) * (newScale / scale);
      const newOffsetY = mouseY - (mouseY - offset.y) * (newScale / scale);

      setScale(newScale);
      setOffset({ x: newOffsetX, y: newOffsetY });
    }
  };

  // Convert client mouse position to image coordinates
  const getImageCoordinates = (clientX: number, clientY: number): { x: number; y: number } | null => {
    if (!imgRef.current || !activeImageMeta) return null;
    const imgRect = imgRef.current.getBoundingClientRect();
    if (
      clientX >= imgRect.left &&
      clientX <= imgRect.right &&
      clientY >= imgRect.top &&
      clientY <= imgRect.bottom
    ) {
      const normX = (clientX - imgRect.left) / imgRect.width;
      const normY = (clientY - imgRect.top) / imgRect.height;
      return {
        x: Math.round(normX * activeImageMeta.width),
        y: Math.round(normY * activeImageMeta.height),
      };
    }
    return null;
  };

  // Mouse Interactions for Panning & Active Tools
  const handleMouseDown = (e: React.MouseEvent) => {
    const imgCoords = getImageCoordinates(e.clientX, e.clientY);

    // If in point picking mode: clicking assigns point/center coordinates to selected node
    if (pickingTarget === 'point' && imgCoords && selectedNodeId) {
      updateNodeInput(selectedNodeId, 'point_x', imgCoords.x);
      updateNodeInput(selectedNodeId, 'point_y', imgCoords.y);
      updateNodeInput(selectedNodeId, 'center_x', imgCoords.x);
      updateNodeInput(selectedNodeId, 'center_y', imgCoords.y);
      updateNodeInput(selectedNodeId, 'x', imgCoords.x);
      updateNodeInput(selectedNodeId, 'y', imgCoords.y);
      setPickingTarget(null);
      setActiveTool('pan');
      return;
    }

    // Pan Tool or Middle Mouse Button
    if (activeTool === 'pan' || e.button === 1 || e.button === 2) {
      setIsInteracting(true);
      setPanDragStart({ x: e.clientX - offset.x, y: e.clientY - offset.y });
      return;
    }

    if (imgCoords && e.button === 0) {
      setIsInteracting(true);
      setToolStartPt(imgCoords);

      if (activeTool === 'measure') {
        setMeasurement({
          p1: imgCoords,
          p2: imgCoords,
          distPx: 0,
          distMm: 0,
          deltaX: 0,
          deltaY: 0,
          angleDeg: 0,
        });
      } else if (activeTool === 'roi') {
        setUserRoi({
          x: imgCoords.x,
          y: imgCoords.y,
          width: 0,
          height: 0,
          active: true,
        });
      } else if (activeTool === 'caliper') {
        setCaliperRay({
          p1: imgCoords,
          p2: imgCoords,
          edgePos: imgCoords,
          contrast: 85,
        });
      } else if (activeTool === 'probe') {
        probePixel(imgCoords.x, imgCoords.y);
      }
    }
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    const imgCoords = getImageCoordinates(e.clientX, e.clientY);

    if (imgCoords) {
      setCursorPos(imgCoords);
      if (activeTool === 'probe' && Math.random() < 0.3) {
        probePixel(imgCoords.x, imgCoords.y);
      }
    } else {
      setCursorPos(null);
    }

    if (!isInteracting) return;

    // Panning
    if (activeTool === 'pan' || e.buttons === 4 || e.buttons === 2) {
      setOffset({ x: e.clientX - panDragStart.x, y: e.clientY - panDragStart.y });
      return;
    }

    // Active Interactive Tools
    if (imgCoords && toolStartPt) {
      if (activeTool === 'measure') {
        const dx = imgCoords.x - toolStartPt.x;
        const dy = imgCoords.y - toolStartPt.y;
        const distPx = Math.sqrt(dx * dx + dy * dy);
        const distMm = +(distPx * 0.05).toFixed(2);
        const angleDeg = +(Math.atan2(dy, dx) * (180 / Math.PI)).toFixed(1);

        setMeasurement({
          p1: toolStartPt,
          p2: imgCoords,
          distPx: +distPx.toFixed(1),
          distMm,
          deltaX: dx,
          deltaY: dy,
          angleDeg,
        });
      } else if (activeTool === 'roi') {
        const x = Math.min(toolStartPt.x, imgCoords.x);
        const y = Math.min(toolStartPt.y, imgCoords.y);
        const width = Math.abs(imgCoords.x - toolStartPt.x);
        const height = Math.abs(imgCoords.y - toolStartPt.y);

        setUserRoi({ x, y, width, height, active: true });
      } else if (activeTool === 'caliper') {
        const midX = Math.round((toolStartPt.x + imgCoords.x) / 2);
        const midY = Math.round((toolStartPt.y + imgCoords.y) / 2);

        setCaliperRay({
          p1: toolStartPt,
          p2: imgCoords,
          edgePos: { x: midX, y: midY },
          contrast: Math.min(100, Math.max(30, Math.round(Math.hypot(imgCoords.x - toolStartPt.x, imgCoords.y - toolStartPt.y) / 2))),
        });
      }
    }
  };

  const handleMouseUp = () => {
    if (isInteracting && toolStartPt) {
      // If user was drawing ROI for a node or template
      if ((pickingTarget === 'roi' || activeTool === 'roi') && selectedNodeId && userRoi.width > 5 && userRoi.height > 5) {
        updateNodeInput(selectedNodeId, 'roi_x', userRoi.x);
        updateNodeInput(selectedNodeId, 'roi_y', userRoi.y);
        updateNodeInput(selectedNodeId, 'roi_width', userRoi.width);
        updateNodeInput(selectedNodeId, 'roi_height', userRoi.height);
        updateNodeInput(selectedNodeId, 'x', userRoi.x);
        updateNodeInput(selectedNodeId, 'y', userRoi.y);
        updateNodeInput(selectedNodeId, 'width', userRoi.width);
        updateNodeInput(selectedNodeId, 'height', userRoi.height);
        if (pickingTarget === 'roi') {
          setPickingTarget(null);
          setActiveTool('pan');
        }
      }

      if (pickingTarget === 'template' && selectedNodeId && userRoi.width > 5 && userRoi.height > 5) {
        const selNode = nodes.find((n) => n.id === selectedNodeId);
        const tmpl = {
          isTrained: true,
          name: `${selNode?.data?.label || 'Target'}_Template`,
          roi: { x: userRoi.x, y: userRoi.y, width: userRoi.width, height: userRoi.height },
          minScore: 0.85,
          angleRange: 15,
          posToleranceMm: 5.0,
          trainedAt: new Date().toLocaleTimeString(),
        };
        setTrainedTemplate(tmpl);
        updateNodeInput(selectedNodeId, 'is_trained', true);
        updateNodeInput(selectedNodeId, 'template_name', tmpl.name);
        updateNodeInput(selectedNodeId, 'roi_x', userRoi.x);
        updateNodeInput(selectedNodeId, 'roi_y', userRoi.y);
        updateNodeInput(selectedNodeId, 'roi_width', userRoi.width);
        updateNodeInput(selectedNodeId, 'roi_height', userRoi.height);
        setPickingTarget(null);
        setActiveTool('pan');
      }
    }

    setIsInteracting(false);
    setToolStartPt(null);
  };

  const getCursorStyle = () => {
    if (pickingTarget === 'point') return 'cursor-crosshair';
    if (pickingTarget === 'roi' || pickingTarget === 'template') return 'cursor-cell';
    if (activeTool === 'pan') {
      return isInteracting ? 'cursor-grabbing' : 'cursor-grab';
    }
    if (activeTool === 'measure' || activeTool === 'caliper') {
      return 'cursor-crosshair';
    }
    if (activeTool === 'roi') {
      return 'cursor-cell';
    }
    return 'cursor-crosshair';
  };

  const imageUrl = `${baseUrl}/api/image/preview/${activeImageId}?max_dim=2048&quality=92`;

  return (
    <div
      ref={containerRef}
      onWheel={handleWheel}
      onMouseDown={handleMouseDown}
      onMouseMove={handleMouseMove}
      onMouseUp={handleMouseUp}
      onMouseLeave={() => { setIsInteracting(false); setCursorPos(null); }}
      className={`relative flex-1 w-full h-full bg-[#08080b] bg-grid-pattern overflow-hidden select-none ${getCursorStyle()}`}
    >
      {/* 1. Floating Top Interactive Vision Toolbar */}
      <div className="absolute top-3 left-1/2 -translate-x-1/2 bg-[#121319]/95 backdrop-blur-md border border-[#2b2c3a] shadow-[0_8px_32px_rgba(0,0,0,0.7)] px-2.5 py-1.5 rounded-lg flex items-center gap-1.5 z-40 text-xs select-none">
        {/* Tool: Pan */}
        <button
          onClick={() => setActiveTool('pan')}
          className={`px-2 py-1 rounded flex items-center gap-1 font-semibold transition-all ${
            activeTool === 'pan' ? 'bg-[#00f0ff] text-black shadow-[0_0_10px_#00f0ff]' : 'text-[#a1a1aa] hover:text-white hover:bg-[#20222e]'
          }`}
          title="Công cụ Di Chuyển (Pan / Hand Tool)"
        >
          <Hand className="w-3.5 h-3.5" />
          <span className="hidden md:inline text-[11px]">Di Chuyển</span>
        </button>

        {/* Tool: Measure 2-Point */}
        <button
          onClick={() => setActiveTool('measure')}
          className={`px-2 py-1 rounded flex items-center gap-1 font-semibold transition-all ${
            activeTool === 'measure' ? 'bg-[#10b981] text-black shadow-[0_0_10px_#10b981]' : 'text-[#a1a1aa] hover:text-white hover:bg-[#20222e]'
          }`}
          title="Công cụ Thước Đo 2 Điểm (Measure Distance & Angle)"
        >
          <Ruler className="w-3.5 h-3.5" />
          <span className="hidden md:inline text-[11px]">Thước Đo</span>
        </button>

        {/* Tool: ROI Box */}
        <button
          onClick={() => setActiveTool('roi')}
          className={`px-2 py-1 rounded flex items-center gap-1 font-semibold transition-all ${
            activeTool === 'roi' ? 'bg-[#f59e0b] text-black shadow-[0_0_10px_#f59e0b]' : 'text-[#a1a1aa] hover:text-white hover:bg-[#20222e]'
          }`}
          title="Công cụ Vẽ Vùng ROI (Region of Interest)"
        >
          <Box className="w-3.5 h-3.5" />
          <span className="hidden md:inline text-[11px]">Vùng ROI</span>
        </button>

        {/* Tool: 1D Caliper Ray */}
        <button
          onClick={() => setActiveTool('caliper')}
          className={`px-2 py-1 rounded flex items-center gap-1 font-semibold transition-all ${
            activeTool === 'caliper' ? 'bg-[#ec4899] text-white shadow-[0_0_10px_#ec4899]' : 'text-[#a1a1aa] hover:text-white hover:bg-[#20222e]'
          }`}
          title="Công cụ Thước Caliper 1D Biên Cạnh"
        >
          <Target className="w-3.5 h-3.5" />
          <span className="hidden md:inline text-[11px]">Caliper 1D</span>
        </button>

        {/* Tool: Pixel Probe */}
        <button
          onClick={() => setActiveTool('probe')}
          className={`px-2 py-1 rounded flex items-center gap-1 font-semibold transition-all ${
            activeTool === 'probe' ? 'bg-[#a855f7] text-white shadow-[0_0_10px_#a855f7]' : 'text-[#a1a1aa] hover:text-white hover:bg-[#20222e]'
          }`}
          title="Kính Lúp / Soi Điểm Ảnh (Pixel Loupe 7x7)"
        >
          <Search className="w-3.5 h-3.5" />
          <span className="hidden md:inline text-[11px]">Soi Điểm</span>
        </button>

        <div className="h-4 w-px bg-[#2b2c3a] mx-1" />

        {/* Quick Sample Image Selector */}
        <div className="flex items-center gap-1 bg-[#090a0f] border border-[#2b2c3a] px-2 py-0.5 rounded">
          <ImageIcon className="w-3.5 h-3.5 text-[#00f0ff]" />
          <select
            value={activeImageId}
            onChange={(e) => setActiveImageId(e.target.value)}
            className="bg-transparent text-[11px] font-bold text-[#00f0ff] outline-none cursor-pointer max-w-[155px] truncate"
            title="Chọn hình ảnh mẫu kiểm tra công nghiệp"
          >
            <option value="sample_bottle" className="bg-[#181920]">🧴 Chai Unilever & Nắp</option>
            <option value="sample_pcb" className="bg-[#181920]">📟 PCB & IC STM32</option>
            <option value="sample_calib" className="bg-[#181920]">🏁 Bàn Cờ Calib</option>
            <option value="sample_pharma" className="bg-[#181920]">💊 Vỉ Dược Phẩm</option>
            <option value="sample_barcode" className="bg-[#181920]">🏷️ Thùng Mã Barcode/QR</option>
            <option value="sample_gear" className="bg-[#181920]">⚙️ Bánh Răng Cơ Khí</option>
          </select>
        </div>

        <div className="h-4 w-px bg-[#2b2c3a] mx-1" />

        {/* Zoom Controls */}
        <button
          onClick={() => setScale(Math.max(0.05, scale / 1.25))}
          className="p-1 hover:bg-[#20222e] rounded text-[#a1a1aa] hover:text-white"
          title="Thu nhỏ (-)"
        >
          <ZoomOut className="w-3.5 h-3.5" />
        </button>
        <span className="font-mono text-[10.5px] text-[#71717a] w-10 text-center font-bold">
          {Math.round(scale * 100)}%
        </span>
        <button
          onClick={() => setScale(Math.min(25.0, scale * 1.25))}
          className="p-1 hover:bg-[#20222e] rounded text-[#a1a1aa] hover:text-white"
          title="Phóng to (+)"
        >
          <ZoomIn className="w-3.5 h-3.5" />
        </button>
        <button
          onClick={() => setScale(1.0)}
          className="px-1.5 py-0.5 rounded text-[10px] font-mono hover:bg-[#20222e] text-[#a1a1aa] hover:text-white"
          title="Tỷ lệ 1:1 gốc"
        >
          1:1
        </button>
        <button
          onClick={() => {
            if (containerRef.current) {
              const rect = containerRef.current.getBoundingClientRect();
              fitToView(rect.width, rect.height);
            }
          }}
          className="p-1 hover:bg-[#20222e] rounded text-[#a1a1aa] hover:text-white"
          title="Khớp toàn bộ khung nhìn (Fit View)"
        >
          <Maximize2 className="w-3.5 h-3.5" />
        </button>

        <div className="h-4 w-px bg-[#2b2c3a] mx-1" />

        {/* Layer Toggles */}
        <button
          onClick={() => setShowOverlays(!showOverlays)}
          className={`p-1.5 rounded transition-colors ${showOverlays ? 'text-[#00f0ff] bg-[#00f0ff]/10' : 'text-[#71717a] hover:text-white'}`}
          title="Bật/Tắt Lớp Vẽ Trực Quan (Overlays)"
        >
          <Layers className="w-3.5 h-3.5" />
        </button>

        <button
          onClick={() => setShowHistogram(!showHistogram)}
          className={`p-1.5 rounded transition-colors ${showHistogram ? 'text-[#10b981] bg-[#10b981]/10' : 'text-[#71717a] hover:text-white'}`}
          title="Bật/Tắt Biểu Đồ Tần Suất Xám (Histogram)"
        >
          <BarChart2 className="w-3.5 h-3.5" />
        </button>

        {/* Clear Tools Button */}
        {(measurement.p1 || userRoi.width > 0 || caliperRay.p1) && (
          <button
            onClick={clearInteractiveTools}
            className="p-1.5 rounded text-[#ef4444] hover:bg-[#ef4444]/20 transition-colors ml-1"
            title="Xóa kết quả đo & vùng chọn vẽ"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {/* Interactive Teaching & Picking Mode Banner HUD */}
      {pickingTarget && (
        <div className="absolute top-14 left-1/2 -translate-x-1/2 bg-[#12131a]/95 border-2 border-[#00f0ff] shadow-[0_0_30px_rgba(0,240,255,0.45)] px-4 py-2 rounded-xl flex items-center gap-3 z-50 animate-pulse">
          <div className="w-2.5 h-2.5 rounded-full bg-[#00f0ff] animate-ping" />
          <span className="text-xs font-bold text-white tracking-wide">
            {pickingTarget === 'point' && '🎯 CHỌN ĐIỂM: Nhấp chuột trái lên ảnh để chọn tọa độ (X, Y) cho khối'}
            {pickingTarget === 'roi' && '📐 CHỌN VÙNG ROI: Nhấn giữ và kéo chuột trên ảnh để xác định vùng quan tâm'}
            {pickingTarget === 'template' && '⭐ TEACHING: Vẽ khung mẫu phôi vàng để lưu mẫu tham chiếu máy học'}
          </span>
          <button
            onClick={() => {
              setPickingTarget(null);
              setActiveTool('pan');
            }}
            className="px-2.5 py-1 rounded bg-red-500/20 hover:bg-red-500/40 border border-red-500/60 text-red-300 font-bold text-[10.5px] transition-colors"
          >
            Hủy Chọn
          </button>
        </div>
      )}

      {/* 2. Main Transform Canvas */}
      <div
        className="absolute transition-transform duration-75 ease-out origin-top-left"
        style={{
          transform: `translate(${offset.x}px, ${offset.y}px) scale(${scale})`,
        }}
      >
        <div
          className="relative shadow-[0_0_35px_rgba(0,0,0,0.85)] border border-[#27272a] bg-[#101014]"
          style={{
            width: `${activeImageMeta?.width || 1920}px`,
            height: `${activeImageMeta?.height || 1080}px`,
          }}
        >
          {/* Inspection Image Stream */}
          <img
            ref={imgRef}
            src={imageUrl}
            alt="Inspection Target"
            className="w-full h-full object-contain pointer-events-none"
            style={{ imageRendering: scale > 2.0 ? 'pixelated' : 'auto' }}
            draggable={false}
          />

          {/* High-Precision Vector Overlays */}
          {showOverlays && (
            <svg
              className="absolute inset-0 w-full h-full pointer-events-none overflow-visible"
              viewBox={`0 0 ${activeImageMeta?.width || 1920} ${activeImageMeta?.height || 1080}`}
            >
              {/* --- A. SAMPLE PRESET 1: BOTTLE CAP & DEFECTS --- */}
              {activeImageId === 'sample_bottle' && (
                <g>
                  {/* Inspection ROI Box */}
                  <rect
                    x="180"
                    y="150"
                    width="1580"
                    height="920"
                    fill="rgba(0, 240, 255, 0.03)"
                    stroke="#00f0ff"
                    strokeWidth="3"
                    strokeDasharray="12 8"
                  />
                  <text x="200" y="190" fill="#00f0ff" fontSize="24" fontWeight="bold" fontFamily="Consolas">
                    GLOBAL ROI: UNILEVER BOTTLE CELL [510 SPP]
                  </text>

                  {/* Bottle 1 */}
                  <g>
                    <rect x="340" y="340" width="240" height="520" fill="none" stroke="#10b981" strokeWidth="3" />
                    <line x1="435" y1="600" x2="465" y2="600" stroke="#f59e0b" strokeWidth="3" />
                    <line x1="450" y1="585" x2="450" y2="615" stroke="#f59e0b" strokeWidth="3" />
                    <line x1="450" y1="600" x2="470" y2="540" stroke="#d946ef" strokeWidth="3" />
                    <circle cx="470" cy="540" r="5" fill="#d946ef" />
                    <rect x="330" y="295" width="310" height="34" rx="4" fill="rgba(18,18,22,0.9)" stroke="#10b981" strokeWidth="2" />
                    <text x="342" y="318" fill="#10b981" fontSize="15" fontWeight="bold" fontFamily="Consolas">
                      P01 [PASS] X:+450.0 Y:+600.0mm A:+15.0°
                    </text>
                  </g>

                  {/* Bottle 2 */}
                  <g>
                    <rect x="880" y="360" width="240" height="520" fill="none" stroke="#10b981" strokeWidth="3" />
                    <line x1="985" y1="620" x2="1015" y2="620" stroke="#f59e0b" strokeWidth="3" />
                    <line x1="1000" y1="605" x2="1000" y2="635" stroke="#f59e0b" strokeWidth="3" />
                    <rect x="870" y="315" width="310" height="34" rx="4" fill="rgba(18,18,22,0.9)" stroke="#10b981" strokeWidth="2" />
                    <text x="882" y="338" fill="#10b981" fontSize="15" fontWeight="bold" fontFamily="Consolas">
                      P02 [PASS] X:+1000.0 Y:+620.0mm A:-08.0°
                    </text>
                  </g>
                </g>
              )}

              {/* --- B. SAMPLE PRESET 2: PCB SMT ASSEMBLY --- */}
              {activeImageId === 'sample_pcb' && (
                <g>
                  {/* IC QFP Package */}
                  <rect x="540" y="340" width="520" height="520" fill="rgba(0, 240, 255, 0.06)" stroke="#00f0ff" strokeWidth="3" strokeDasharray="8 6" />
                  <text x="550" y="325" fill="#00f0ff" fontSize="20" fontWeight="bold" fontFamily="Consolas">
                    IC1: STM32F407 [100-PIN TQFP: PITCH=0.5mm OK]
                  </text>
                  {/* Pin Count Caliper Rays */}
                  <line x1="550" y1="340" x2="1050" y2="340" stroke="#10b981" strokeWidth="2.5" />
                  <line x1="550" y1="860" x2="1050" y2="860" stroke="#10b981" strokeWidth="2.5" />
                  {/* SMT Passives */}
                  <rect x="1140" y="240" width="250" height="280" fill="none" stroke="#f59e0b" strokeWidth="2" />
                  <text x="1140" y="230" fill="#f59e0b" fontSize="16" fontWeight="bold" fontFamily="Consolas">
                    SMD 0805 [6/6 PRESENT]
                  </text>
                </g>
              )}

              {/* --- C. SAMPLE PRESET 3: CALIBRATION CHECKERBOARD --- */}
              {activeImageId === 'sample_calib' && (
                <g>
                  <circle cx="968" cy="608" r="45" fill="none" stroke="#ec4899" strokeWidth="3" />
                  <line x1="918" y1="608" x2="1018" y2="608" stroke="#ec4899" strokeWidth="2" />
                  <line x1="968" y1="558" x2="968" y2="658" stroke="#ec4899" strokeWidth="2" />
                  <rect x="170" y="110" width="1540" height="980" fill="none" stroke="#10b981" strokeWidth="2.5" strokeDasharray="10 5" />
                  <text x="190" y="95" fill="#10b981" fontSize="22" fontWeight="bold" fontFamily="Consolas">
                    HOMOGRAPHY CALIBRATION: REPROJECTION ERROR = 0.042 px [99.8%]
                  </text>
                </g>
              )}

              {/* --- D. SAMPLE PRESET 4: PHARMACEUTICAL BLISTER PACK --- */}
              {activeImageId === 'sample_pharma' && (
                <g>
                  <rect x="350" y="230" width="1220" height="620" fill="none" stroke="#00f0ff" strokeWidth="2.5" strokeDasharray="8 6" />
                  <text x="360" y="215" fill="#00f0ff" fontSize="22" fontWeight="bold" fontFamily="Consolas">
                    BLISTER INSPECTION: 10 POCKETS (9 OK, 1 DEFECT)
                  </text>
                  {/* Defect on pocket 4 */}
                  <rect x="940" y="320" width="200" height="240" fill="rgba(239, 68, 68, 0.2)" stroke="#ef4444" strokeWidth="3.5" />
                  <text x="950" y="305" fill="#ef4444" fontSize="18" fontWeight="bold" fontFamily="Consolas">
                    DEFECT: MISSING PILL #04
                  </text>
                </g>
              )}

              {/* --- E. SAMPLE PRESET 5: BARCODE & QR CODE --- */}
              {activeImageId === 'sample_barcode' && (
                <g>
                  {/* 1D Barcode */}
                  <rect x="460" y="400" width="620" height="240" fill="none" stroke="#10b981" strokeWidth="3" />
                  <text x="465" y="385" fill="#10b981" fontSize="18" fontWeight="bold" fontFamily="Consolas">
                    CODE128: 893456789012 [GRADE: A (ISO 15416)]
                  </text>
                  {/* 2D QR Code */}
                  <rect x="1170" y="410" width="200" height="200" fill="none" stroke="#00f0ff" strokeWidth="3" />
                  <text x="1170" y="395" fill="#00f0ff" fontSize="18" fontWeight="bold" fontFamily="Consolas">
                    DATAMATRIX: ECC200 [OK]
                  </text>
                </g>
              )}

              {/* --- F. SAMPLE PRESET 6: GEAR METROLOGY --- */}
              {activeImageId === 'sample_gear' && (
                <g>
                  {/* Outer pitch circle */}
                  <circle cx="960" cy="540" r="380" fill="none" stroke="#10b981" strokeWidth="3" strokeDasharray="6 4" />
                  <text x="980" y="150" fill="#10b981" fontSize="20" fontWeight="bold" fontFamily="Consolas">
                    PITCH CIRCLE DIA: 760.00 mm (TEETH: 24)
                  </text>
                  {/* Inner bore hole */}
                  <circle cx="960" cy="540" r="130" fill="none" stroke="#00f0ff" strokeWidth="3" />
                  <text x="980" y="525" fill="#00f0ff" fontSize="18" fontWeight="bold" fontFamily="Consolas">
                    BORE DIA: 260.00 mm
                  </text>
                </g>
              )}

              {/* --- G. INTERACTIVE TOOL OVERLAYS --- */}

              {/* 1. Measurement Line */}
              {measurement.p1 && measurement.p2 && (
                <g>
                  <line
                    x1={measurement.p1.x}
                    y1={measurement.p1.y}
                    x2={measurement.p2.x}
                    y2={measurement.p2.y}
                    stroke="#10b981"
                    strokeWidth="3"
                  />
                  {/* Endpoint markers */}
                  <circle cx={measurement.p1.x} cy={measurement.p1.y} r="6" fill="#10b981" stroke="#ffffff" strokeWidth="2" />
                  <circle cx={measurement.p2.x} cy={measurement.p2.y} r="6" fill="#10b981" stroke="#ffffff" strokeWidth="2" />
                  {/* Measurement HUD Box */}
                  <g transform={`translate(${(measurement.p1.x + measurement.p2.x) / 2}, ${(measurement.p1.y + measurement.p2.y) / 2 - 25})`}>
                    <rect x="-160" y="-14" width="320" height="28" rx="5" fill="rgba(12, 14, 20, 0.92)" stroke="#10b981" strokeWidth="2" />
                    <text x="0" y="5" fill="#10b981" fontSize="14" fontWeight="bold" fontFamily="Consolas" textAnchor="middle">
                      L: {measurement.distPx}px ({measurement.distMm}mm) | ∠: {measurement.angleDeg}°
                    </text>
                  </g>
                </g>
              )}

              {/* 2. Interactive User ROI Box */}
              {userRoi.active && userRoi.width > 5 && userRoi.height > 5 && (
                <g>
                  <rect
                    x={userRoi.x}
                    y={userRoi.y}
                    width={userRoi.width}
                    height={userRoi.height}
                    fill="rgba(245, 158, 11, 0.08)"
                    stroke="#f59e0b"
                    strokeWidth="3"
                    strokeDasharray="8 6"
                  />
                  {/* Corner handles */}
                  <rect x={userRoi.x - 5} y={userRoi.y - 5} width="10" height="10" fill="#f59e0b" />
                  <rect x={userRoi.x + userRoi.width - 5} y={userRoi.y - 5} width="10" height="10" fill="#f59e0b" />
                  <rect x={userRoi.x - 5} y={userRoi.y + userRoi.height - 5} width="10" height="10" fill="#f59e0b" />
                  <rect x={userRoi.x + userRoi.width - 5} y={userRoi.y + userRoi.height - 5} width="10" height="10" fill="#f59e0b" />
                  {/* ROI Label */}
                  <g transform={`translate(${userRoi.x}, ${userRoi.y - 12})`}>
                    <rect x="0" y="-18" width="260" height="24" rx="4" fill="rgba(18, 18, 24, 0.9)" stroke="#f59e0b" strokeWidth="1.5" />
                    <text x="10" y="-2" fill="#f59e0b" fontSize="13" fontWeight="bold" fontFamily="Consolas">
                      ROI: X:{userRoi.x} Y:{userRoi.y} W:{userRoi.width} H:{userRoi.height}
                    </text>
                  </g>
                </g>
              )}

              {/* 3. Interactive Caliper 1D Ray */}
              {caliperRay.p1 && caliperRay.p2 && (
                <g>
                  {/* Ray Line */}
                  <line
                    x1={caliperRay.p1.x}
                    y1={caliperRay.p1.y}
                    x2={caliperRay.p2.x}
                    y2={caliperRay.p2.y}
                    stroke="#ec4899"
                    strokeWidth="3"
                  />
                  {/* Search Profile Band */}
                  <line
                    x1={caliperRay.p1.x}
                    y1={caliperRay.p1.y}
                    x2={caliperRay.p2.x}
                    y2={caliperRay.p2.y}
                    stroke="rgba(236, 72, 153, 0.25)"
                    strokeWidth="24"
                  />
                  {/* End Arrows */}
                  <circle cx={caliperRay.p1.x} cy={caliperRay.p1.y} r="5" fill="#ec4899" />
                  <circle cx={caliperRay.p2.x} cy={caliperRay.p2.y} r="5" fill="#ec4899" />
                  {/* Sub-Pixel Found Edge Marker */}
                  {caliperRay.edgePos && (
                    <g>
                      <line
                        x1={caliperRay.edgePos.x - 15}
                        y1={caliperRay.edgePos.y - 15}
                        x2={caliperRay.edgePos.x + 15}
                        y2={caliperRay.edgePos.y + 15}
                        stroke="#00f0ff"
                        strokeWidth="3"
                      />
                      <line
                        x1={caliperRay.edgePos.x + 15}
                        y1={caliperRay.edgePos.y - 15}
                        x2={caliperRay.edgePos.x - 15}
                        y2={caliperRay.edgePos.y + 15}
                        stroke="#00f0ff"
                        strokeWidth="3"
                      />
                      <circle cx={caliperRay.edgePos.x} cy={caliperRay.edgePos.y} r="8" fill="none" stroke="#00f0ff" strokeWidth="2" />
                      <g transform={`translate(${caliperRay.edgePos.x + 20}, ${caliperRay.edgePos.y - 10})`}>
                        <rect x="0" y="-16" width="220" height="24" rx="4" fill="rgba(10, 10, 15, 0.9)" stroke="#00f0ff" strokeWidth="1.5" />
                        <text x="8" y="0" fill="#00f0ff" fontSize="12" fontWeight="bold" fontFamily="Consolas">
                          EDGE @ ({caliperRay.edgePos.x}, {caliperRay.edgePos.y}) K={caliperRay.contrast}
                        </text>
                      </g>
                    </g>
                  )}
                </g>
              )}

              {/* 4. Golden Master Trained Template Display */}
              {trainedTemplate?.isTrained && trainedTemplate.roi && (
                <g>
                  <rect
                    x={trainedTemplate.roi.x}
                    y={trainedTemplate.roi.y}
                    width={trainedTemplate.roi.width}
                    height={trainedTemplate.roi.height}
                    fill="rgba(245, 158, 11, 0.12)"
                    stroke="#f59e0b"
                    strokeWidth="2.5"
                    strokeDasharray="6 4"
                  />
                  <g transform={`translate(${trainedTemplate.roi.x}, ${trainedTemplate.roi.y - 10})`}>
                    <rect x="0" y="-18" width="250" height="20" rx="3" fill="rgba(18, 14, 8, 0.95)" stroke="#f59e0b" strokeWidth="1" />
                    <text x="6" y="-4" fill="#f59e0b" fontSize="11" fontWeight="bold" fontFamily="Consolas">
                      ★ MẪU VÀNG: {trainedTemplate.name}
                    </text>
                  </g>
                </g>
              )}

              {/* --- H. DYNAMIC PIPELINE ENGINE OVERLAYS --- */}
              {pipelineOverlays && pipelineOverlays.map((ov, idx) => {
                if (ov.type === 'rect') {
                  const c = ov.coordinates || {};
                  return (
                    <g key={`pipe_${idx}`}>
                      <rect
                        x={c.x || 0}
                        y={c.y || 0}
                        width={c.width || 0}
                        height={c.height || 0}
                        fill={ov.filled ? (ov.fill_color || 'rgba(0, 240, 255, 0.2)') : 'none'}
                        stroke={ov.color || '#00f0ff'}
                        strokeWidth={ov.thickness || 2.5}
                      />
                      {ov.label && (
                        <text
                          x={(c.x || 0) + 6}
                          y={(c.y || 0) - 8}
                          fill={ov.color || '#00f0ff'}
                          fontSize="15"
                          fontWeight="bold"
                          fontFamily="Consolas"
                        >
                          {ov.label}
                        </text>
                      )}
                    </g>
                  );
                }
                if (ov.type === 'crosshair') {
                  const c = ov.coordinates || {};
                  const cx = c.cx || 0;
                  const cy = c.cy || 0;
                  const s = (c.size || 30) / 2;
                  return (
                    <g key={`pipe_${idx}`}>
                      <line x1={cx - s} y1={cy} x2={cx + s} y2={cy} stroke={ov.color || '#00f0ff'} strokeWidth={ov.thickness || 2} />
                      <line x1={cx} y1={cy - s} x2={cx} y2={cy + s} stroke={ov.color || '#00f0ff'} strokeWidth={ov.thickness || 2} />
                      <circle cx={cx} cy={cy} r="4" fill={ov.color || '#00f0ff'} />
                    </g>
                  );
                }
                return null;
              })}
            </svg>
          )}
        </div>
      </div>

      {/* 3. Floating HUD Panels */}
      <HistogramOverlay />
      <PixelProbeOverlay />

      {/* 4. Bottom-Left Coordinate & Intensity HUD Bar */}
      <div className="absolute bottom-3 left-3 bg-[#101016]/90 backdrop-blur-md border border-[#272733] px-3 py-1.5 rounded text-[11px] font-mono text-[#a1a1aa] flex items-center gap-3 select-none pointer-events-none z-30">
        <span className="flex items-center gap-1.5 text-[#00f0ff] font-bold">
          <Crosshair className="w-3.5 h-3.5" />
          <span>
            {cursorPos ? `X: ${cursorPos.x} px | Y: ${cursorPos.y} px` : 'X: ---- px | Y: ---- px'}
          </span>
        </span>
        <span className="text-[#3f3f46]">|</span>
        <span>
          Độ Phân Giải: {activeImageMeta?.width || 1920} x {activeImageMeta?.height || 1080} ({activeImageMeta?.color_space || 'GRAY'})
        </span>
        <span className="text-[#3f3f46]">|</span>
        <span className="text-[#10b981] font-semibold">
          Công Cụ: {activeTool.toUpperCase()}
        </span>
      </div>
    </div>
  );
};
