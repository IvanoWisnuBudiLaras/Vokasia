"use client";

import { useEffect, useState } from "react";

/**
 * Custom hook untuk meng-cache state form ke localStorage secara otomatis.
 * Mencegah data hilang saat berpindah halaman, koneksi terputus, atau token expired di tengah pengisian.
 * 
 * HANYA untuk form Create/Update/Post (bukan form filter/search).
 */
export function useFormDraft<T extends Record<string, any>>(
  storageKey: string,
  initialValues: T
) {
  const [values, setValues] = useState<T>(initialValues);
  const [isLoaded, setIsLoaded] = useState(false);

  // 1. Load dari localStorage saat mount
  useEffect(() => {
    if (typeof window === "undefined" || !storageKey) return;

    try {
      const saved = localStorage.getItem(`vokasia_draft_${storageKey}`);
      if (saved) {
        const parsed = JSON.parse(saved) as Partial<T>;
        setValues((prev) => ({ ...prev, ...parsed }));
      }
    } catch {
      // Abaikan error parse localStorage
    } finally {
      setIsLoaded(true);
    }
  }, [storageKey]);

  // 2. Simpan ke localStorage saat values berubah
  useEffect(() => {
    if (!isLoaded || typeof window === "undefined" || !storageKey) return;

    try {
      localStorage.setItem(`vokasia_draft_${storageKey}`, JSON.stringify(values));
    } catch {
      // Abaikan error kuota localStorage
    }
  }, [storageKey, values, isLoaded]);

  // 3. Clear draft saat submit sukses
  const clearDraft = () => {
    if (typeof window === "undefined" || !storageKey) return;
    try {
      localStorage.removeItem(`vokasia_draft_${storageKey}`);
    } catch {
      // Abaikan error
    }
    setValues(initialValues);
  };

  const updateField = <K extends keyof T>(field: K, value: T[K]) => {
    setValues((prev) => ({ ...prev, [field]: value }));
  };

  return {
    values,
    setValues,
    updateField,
    clearDraft,
    isLoaded,
  };
}
