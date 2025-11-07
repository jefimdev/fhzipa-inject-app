// Prevents additional console window on Windows in release, DO NOT REMOVE!!
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    #[cfg(target_os = "linux")]
    {
        use std::env;
        if env::var_os("WEBKIT_DISABLE_DMABUF_RENDERER").is_none() {
            env::set_var("WEBKIT_DISABLE_DMABUF_RENDERER", "1");
        }

        if env::var_os("GDK_BACKEND").is_none() {
            if env::var_os("WAYLAND_DISPLAY").is_some() {
                env::set_var("GDK_BACKEND", "wayland");
            } else if env::var_os("DISPLAY").is_some() {
                env::set_var("GDK_BACKEND", "x11");
            }
        }
    }

    iloader_lib::run()
}
