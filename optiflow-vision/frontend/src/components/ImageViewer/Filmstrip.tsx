import React from 'react';
import { useViewerStore } from '../../state/useViewerStore';
import { useSidecarStore } from '../../state/useSidecarStore';
import { useDockStore } from '../../state/useDockStore';
import { useI18n } from '../../i18n';
import { 
  Film, ChevronLeft, ChevronRight, Plus, Eye, 
  Maximize2, X, ChevronDown, ChevronUp, Image as ImageIcon 
} from 'lucide-react';

export const Filmstrip: React.FC = () => {
  const { lang } = useI18n();
  const { baseUrl } = useSidecarStore();
  const { filmstrip, setFilmstripOpen } = useDockStore();
  const { 
    activeImageId, setActiveImageId, availableImages, activeImageMeta 
  } = useViewerStore();

  if (!filmstrip.isOpen) {
    return (
      <div className="h-6 bg-[#101014] border-t border-[#252630] flex items-center justify-between px-3 text-[10.5px] select-none text-[#71717a]">
        <button
          onClick={() => setFilmstripOpen(true)}
          className="flex items-center gap-1.5 hover:text-[#00f0ff] transition-colors"
          title="Mở Băng Hình Ảnh (Filmstrip)"
        >
          <Film className="w-3.5 h-3.5" />
          <span>Filmstrip ({availableImages.length} Images)</span>
          <ChevronUp className="w-3 h-3" />
        </button>
        <span className="font-mono text-[10px] text-[#52525b]">
          {activeImageId} ({activeImageMeta?.width}x{activeImageMeta?.height})
        </span>
      </div>
    );
  }

  const currentIndex = availableImages.findIndex((img) => img.id === activeImageId);

  const handlePrev = () => {
    if (availableImages.length === 0) return;
    const prevIdx = (currentIndex - 1 + availableImages.length) % availableImages.length;
    setActiveImageId(availableImages[prevIdx].id);
  };

  const handleNext = () => {
    if (availableImages.length === 0) return;
    const nextIdx = (currentIndex + 1) % availableImages.length;
    setActiveImageId(availableImages[nextIdx].id);
  };

  const handleImportImage = () => {
    alert(lang === 'vi' 
      ? 'Chức năng nạp ảnh từ Camera / Ổ đĩa ngoài sẽ mở hộp thoại chọn tệp hình ảnh (PNG, BMP, TIFF, JPG).' 
      : 'Import Image from camera or disk (PNG, BMP, TIFF, JPG).');
  };

  return (
    <div className="h-24 bg-[#121317] border-t border-[#252630] flex flex-col select-none z-10 shrink-0">
      {/* Strip Header */}
      <div className="h-6 px-3 bg-[#15161c] border-b border-[#202129] flex items-center justify-between text-[10.5px] text-[#71717a]">
        <div className="flex items-center gap-2">
          <Film className="w-3.5 h-3.5 text-[#00f0ff]" />
          <span className="font-bold text-[#e4e4e7] uppercase tracking-wider text-[10px]">
            {lang === 'vi' ? 'Băng Hình Tập Ảnh (Filmstrip)' : 'Image Set Filmstrip'}
          </span>
          <span className="font-mono text-[10px] text-[#a1a1aa] px-1.5 py-0.2 rounded bg-[#202129]">
            Frame: {currentIndex >= 0 ? currentIndex + 1 : 1} / {availableImages.length || 1}
          </span>
        </div>

        <div className="flex items-center gap-1.5">
          {/* Previous / Next buttons */}
          <button
            onClick={handlePrev}
            className="p-1 rounded hover:bg-[#26262c] text-[#a1a1aa] hover:text-white"
            title="Khung hình trước (Previous Frame)"
          >
            <ChevronLeft className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={handleNext}
            className="p-1 rounded hover:bg-[#26262c] text-[#a1a1aa] hover:text-white"
            title="Khung hình kế tiếp (Next Frame)"
          >
            <ChevronRight className="w-3.5 h-3.5" />
          </button>

          <div className="h-3 w-px bg-[#26262c] mx-1" />

          {/* Import New Image */}
          <button
            onClick={handleImportImage}
            className="px-2 py-0.5 rounded bg-[#1e2028] hover:bg-[#0284c7] hover:text-white text-[#00f0ff] border border-[#26262c] font-semibold text-[10px] flex items-center gap-1 transition-all"
            title="Nạp tệp ảnh vào tập dữ liệu"
          >
            <Plus className="w-3 h-3" />
            <span>Nạp Ảnh</span>
          </button>

          {/* Minimize / Collapse */}
          <button
            onClick={() => setFilmstripOpen(false)}
            className="p-1 rounded hover:bg-[#26262c] text-[#71717a] hover:text-white transition-colors"
            title="Thu nhỏ filmstrip"
          >
            <ChevronDown className="w-3.5 h-3.5" />
          </button>
        </div>
      </div>

      {/* Thumbnails Carousel */}
      <div className="flex-1 flex items-center gap-2.5 px-3 py-1.5 overflow-x-auto scrollbar-none">
        {availableImages.map((img) => {
          const isActive = img.id === activeImageId;
          const thumbUrl = `${baseUrl}/api/image/preview/${img.id}?max_dim=180&quality=80`;

          return (
            <div
              key={img.id}
              onClick={() => setActiveImageId(img.id)}
              className={`group relative h-full aspect-[4/3] rounded bg-[#09090b] cursor-pointer overflow-hidden transition-all duration-150 flex flex-col justify-end border-2 ${
                isActive
                  ? 'border-[#00f0ff] shadow-[0_0_12px_rgba(0,240,255,0.4)] ring-1 ring-[#00f0ff]'
                  : 'border-[#26262c] hover:border-[#3f3f46]'
              }`}
            >
              {/* Thumbnail image */}
              <img
                src={thumbUrl}
                alt={img.id}
                className="absolute inset-0 w-full h-full object-cover"
                draggable={false}
              />

              {/* Gradient Bottom Title Bar */}
              <div className="relative z-10 w-full bg-gradient-to-t from-black/90 via-black/60 to-transparent p-1 flex items-center justify-between text-[9px] font-mono">
                <span className={`truncate max-w-[100px] font-semibold ${isActive ? 'text-[#00f0ff]' : 'text-white'}`}>
                  {img.id === 'sample_bottle' ? '🧴 Chai Unilever' :
                   img.id === 'sample_pcb' ? '📟 PCB STM32' :
                   img.id === 'sample_calib' ? '🏁 Calib Target' :
                   img.id === 'sample_pharma' ? '💊 Vỉ Thuốc Lỗi' :
                   img.id === 'sample_barcode' ? '🏷️ QR/Barcode' :
                   img.id === 'sample_gear' ? '⚙️ Bánh Răng' : img.id}
                </span>
                <span className="text-[#a1a1aa] text-[8.5px]">
                  {img.width}x{img.height}
                </span>
              </div>

              {isActive && (
                <div className="absolute top-1 right-1 w-2 h-2 rounded-full bg-[#00f0ff] shadow-[0_0_6px_#00f0ff]" />
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
