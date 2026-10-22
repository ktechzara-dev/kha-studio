import React, { useState } from 'react';
import { useI18n } from '../../i18n';
import { useProjectStore } from '../../state/useProjectStore';
import { useSidecarStore } from '../../state/useSidecarStore';
import { useViewerStore } from '../../state/useViewerStore';
import { useDockStore } from '../../state/useDockStore';
import { useGraphStore } from '../../state/useGraphStore';
import { 
  FolderPlus, FolderOpen, Save, FileText, Image, Power, 
  Maximize, ZoomIn, Eye, Activity, ShieldCheck, Globe, Check,
  RotateCcw, Sliders, Terminal, Sun, Moon, Search, Sparkles, Cpu,
  Undo2, Redo2, LayoutDashboard
} from 'lucide-react';

export const TopMenuBar: React.FC = () => {
  const { t, lang, setLanguage } = useI18n();
  const { project, filePath, isDirty, setNewProjectModalOpen, saveProjectToFile } = useProjectStore();
  const { isOnline, latencyMs, memoryMb } = useSidecarStore();
  const { 
    showHistogram, setShowHistogram, 
    showProbe, setShowProbe, 
    showOverlays, setShowOverlays, 
    resetView 
  } = useViewerStore();

  const { 
    leftDock, setLeftDock, 
    rightDock, setRightDock, 
    bottomDock, setBottomDock, 
    filmstrip, setFilmstripOpen,
    currentWorkspace, setWorkspace, resetLayout,
    theme, toggleTheme, setCommandPaletteOpen 
  } = useDockStore();

  const { runOnce } = useGraphStore();

  const [activeMenu, setActiveMenu] = useState<string | null>(null);

  const handleOpenLocalProject = async () => {
    const path = prompt(lang === 'vi' ? 'Nhập đường dẫn file dự án .vsproj:' : 'Enter .vsproj file path:');
    if (path) {
      await useProjectStore.getState().openProjectFromFile(path);
    }
  };

  const handleSave = async () => {
    if (!filePath) {
      const path = prompt(lang === 'vi' ? 'Lưu dự án thành (đường dẫn .vsproj):' : 'Save project as (.vsproj path):');
      if (path) {
        await saveProjectToFile(path);
      }
    } else {
      await saveProjectToFile();
    }
  };

  return (
    <header className="h-9 bg-[#131418] border-b border-[#252630] flex items-center justify-between px-3 text-xs select-none z-50">
      {/* LEFT: Branding & Main Menus */}
      <div className="flex items-center gap-3">
        {/* Brand Logo & Name */}
        <div className="flex items-center gap-2 pr-3 border-r border-[#252630]">
          <div className="w-5 h-5 rounded bg-gradient-to-br from-[#00f0ff] to-[#0284c7] flex items-center justify-center shadow-[0_0_8px_rgba(0,240,255,0.4)]">
            <Activity className="w-3.5 h-3.5 text-black stroke-[2.5]" />
          </div>
          <span className="font-bold tracking-wide text-[#00f0ff] text-sm">
            OptiFlow
          </span>
          <span className="text-[10px] text-[#71717a] font-semibold uppercase tracking-wider hidden md:inline">
            Vision Studio
          </span>
        </div>

        {/* Menu Buttons */}
        <div className="flex items-center gap-0.5">
          {/* File Menu */}
          <div className="relative">
            <button 
              onClick={() => setActiveMenu(activeMenu === 'file' ? null : 'file')}
              className={`px-2.5 py-1 rounded transition-colors ${activeMenu === 'file' ? 'bg-[#26262c] text-[#00f0ff]' : 'hover:bg-[#1e1e23] text-[#e4e4e7]'}`}
            >
              {t('menu.file')}
            </button>
            {activeMenu === 'file' && (
              <div 
                className="absolute left-0 top-full mt-1 w-56 hud-panel py-1 text-xs z-50 shadow-2xl"
                onMouseLeave={() => setActiveMenu(null)}
              >
                <button 
                  onClick={() => { setNewProjectModalOpen(true); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span className="flex items-center gap-2"><FolderPlus className="w-3.5 h-3.5" />{t('menu.new_project')}</span>
                  <span className="text-[10px] text-[#71717a] font-mono">Ctrl+N</span>
                </button>
                <button 
                  onClick={() => { handleOpenLocalProject(); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span className="flex items-center gap-2"><FolderOpen className="w-3.5 h-3.5" />{t('menu.open_project')}</span>
                  <span className="text-[10px] text-[#71717a] font-mono">Ctrl+O</span>
                </button>
                <div className="my-1 border-t border-[#26262c]" />
                <button 
                  onClick={() => { handleSave(); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span className="flex items-center gap-2"><Save className="w-3.5 h-3.5" />{t('menu.save_project')}</span>
                  <span className="text-[10px] text-[#71717a] font-mono">Ctrl+S</span>
                </button>
              </div>
            )}
          </div>

          {/* Edit Menu */}
          <div className="relative">
            <button 
              onClick={() => setActiveMenu(activeMenu === 'edit' ? null : 'edit')}
              className={`px-2.5 py-1 rounded transition-colors ${activeMenu === 'edit' ? 'bg-[#26262c] text-[#00f0ff]' : 'hover:bg-[#1e1e23] text-[#e4e4e7]'}`}
            >
              Chỉnh Sửa
            </button>
            {activeMenu === 'edit' && (
              <div 
                className="absolute left-0 top-full mt-1 w-52 hud-panel py-1 text-xs z-50 shadow-2xl"
                onMouseLeave={() => setActiveMenu(null)}
              >
                <button className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left">
                  <span className="flex items-center gap-2"><Undo2 className="w-3.5 h-3.5" />Hoàn tác (Undo)</span>
                  <span className="text-[10px] text-[#71717a] font-mono">Ctrl+Z</span>
                </button>
                <button className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left">
                  <span className="flex items-center gap-2"><Redo2 className="w-3.5 h-3.5" />Làm lại (Redo)</span>
                  <span className="text-[10px] text-[#71717a] font-mono">Ctrl+Y</span>
                </button>
                <div className="my-1 border-t border-[#26262c]" />
                <button 
                  onClick={() => { setCommandPaletteOpen(true); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span className="flex items-center gap-2"><Search className="w-3.5 h-3.5 text-[#00f0ff]" />Command Palette</span>
                  <span className="text-[10px] text-[#00f0ff] font-mono font-bold">Ctrl+P</span>
                </button>
              </div>
            )}
          </div>

          {/* View Menu */}
          <div className="relative">
            <button 
              onClick={() => setActiveMenu(activeMenu === 'view' ? null : 'view')}
              className={`px-2.5 py-1 rounded transition-colors ${activeMenu === 'view' ? 'bg-[#26262c] text-[#00f0ff]' : 'hover:bg-[#1e1e23] text-[#e4e4e7]'}`}
            >
              {t('menu.view')}
            </button>
            {activeMenu === 'view' && (
              <div 
                className="absolute left-0 top-full mt-1 w-60 hud-panel py-1 text-xs z-50 shadow-2xl"
                onMouseLeave={() => setActiveMenu(null)}
              >
                <button 
                  onClick={() => { resetLayout(); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left font-bold text-[#00f0ff]"
                >
                  <span className="flex items-center gap-2"><RotateCcw className="w-3.5 h-3.5" />Khôi Phục Bố Cục (Reset Layout)</span>
                </button>
                <div className="my-1 border-t border-[#26262c]" />
                <button 
                  onClick={() => { setLeftDock({ isOpen: !leftDock.isOpen }); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span>Bảng Trái (Filter Catalog / Tree)</span>
                  {leftDock.isOpen && <Check className="w-3.5 h-3.5 text-[#10b981]" />}
                </button>
                <button 
                  onClick={() => { setRightDock({ isOpen: !rightDock.isOpen }); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span>Bảng Phải (Properties / Preview)</span>
                  {rightDock.isOpen && <Check className="w-3.5 h-3.5 text-[#10b981]" />}
                </button>
                <button 
                  onClick={() => { setBottomDock({ isOpen: !bottomDock.isOpen }); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span>Bảng Dưới (Console / Profiler)</span>
                  {bottomDock.isOpen && <Check className="w-3.5 h-3.5 text-[#10b981]" />}
                </button>
                <button 
                  onClick={() => { setFilmstripOpen(!filmstrip.isOpen); setActiveMenu(null); }}
                  className="w-full px-3 py-1.5 flex items-center justify-between hover:bg-[#0284c7]/20 hover:text-[#00f0ff] text-left"
                >
                  <span>Băng Ảnh (Filmstrip)</span>
                  {filmstrip.isOpen && <Check className="w-3.5 h-3.5 text-[#10b981]" />}
                </button>
              </div>
            )}
          </div>

          {/* Workspaces Menu */}
          <div className="relative">
            <button 
              onClick={() => setActiveMenu(activeMenu === 'workspaces' ? null : 'workspaces')}
              className={`px-2.5 py-1 rounded transition-colors ${activeMenu === 'workspaces' ? 'bg-[#26262c] text-[#00f0ff]' : 'hover:bg-[#1e1e23] text-[#e4e4e7]'}`}
            >
              Không Gian (Workspaces)
            </button>
            {activeMenu === 'workspaces' && (
              <div 
                className="absolute left-0 top-full mt-1 w-64 hud-panel py-1 text-xs z-50 shadow-2xl"
                onMouseLeave={() => setActiveMenu(null)}
              >
                {[
                  { id: 'Default', label: 'Default (Chuẩn IDE Golden Ratio)' },
                  { id: 'HMI Designer', label: 'HMI Designer (Thiết Kế Màn Hình HMI)' },
                  { id: 'Operator View', label: 'Operator View (Màn Hình Vận Hành)' },
                  { id: 'Inspection & Tuning', label: 'Inspection & Tuning (Soi Chi Tiết Ảnh)' },
                  { id: 'Graph Engineering', label: 'Graph Engineering (Thiết Kế Đồ Thị)' },
                ].map((ws) => (
                  <button
                    key={ws.id}
                    onClick={() => { setWorkspace(ws.id); setActiveMenu(null); }}
                    className={`w-full px-3 py-1.5 flex items-center justify-between text-left transition-colors ${
                      currentWorkspace === ws.id
                        ? 'bg-[#00f0ff]/20 text-[#00f0ff] font-semibold'
                        : 'hover:bg-[#1e1e23] text-[#a1a1aa] hover:text-white'
                    }`}
                  >
                    <span>{ws.label}</span>
                    {currentWorkspace === ws.id && <Check className="w-3.5 h-3.5 text-[#00f0ff]" />}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </div>

      {/* CENTER: Quick Command Search Trigger */}
      <button
        onClick={() => setCommandPaletteOpen(true)}
        className="hidden xl:flex items-center gap-2 px-2.5 py-0.5 rounded-full bg-[#1b1b22] hover:bg-[#23242c] border border-[#2b2c36] text-[11px] text-[#71717a] hover:text-[#f4f4f7] transition-all max-w-[200px]"
        title="Mở bảng lệnh nhanh (Ctrl+P)"
      >
        <Search className="w-3 h-3 text-[#00f0ff]" />
        <span>Gõ lệnh hoặc tìm bộ lọc...</span>
        <span className="ml-auto font-mono text-[9px] bg-[#292a34] px-1.5 py-0.2 rounded text-[#a1a1aa]">Ctrl+P</span>
      </button>

      {/* RIGHT: Security, Theme, Latency & Language */}
      <div className="flex items-center gap-2.5">
        {/* Offline Badge */}
        <div className="flex items-center gap-1 px-2 py-0.5 rounded bg-[#10b981]/10 border border-[#10b981]/30 text-[#10b981] text-[10px] font-bold">
          <ShieldCheck className="w-3 h-3" />
          <span>OFFLINE</span>
        </div>

        {/* Sidecar IPC Status */}
        <div className="flex items-center gap-1.5 text-[10.5px] font-mono px-2 py-0.5 rounded bg-[#1b1b20] border border-[#26262c]">
          <span className={`w-2 h-2 rounded-full ${isOnline ? 'bg-[#10b981] shadow-[0_0_6px_#10b981]' : 'bg-[#ef4444]'}`} />
          <span className={isOnline ? 'text-[#e4e4e7]' : 'text-[#ef4444]'}>
            {isOnline ? `${latencyMs}ms` : 'ERR'}
          </span>
        </div>

        {/* Theme Switcher */}
        <button
          onClick={toggleTheme}
          className="p-1 rounded hover:bg-[#26262c] text-[#a1a1aa] hover:text-[#f59e0b] transition-colors"
          title={`Chuyển giao diện: hiện tại là ${theme === 'dark' ? 'Dark Obsidian' : 'Studio Light'}`}
        >
          {theme === 'dark' ? <Sun className="w-3.5 h-3.5 text-[#f59e0b]" /> : <Moon className="w-3.5 h-3.5 text-[#38bdf8]" />}
        </button>

        {/* Language Switcher */}
        <button
          onClick={() => setLanguage(lang === 'vi' ? 'en' : 'vi')}
          className="flex items-center gap-1 px-2 py-0.5 rounded bg-[#1b1b20] hover:bg-[#26262c] border border-[#26262c] text-[#e4e4e7] transition-all font-semibold text-[11px]"
          title="Chuyển đổi ngôn ngữ"
        >
          <Globe className="w-3 h-3 text-[#00f0ff]" />
          <span>{lang === 'vi' ? 'VI' : 'EN'}</span>
        </button>
      </div>
    </header>
  );
};
