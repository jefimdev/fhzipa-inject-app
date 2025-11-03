#[macro_use]
mod account;

use crate::account::{
    delete_account, invalidate_account, logged_in_as, login_email_pass, login_stored_pass,
};

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_opener::init())
        .plugin(tauri_plugin_store::Builder::new().build())
        .invoke_handler(tauri::generate_handler![
            login_email_pass,
            invalidate_account,
            logged_in_as,
            login_stored_pass,
            delete_account
        ])
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
