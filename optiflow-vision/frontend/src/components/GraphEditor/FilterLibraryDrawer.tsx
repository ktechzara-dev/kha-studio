import React, { useState, useMemo } from 'react';
import { useI18n } from '../../i18n';
import { useGraphStore, FilterDef } from '../../state/useGraphStore';
import { 
  Search, Plus, Sliders, Target, Ruler, Cpu, Layers, 
  Camera, Radio, ChevronRight, ChevronDown, Sparkles 
} from 'lucide-react';

const CATEGORY_ICONS: Record<string, any> = {
  Processing: Sliders,
  Analysis: Target,
  Measurement: Ruler,
  Logic: Cpu,
  Overlays: Layers,
  Acquisition: Camera,
  Communication: Radio,
};

const CATEGORY_NAMES_VI: Record<string, string> = {
  Processing: 'Xử Lý Ảnh (Processing)',
  Analysis: 'Phân Tích Vật Thể (Analysis)',
  Measurement: 'Đo Lường Kỹ Thuật (Measurement)',
  Logic: 'Logic & Phán Đoán (Logic)',
  Overlays: 'Đồ Họa & Vẽ HUD (Overlays)',
  Acquisition: 'Thu Nhận Ảnh (Acquisition)',
  Communication: 'Truyền Thông PLC (Communication)',
};

