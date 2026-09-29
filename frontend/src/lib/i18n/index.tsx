"use client";

import { createContext, useContext, useEffect, useMemo, useSyncExternalStore, type ReactNode } from "react";
import type { TranslationKey } from "./tr";
import {
  defaultLocale,
  isLocale,
  translate,
  translateErrorCode,
  type Locale,
  type TranslationParams,
} from "./translate";

export type { TranslationKey } from "./tr";
export { locales, type Locale } from "./translate";

const storageKey = "akiron.locale";

/**
 * The chosen language lives in localStorage, an external store; reading it through
 * useSyncExternalStore renders Turkish on the server and switches after hydration without an
 * effect-driven second render. The in-memory value covers browsers that block storage.
 */
const localeStore = (() => {
  const listeners = new Set<() => void>();
  let memory: Locale | null = null;

  return {
    subscribe(listener: () => void) {
      listeners.add(listener);
      window.addEventListener("storage", listener);
      return () => {
        listeners.delete(listener);
        window.removeEventListener("storage", listener);
      };
    },
    getSnapshot(): Locale {
      if (memory) return memory;
      try {
        const stored = window.localStorage.getItem(storageKey);
        return isLocale(stored) ? stored : defaultLocale;
      } catch {
        return defaultLocale;
      }
    },
    getServerSnapshot(): Locale {
      return defaultLocale;
    },
    set(locale: Locale) {
      memory = locale;
      try {
        window.localStorage.setItem(storageKey, locale);
      } catch {
        // Not persisted; the choice still applies for this visit.
      }
      listeners.forEach((listener) => listener());
    },
  };
})();

interface I18nContextValue {
  locale: Locale;
  setLocale: (locale: Locale) => void;
  t: (key: TranslationKey, params?: TranslationParams) => string;
  tError: (code: string | undefined, params?: TranslationParams) => string;
}

const I18nContext = createContext<I18nContextValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
  const locale = useSyncExternalStore(localeStore.subscribe, localeStore.getSnapshot, localeStore.getServerSnapshot);

  useEffect(() => {
    document.documentElement.lang = locale;
  }, [locale]);

  const value = useMemo<I18nContextValue>(
    () => ({
      locale,
      setLocale: localeStore.set,
      t: (key, params) => translate(locale, key, params),
      tError: (code, params) => translateErrorCode(locale, code, params),
    }),
    [locale],
  );

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nContextValue {
  const context = useContext(I18nContext);
  if (!context) throw new Error("useI18n must be used inside I18nProvider.");
  return context;
}
