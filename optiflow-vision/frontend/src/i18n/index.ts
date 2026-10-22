import { create } from 'zustand';
import en from './en.json';
import vi from './vi.json';

export type Language = 'en' | 'vi';

interface I18nState {
  lang: Language;
  setLanguage: (lang: Language) => void;
  t: (key: string) => string;
}

const dictionaries: Record<Language, any> = { en, vi };

export const useI18n = create<I18nState>((set, get) => ({
  lang: 'vi', // Default to Vietnamese for sếp Kha
  setLanguage: (lang: Language) => set({ lang }),
  t: (key: string): string => {
    const { lang } = get();
    const parts = key.split('.');
    let curr = dictionaries[lang];
    for (const p of parts) {
      if (curr && typeof curr === 'object' && p in curr) {
        curr = curr[p];
      } else {
        // Fallback to English
        let fallback = dictionaries['en'];
        for (const fp of parts) {
          if (fallback && typeof fallback === 'object' && fp in fallback) {
            fallback = fallback[fp];
          } else {
            return key;
          }
        }
        return fallback;
      }
    }
    return typeof curr === 'string' ? curr : key;
  },
}));
