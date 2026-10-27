import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import type { ReactNode } from 'react';
import { BUILD_EXPERIENCE_KEY, BUILD_SOUND_KEY, readBuildPreference, writeBuildPreference } from './build-experience';

const storage = { getItem: (key: string) => localStorage.getItem(key), setItem: (key: string, value: string) => localStorage.setItem(key, value) };
type BuildPreferences = { enabled: boolean; soundWanted: boolean; setEnabled: (value: boolean) => void; setSoundWanted: (value: boolean) => void };
const Context = createContext<BuildPreferences | null>(null);

export function BuildPreferencesProvider({ children }: { children: ReactNode }) {
 const [enabled, updateEnabled] = useState(() => readBuildPreference(storage));
 const [soundWanted, updateSound] = useState(() => readBuildPreference(storage, BUILD_SOUND_KEY));
 const setEnabled = useCallback((value: boolean) => { updateEnabled(value); writeBuildPreference(storage, value); }, []);
 const setSoundWanted = useCallback((value: boolean) => { updateSound(value); writeBuildPreference(storage, value, BUILD_SOUND_KEY); }, []);
 useEffect(() => {
  function changed(event: StorageEvent) {
   if (event.key === null || event.key === BUILD_EXPERIENCE_KEY) updateEnabled(readBuildPreference(storage));
   if (event.key === null || event.key === BUILD_SOUND_KEY) updateSound(readBuildPreference(storage, BUILD_SOUND_KEY));
  }
  window.addEventListener('storage', changed);
  return () => window.removeEventListener('storage', changed);
 }, []);
 const value = useMemo(() => ({ enabled, soundWanted, setEnabled, setSoundWanted }), [enabled, soundWanted, setEnabled, setSoundWanted]);
 return <Context.Provider value={value}>{children}</Context.Provider>;
}

export function useBuildPreferences() {
 const value = useContext(Context);
 if (!value) throw new Error('Build preferences require their provider.');
 return value;
}
