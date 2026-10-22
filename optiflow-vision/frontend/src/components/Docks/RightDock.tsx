import React from 'react';
import { useDockStore } from '../../state/useDockStore';
import { useGraphStore } from '../../state/useGraphStore';
import { useViewerStore } from '../../state/useViewerStore';
import { useI18n } from '../../i18n';
import { PropertyInspector } from '../GraphEditor/PropertyInspector';
import { ImageViewer } from '../ImageViewer/ImageViewer';
import { 
  Sliders, Eye, PanelRightClose, Activity, Layers, Maximize2 
} from 'lucide-react';

export const RightDock: React.FC = () => {
  const { lang } = useI18n();
  const { rightDock, setRightDock } = useDockStore();
  const { selectedNodeId, nodes } = useGraphStore();
  const { activeImageId } = useViewerStore();

  const startResizingRight = (e: React.MouseEvent) => {
    e.preventDefault();
    const startX = e.clientX;
    const startWidth = rightDock.width;

    const onMouseMove = (moveEvent: MouseEvent) => {
      const newWidth = Math.max(260, Math.min(650, startWidth - (moveEvent.clientX - startX)));
      setRightDock({ width: newWidth });
    };

    const onMouseUp = () => {
      window.removeEventListener('mousemove', onMouseMove);
      window.removeEventListener('mouseup', onMouseUp);
    };

    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('mouseup', onMouseUp);
  };

  if (!rightDock.isOpen) {
    return (
      <aside className="w-8 bg-[#131418] border-l border-[#252630] flex flex-col items-center py-2 select-none z-20 shrink-0 gap-3">
        <button
          onClick={() => setRightDock({ isOpen: true })}
          className="p-1.5 rounded hover:bg-[#252630] text-[#00f0ff] hover:text-white transition-colors"
          title="Mở rộng Dock Phải (Thuộc Tính & Xem Dữ Liệu)"
        >
          <PanelRightClose className="w-4 h-4 rotate-180" />
        </button>
        <div className="h-4 w-px bg-[#252630]" />
        <button
          onClick={() => setRightDock({ isOpen: true, activeTab: 'inspector' })}
          className="p-1 rounded text-[#a1a1aa] hover:text-[#00f0ff] hover:bg-[#252630]"
          title="Mở Bảng Thuộc Tính & Teaching"
        >
          <Sliders className="w-4 h-4" />
        </button>
        <button
          onClick={() => setRightDock({ isOpen: true, activeTab: 'preview' })}
          className="p-1 rounded text-[#a1a1aa] hover:text-[#10b981] hover:bg-[#252630]"
          title="Mở Xem Dữ Liệu Live"
        >
          <Eye className="w-4 h-4" />
        </button>
      </aside>
    );
  }

  return (
    <aside 
      className="relative bg-[#131418] border-l border-[#252630] flex flex-col h-full select-none z-20 shrink-0 group/dock"
      style={{ width: `${rightDock.width}px`, minWidth: `${rightDock.width}px`, maxWidth: `${rightDock.width}px` }}
    >
      {/* Draggable Resize Handle */}
      <div
        onMouseDown={startResizingRight}
        className="absolute top-0 -left-1 w-2 h-full cursor-col-resize hover:bg-[#00f0ff]/80 active:bg-[#00f0ff] transition-colors z-40"
        title="Kéo chuột để thay đổi độ rộng Dock Phải"
      />
      {/* 1. Header with Tab Switcher */}
      <div className="h-8 px-2 bg-[#17181f] border-b border-[#252630] flex items-center justify-between text-xs">
        <div className="flex items-center gap-1">
          {/* Tab 1: Properties Inspector */}
          <button
            onClick={() => setRightDock({ activeTab: 'inspector' })}
            className={`px-2.5 py-1 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              rightDock.activeTab === 'inspector'
                ? 'bg-[#1e2029] text-[#00f0ff] border border-[#00f0ff]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <Sliders className="w-3.5 h-3.5" />
            <span>{lang === 'vi' ? 'Thuộc Tính' : 'Properties'}</span>
            {selectedNodeId && (
              <span className="w-1.5 h-1.5 rounded-full bg-[#00f0ff]" />
            )}
          </button>

          {/* Tab 2: Live Data Preview */}
          <button
            onClick={() => setRightDock({ activeTab: 'preview' })}
            className={`px-2.5 py-1 rounded font-semibold text-[11px] flex items-center gap-1.5 transition-all ${
              rightDock.activeTab === 'preview'
                ? 'bg-[#1e2029] text-[#10b981] border border-[#10b981]/30 shadow-sm'
                : 'text-[#71717a] hover:text-white'
            }`}
          >
            <Eye className="w-3.5 h-3.5" />
            <span>{lang === 'vi' ? 'Xem Dữ Liệu' : 'Data Preview'}</span>
          </button>
        </div>

        {/* Close Right Dock */}
        <button
          onClick={() => setRightDock({ isOpen: false })}
          className="p-1 rounded hover:bg-[#252630] text-[#71717a] hover:text-white transition-colors"
          title="Thu gọn Dock phải"
        >
          <PanelRightClose className="w-3.5 h-3.5" />
        </button>
      </div>

      {/* 2. Dock Body */}
      <div className="flex-1 overflow-hidden flex flex-col relative">
        {rightDock.activeTab === 'inspector' ? (
          <div className="w-full h-full flex flex-col">
            <PropertyInspector />
          </div>
        ) : (
          <div className="w-full h-full flex flex-col relative bg-[#09090b]">
            <ImageViewer />
          </div>
        )}
      </div>
    </aside>
  );
};