export const FilterLibraryDrawer: React.FC = () => {
  const { lang } = useI18n();
  const { filterDefs, addFilterNode } = useGraphStore();

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedCategory, setSelectedCategory] = useState<string | null>(null);
  const [collapsedCategories, setCollapsedCategories] = useState<Record<string, boolean>>({});

  const filterList = useMemo(() => Object.values(filterDefs), [filterDefs]);

  // Group by category
  const categories = useMemo(() => {
    const map: Record<string, FilterDef[]> = {};
    for (const f of filterList) {
      if (!map[f.category]) map[f.category] = [];
      map[f.category].push(f);
    }
    return map;
  }, [filterList]);

  // Filter by search query
  const filteredCategories = useMemo(() => {
    const q = searchQuery.toLowerCase().trim();
    if (!q && !selectedCategory) return categories;

    const result: Record<string, FilterDef[]> = {};
    for (const [cat, list] of Object.entries(categories)) {
      if (selectedCategory && cat !== selectedCategory) continue;
      const matched = list.filter(
        (f) =>
          f.label.toLowerCase().includes(q) ||
          f.filter_type.toLowerCase().includes(q) ||
          f.description.toLowerCase().includes(q)
      );
      if (matched.length > 0) {
        result[cat] = matched;
      }
    }
    return result;
  }, [categories, searchQuery, selectedCategory]);

  const toggleCategory = (cat: string) => {
    setCollapsedCategories((prev) => ({ ...prev, [cat]: !prev[cat] }));
  };

  return (
    <div className="w-full bg-[#131418] flex flex-col h-full select-none z-10">
      {/* Search Header */}
      <div className="p-3 border-b border-[#26262c] flex flex-col gap-2">
        <div className="flex items-center justify-between">
          <span className="font-bold text-xs text-[#00f0ff] uppercase tracking-wider flex items-center gap-1.5">
            <Sparkles className="w-3.5 h-3.5" />
            <span>Thư Viện Bộ Lọc (25)</span>
          </span>
          <span className="text-[10px] text-[#71717a] font-mono">
            {filterList.length} Filters
          </span>
        </div>

        {/* Search input */}
        <div className="relative">
          <Search className="w-3.5 h-3.5 absolute left-2.5 top-2.5 text-[#71717a]" />
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => setSearchQuery(e.target.value)}
            placeholder={lang === 'vi' ? 'Tìm bộ lọc (Otsu, Blob, SERT...)' : 'Search filters...'}
            className="w-full bg-[#101013] border border-[#26262c] focus:border-[#00f0ff] rounded pl-8 pr-3 py-1.5 text-xs text-white placeholder-[#71717a] outline-none"
          />
        </div>

        {/* Quick Category Filter Pills */}
        <div className="flex items-center gap-1 overflow-x-auto py-1 scrollbar-none">
          <button
            onClick={() => setSelectedCategory(null)}
            className={`px-2 py-0.5 rounded text-[10px] font-semibold whitespace-nowrap transition-colors ${
              selectedCategory === null
                ? 'bg-[#00f0ff]/20 text-[#00f0ff] border border-[#00f0ff]/40'
                : 'bg-[#1b1b1f] text-[#71717a] hover:text-white'
            }`}
          >
            Tất Cả
          </button>
          {Object.keys(categories).map((cat) => (
            <button
              key={cat}
              onClick={() => setSelectedCategory(selectedCategory === cat ? null : cat)}
              className={`px-2 py-0.5 rounded text-[10px] font-semibold whitespace-nowrap transition-colors ${
                selectedCategory === cat
                  ? 'bg-[#00f0ff]/20 text-[#00f0ff] border border-[#00f0ff]/40'
                  : 'bg-[#1b1b1f] text-[#71717a] hover:text-white'
              }`}
            >
              {cat}
            </button>
          ))}
        </div>
      </div>

      {/* Filter Accordion List */}
      <div className="flex-1 overflow-y-auto p-2 space-y-2">
        {Object.entries(filteredCategories).map(([cat, list]) => {
          const isCollapsed = Boolean(collapsedCategories[cat]);
          const Icon = CATEGORY_ICONS[cat] || Sliders;
          const catTitle = lang === 'vi' ? (CATEGORY_NAMES_VI[cat] || cat) : cat;

          return (
            <div key={cat} className="rounded border border-[#26262c] bg-[#101013] overflow-hidden">
              {/* Category Header */}
              <button
                onClick={() => toggleCategory(cat)}
                className="w-full px-2.5 py-1.5 bg-[#18181b] hover:bg-[#1f1f24] flex items-center justify-between text-left transition-colors"
              >
                <div className="flex items-center gap-2">
                  <Icon className="w-3.5 h-3.5 text-[#00f0ff]" />
                  <span className="font-bold text-[11px] text-[#e4e4e7]">{catTitle}</span>
                </div>
                <div className="flex items-center gap-1.5">
                  <span className="text-[10px] text-[#71717a] font-mono px-1.5 py-0.2 rounded bg-[#26262c]">
                    {list.length}
                  </span>
                  {isCollapsed ? (
                    <ChevronRight className="w-3.5 h-3.5 text-[#71717a]" />
                  ) : (
                    <ChevronDown className="w-3.5 h-3.5 text-[#71717a]" />
                  )}
                </div>
              </button>

              {/* Items List */}
              {!isCollapsed && (
                <div className="p-1 space-y-1">
                  {list.map((f) => (
                    <div
                      key={f.filter_type}
                      className="group p-2 rounded bg-[#141417] hover:bg-[#1f1f24] border border-transparent hover:border-[#3f3f46] flex items-start justify-between gap-2 transition-all"
                    >
                      <div className="flex flex-col flex-1 min-w-0">
                        <span className="font-semibold text-[11.5px] text-[#f4f4f7] truncate group-hover:text-[#00f0ff]">
                          {f.label}
                        </span>
                        <span className="text-[10px] text-[#71717a] line-clamp-1">
                          {f.description}
                        </span>
                        <div className="flex items-center gap-2 mt-1 text-[9.5px] text-[#a1a1aa] font-mono">
                          <span>In: {f.inputs.length}</span>
                          <span>•</span>
                          <span>Out: {f.outputs.length}</span>
                        </div>
                      </div>

                      {/* Add Button */}
                      <button
                        onClick={() => addFilterNode(f.filter_type)}
                        className="p-1.5 rounded bg-[#1b1b1f] hover:bg-[#0284c7] hover:text-white text-[#00f0ff] border border-[#26262c] transition-all shrink-0 mt-0.5"
                        title="Thêm bộ lọc vào đồ thị"
                      >
                        <Plus className="w-3.5 h-3.5" />
                      </button>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
