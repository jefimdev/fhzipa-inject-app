use std::sync::OnceLock;

use isideload::util::{
    fs_storage::FsStorage, keyring_storage::KeyringStorage, storage::SideloadingStorage,
};
use tauri::{AppHandle, Manager};
use tracing::warn;

static KEYRING_AVAILABLE: OnceLock<bool> = OnceLock::new();

#[tauri::command]
pub fn force_disable_keyring() {
    KEYRING_AVAILABLE.set(false).ok();
}

#[tauri::command]
pub fn keyring_available() -> bool {
    *KEYRING_AVAILABLE.get_or_init(check_keyring_available)
}

fn check_keyring_available() -> bool {
    let entry = keyring::Entry::new("iloader", "test");
    if let Ok(entry) = entry {
        return entry.set_password("test").is_ok() && entry.get_password().is_ok();
    }
    false
}

pub fn create_sideloading_storage(app: &AppHandle) -> Result<Box<dyn SideloadingStorage>, String> {
    if keyring_available() {
        Ok(Box::new(KeyringStorage::new("iloader".to_string())))
    } else {
        warn!(
            "Keyring is not available, falling back to filesystem storage for sideloading data. This is insecure!"
        );
        Ok(Box::new(FsStorage::new(
            app.path()
                .app_data_dir()
                .map_err(|e| format!("Failed to get app data directory: {:?}", e))?,
        )))
    }
}
