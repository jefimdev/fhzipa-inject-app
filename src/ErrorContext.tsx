import React, {
    createContext,
    useContext,
    useState,
} from "react";
import { Modal } from "./components/Modal";
import "./ErrorContext.css";
import { toast } from "sonner";

export const ErrorContext = createContext<{
    err: (msg: string, err: string | null) => string;
}>({ err: () => "" });

export const ErrorProvider: React.FC<{ children: React.ReactNode }> = ({
    children,
}) => {
    const [msg, setMsg] = useState<string | null>(null);
    const [error, setError] = useState<string | null>(null);

    return (
        <ErrorContext.Provider value={{
            err: (msg: string, err: string | null) => {
                setMsg(msg);
                setError(err);
                return msg;
            }
        }}>
            <Modal forceTop isOpen={error !== null || msg !== null} close={() => {
                setError(null);
                setMsg(null);
            }}>
                <div className="error-outer">
                    <div className="log-header">
                        <h2>An Error Occured: {msg ?? "Unknown"}</h2>
                        <button onClick={() => {
                            navigator.clipboard.writeText(error?.replace(/^\n+/, "") ?? "No error");
                            toast.success("Logs copied to clipboard");
                        }}>Copy to clipboard</button>
                    </div>
                    <pre className="error-inner">
                        {error?.replace(/^\n+/, "")}
                    </pre>
                    <button onClick={() => {
                        setError(null);
                        setMsg(null);
                    }}>
                        Dismiss
                    </button>
                </div>
            </Modal>
            {children}
        </ErrorContext.Provider>
    );
};

export const useError = () => {
    return useContext(ErrorContext);
};