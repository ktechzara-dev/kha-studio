import React from 'react';
import { useDockStore, ProgramTab } from '../../state/useDockStore';
import { useI18n } from '../../i18n';
import { 
  GitBranch, Box, Plus, X, Sparkles, LayoutGrid, Zap 
} from 'lucide-react';

export const ProgramTabBar: React.FC = () => {
  const { lang } = useI18n();
  const { tabs, activeTabId, setActiveTabId, closeTab, addTab } = useDockStore();

  const handleAddNewMacro = () => {
    const name = prompt(
      lang === 'vi' ? 'Nhập tên Macro mới:' : 'Enter new Macro name:',
      `Macro_${tabs.length}`
    );
    if (name && name.trim()) {
      addTab(name.trim(), 'MACRO');
    }
  };

  return (
    <div className="h-8 bg-[#101014] border-b border-[#252630] flex items-center justify-between px-2 text-xs select-none z-10 shrink-0">
      {/* Tabs List */}
      <div className="flex items-center gap-1 overflow-x-auto scrollbar-none h-full pt-1">
        {tabs.map((tab) => {
          const isActive = tab.id === activeTabId;
          const isMain = tab.type === 'MAIN';

          return (
            <div
              key={tab.id}
              onClick={() => setActiveTabId(tab.id)}
              className={`group h-7 px-3 rounded-t flex items-center gap-2 cursor-pointer transition-all border-t-2 ${
                isActive
                  ? 'bg-[#181920] border-[#00f0ff] text-[#f4f4f7] font-semibold shadow-md'
                  : 'bg-[#121317] border-transparent text-[#9ca3af] hover:bg-[#16171d] hover:text-white'
              }`}
            >
              {isMain ? (
                <GitBranch className={`w-3.5 h-3.5 ${isActive ? 'text-[#00f0ff]' : 'text-[#71717a]'}`} />
              ) : (
                <Box className={`w-3.5 h-3.5 ${isActive ? 'text-[#f59e0b]' : 'text-[#71717a]'}`} />
              )}

              <span className="truncate max-w-[150px] text-[11px]">{tab.name}</span>

              {/* Close Tab Button (allow closing macros) */}
              {!isMain && tabs.length > 1 && (
                <button
                  onClick={(e) => {
                    e.stopPropagation();
                    closeTab(tab.id);
                  }}
                  className="p-0.5 rounded hover:bg-[#ef4444]/20 hover:text-[#ef4444] text-[#71717a] transition-colors ml-1"
                  title="Close Tab"
                >
                  <X className="w-3 h-3" />
                </button>
              )}
            </div>
          );
        })}

        {/* Add Macro Tab Button */}
        <button
          onClick={handleAddNewMacro}
          className="h-6 px-2 rounded hover:bg-[#1e2028] text-[#71717a] hover:text-[#00f0ff] flex items-center gap-1 transition-all text-[11px]"
          title={lang === 'vi' ? 'Tạo Macro / Chương Trình Con Mới' : 'Add Macro Tab'}
        >
          <Plus className="w-3.5 h-3.5" />
          <span className="hidden sm:inline">Macro</span>
        </button>
      </div>

      {/* Program Flow Hint */}
      <div className="flex items-center gap-2 text-[10px] text-[#71717a] font-mono pr-2">
        <span className="w-1.5 h-1.5 rounded-full bg-[#10b981]" />
        <span>Topological Flow Engine</span>
      </div>
    </div>
  );
};
