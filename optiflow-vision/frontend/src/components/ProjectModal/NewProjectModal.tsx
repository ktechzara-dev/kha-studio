import React, { useState } from 'react';
import { useI18n } from '../../i18n';
import { useProjectStore } from '../../state/useProjectStore';
import { FolderPlus, X } from 'lucide-react';

export const NewProjectModal: React.FC = () => {
  const { t, lang } = useI18n();
  const { newProjectModalOpen, setNewProjectModalOpen, createNewProject } = useProjectStore();

  const [name, setName] = useState('Packaging_Line_01');
  const [author, setAuthor] = useState('Kha Studio Automation');
  const [fps, setFps] = useState('30');

  if (!newProjectModalOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (name.trim()) {
      await createNewProject(name.trim(), lang);
      setNewProjectModalOpen(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="w-full max-w-md hud-panel p-5 text-xs text-[#e4e4e7] animate-in fade-in zoom-in-95 duration-150">
        {/* Header */}
        <div className="flex items-center justify-between pb-3 mb-4 border-b border-[#26262c]">
          <div className="flex items-center gap-2 text-sm font-bold text-[#00f0ff]">
            <FolderPlus className="w-4 h-4" />
            <span>{t('project.new_title')}</span>
          </div>
          <button 
            onClick={() => setNewProjectModalOpen(false)}
            className="text-[#71717a] hover:text-white p-1 rounded"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="space-y-4">
          <div>
            <label className="block text-[#a1a1aa] font-semibold mb-1">
              {t('project.project_name')}
            </label>
            <input
              type="text"
              value={name}
              onChange={(e) => setName(e.target.value)}
              className="w-full bg-[#101013] border border-[#26262c] focus:border-[#00f0ff] rounded px-3 py-2 text-white font-mono outline-none"
              placeholder="MyInspectionProject"
              required
            />
          </div>

          <div>
            <label className="block text-[#a1a1aa] font-semibold mb-1">
              {t('project.author')}
            </label>
            <input
              type="text"
              value={author}
              onChange={(e) => setAuthor(e.target.value)}
              className="w-full bg-[#101013] border border-[#26262c] focus:border-[#00f0ff] rounded px-3 py-2 text-white outline-none"
            />
          </div>

          <div>
            <label className="block text-[#a1a1aa] font-semibold mb-1">
              {t('project.target_fps')}
            </label>
            <input
              type="number"
              min="1"
              max="200"
              value={fps}
              onChange={(e) => setFps(e.target.value)}
              className="w-full bg-[#101013] border border-[#26262c] focus:border-[#00f0ff] rounded px-3 py-2 text-white font-mono outline-none"
            />
          </div>

          <div className="flex items-center justify-end gap-2 pt-3 border-t border-[#26262c]">
            <button
              type="button"
              onClick={() => setNewProjectModalOpen(false)}
              className="px-4 py-2 rounded bg-[#1b1b1f] hover:bg-[#26262c] text-[#a1a1aa] font-semibold"
            >
              {t('project.cancel_button')}
            </button>
            <button
              type="submit"
              className="px-4 py-2 rounded bg-[#0284c7] hover:bg-[#0369a1] text-white font-bold border border-[#00f0ff]"
            >
              {t('project.create_button')}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
