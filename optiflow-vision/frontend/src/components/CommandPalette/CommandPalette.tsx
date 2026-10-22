import React, { useState, useEffect, useRef } from 'react';
import { useDockStore } from '../../state/useDockStore';
import { useGraphStore } from '../../state/useGraphStore';
import { useI18n } from '../../i18n';
import { 
  Search, Play, StepForward, RotateCcw, Sparkles, 
  Cpu, Sun, Moon, Film, Terminal, X, CornerDownLeft 
} from 'lucide-react';

export const CommandPalette: React.FC = () => {
  const { lang } = useI18n();
  const { 
    isCommandPaletteOpen, setCommandPaletteOpen, 
    resetLayout, toggleTheme, theme, setFilmstripOpen, filmstrip, 
    clearLogs, setWorkspace 
  } = useDockStore();
  const { filterDefs, addFilterNode, runOnce } = useGraphStore();

  const [query, setQuery] = useState('');
  const [selectedIndex, setSelectedIndex] = useState(0);
  const inputRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    if (isCommandPaletteOpen) {
      setQuery('');
      setSelectedIndex(0);
      setTimeout(() => inputRef.current?.focus(), 50);
    }
  }, [isCommandPaletteOpen]);

  // Command items
  const commands = [
    {
      id: 'cmd_run_once',
      category: 'Thực Thi',
      label: 'Run Once (Chạy 1 Lần DAG)',
      shortcut: 'F5',
      icon: Play,
      action: () => runOnce(),
    },
    {
      id: 'cmd_reset_layout',
      category: 'Bố Cục',
      label: 'Reset Layout (Khôi phục bố cục mặc định)',
      shortcut: 'Alt+R',
      icon: RotateCcw,
      action: () => resetLayout(),
    },
    {
      id: 'cmd_ws_tuning',
      category: 'Bố Cục',
      label: 'Workspace: Inspection & Tuning',
      shortcut: 'Alt+1',
      icon: RotateCcw,
      action: () => setWorkspace('Inspection & Tuning'),
    },
    {
      id: 'cmd_ws_engineering',
      category: 'Bố Cục',
      label: 'Workspace: Graph Engineering',
      shortcut: 'Alt+2',
      icon: RotateCcw,
      action: () => setWorkspace('Graph Engineering'),
    },
    {
      id: 'cmd_toggle_theme',
      category: 'Giao Diện',
      label: `Chuyển sang giao diện ${theme === 'dark' ? 'Studio Light' : 'Obsidian Dark'}`,
      shortcut: 'Ctrl+T',
      icon: theme === 'dark' ? Sun : Moon,
      action: () => toggleTheme(),
    },
    {
      id: 'cmd_toggle_filmstrip',
      category: 'Giao Diện',
      label: 'Bật / Tắt Băng Hình Ảnh (Filmstrip)',
      shortcut: 'Ctrl+F',
      icon: Film,
      action: () => setFilmstripOpen(!filmstrip.isOpen),
    },
    {
      id: 'cmd_clear_logs',
      category: 'Hệ Thống',
      label: 'Xóa Toàn Bộ Nhật Ký Console',
      shortcut: '',
      icon: Terminal,
      action: () => clearLogs(),
    },
    // Map all 25 filters into quick-add commands
    ...Object.values(filterDefs).map((f) => ({
      id: `filter_${f.filter_type}`,
      category: `Bộ Lọc: ${f.category}`,
      label: `Thêm Bộ Lọc: ${f.label} (${f.filter_type})`,
      shortcut: '+ Node',
      icon: Sparkles,
      action: () => addFilterNode(f.filter_type),
    })),
  ];

  const filteredCommands = commands.filter(
    (c) =>
      c.label.toLowerCase().includes(query.toLowerCase()) ||
      c.category.toLowerCase().includes(query.toLowerCase())
  );

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Escape') {
      setCommandPaletteOpen(false);
    } else if (e.key === 'ArrowDown') {
      e.preventDefault();
      setSelectedIndex((prev) => (prev + 1) % Math.max(1, filteredCommands.length));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setSelectedIndex((prev) => (prev - 1 + filteredCommands.length) % Math.max(1, filteredCommands.length));
    } else if (e.key === 'Enter') {
      e.preventDefault();
      const selected = filteredCommands[selectedIndex];
      if (selected) {
        selected.action();
        setCommandPaletteOpen(false);
      }
    }
  };

  if (!isCommandPaletteOpen) return null;

  return (
    <div 
      className="fixed inset-0 z-50 bg-black/70 backdrop-blur-sm flex items-start justify-center pt-24 select-none"
      onClick={() => setCommandPaletteOpen(false)}
    >
      <div 
        className="w-[580px] rounded-lg bg-[#14151b] border border-[#00f0ff]/40 shadow-[0_0_40px_rgba(0,0,0,0.8)] overflow-hidden flex flex-col"
        onClick={(e) => e.stopPropagation()}
        onKeyDown={handleKeyDown}
      >
        {/* Search Input Bar */}
        <div className="p-3 border-b border-[#252630] flex items-center gap-3 bg-[#181922]">
          <Search className="w-4 h-4 text-[#00f0ff]" />
          <input
            ref={inputRef}
            type="text"
            value={query}
            onChange={(e) => {
              setQuery(e.target.value);
              setSelectedIndex(0);
            }}
            placeholder={lang === 'vi' ? 'Tìm bộ lọc hoặc gõ lệnh IDE (F5, Otsu, SERT, Layout)...' : 'Search filters or type IDE command...'}
            className="flex-1 bg-transparent text-sm text-white placeholder-[#71717a] outline-none font-sans"
          />
          <span className="text-[10px] text-[#71717a] font-mono px-1.5 py-0.5 rounded bg-[#252630]">
            ESC để đóng
          </span>
        </div>

        {/* Results List */}
        <div className="max-h-[380px] overflow-y-auto p-1.5 space-y-0.5">
          {filteredCommands.length === 0 ? (
            <div className="p-6 text-center text-[#71717a] text-xs">
              Không tìm thấy lệnh hoặc bộ lọc nào phù hợp.
            </div>
          ) : (
            filteredCommands.map((item, idx) => {
              const isSelected = idx === selectedIndex;
              const Icon = item.icon;

              return (
                <div
                  key={item.id}
                  onClick={() => {
                    item.action();
                    setCommandPaletteOpen(false);
                  }}
                  onMouseEnter={() => setSelectedIndex(idx)}
                  className={`px-3 py-2 rounded flex items-center justify-between cursor-pointer transition-all ${
                    isSelected
                      ? 'bg-[#00f0ff]/15 text-[#00f0ff] font-semibold border border-[#00f0ff]/30'
                      : 'text-[#e4e4e7] hover:bg-[#1a1b23]'
                  }`}
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <Icon className={`w-3.5 h-3.5 shrink-0 ${isSelected ? 'text-[#00f0ff]' : 'text-[#71717a]'}`} />
                    <div className="flex flex-col truncate">
                      <span className="text-xs truncate">{item.label}</span>
                      <span className="text-[9.5px] text-[#71717a] uppercase font-mono">{item.category}</span>
                    </div>
                  </div>

                  {item.shortcut && (
                    <span className="text-[10px] text-[#71717a] font-mono px-1.5 py-0.5 rounded bg-[#1e2029] shrink-0 ml-2">
                      {item.shortcut}
                    </span>
                  )}
                </div>
              );
            })
          )}
        </div>

        {/* Footer Hint */}
        <div className="px-3 py-1.5 bg-[#101014] border-t border-[#252630] flex items-center justify-between text-[10px] text-[#71717a]">
          <span>Điều hướng: Phím ↑ ↓ • Chọn: Enter</span>
          <span className="text-[#00f0ff] font-bold">OptiFlow Command Palette (Ctrl+P)</span>
        </div>
      </div>
    </div>
  );
};
