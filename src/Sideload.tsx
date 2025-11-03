import { invoke } from "@tauri-apps/api/core";
import { open } from "@tauri-apps/plugin-dialog";
import { toast } from "sonner";

export const Sideload = () => {
  return (
    <>
      <h2>Sideload .IPA</h2>
      <div>
        <button
          onClick={async () => {
            let path = await open({
              multiple: false,
              filters: [{ name: "IPA Files", extensions: ["ipa"] }],
            });
            if (!path) return;
            toast.promise(invoke("sideload", { appPath: path as string }), {
              loading: "Installing...",
              success: "App installed successfully!",
              error: (e) => {
                console.error(e);
                return e;
              },
            });
          }}
        >
          Install
        </button>
      </div>
    </>
  );
};
