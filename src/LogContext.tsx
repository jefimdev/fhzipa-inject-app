import React, {
  createContext,
  useContext,
  useState,
  useEffect,
  useRef,
} from "react";

import { attachLogger, RecordPayload } from '@fltsci/tauri-plugin-tracing';

export const LogContext = createContext<RecordPayload[]>([]);

export const LogProvider: React.FC<{ children: React.ReactNode }> = ({
  children,
}) => {
  const [logs, setLogs] = useState<RecordPayload[]>([]);
  const listenerAdded = useRef<boolean>(false);
  let unlistenRef = useRef<(() => void) | null>(null);

  useEffect(() => {
    if (!listenerAdded.current) {
      const setupLogger = async () => {
        listenerAdded.current = true;
        unlistenRef.current = await attachLogger((record) => {
          console.log(record)
          setLogs((prevLogs) => [...prevLogs, record]);
        });
      };

      setupLogger();
    }

    return () => {
      if (unlistenRef.current) {
        unlistenRef.current();
      }
    };
  }, []);

  return (
    <LogContext.Provider value={logs}>
      {children}
    </LogContext.Provider>
  );
};

export const useLogs = () => {
  return useContext(LogContext);
};