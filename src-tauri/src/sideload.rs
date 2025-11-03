use crate::{
    account::get_developer_session,
    device::{get_provider, DeviceInfoMutex},
    operation::Operation,
};
use isideload::{sideload::sideload_app, SideloadConfiguration};
use tauri::{AppHandle, Manager, State, Window};

#[tauri::command]
pub async fn sideload(
    handle: AppHandle,
    device_state: State<'_, DeviceInfoMutex>,
    app_path: String,
) -> Result<(), String> {
    let device = {
        let device_lock = device_state.lock().unwrap();
        match &*device_lock {
            Some(d) => d.clone(),
            None => return Err("No device selected".to_string()),
        }
    };

    let provider = get_provider(&device).await?;

    let config = SideloadConfiguration::default()
        .set_machine_name("iloader".to_string())
        .set_store_dir(
            handle
                .path()
                .app_data_dir()
                .map_err(|e| format!("Failed to get app data dir: {:?}", e))?,
        );

    let dev_session = get_developer_session().await.map_err(|e| e.to_string())?;

    sideload_app(&provider, &dev_session, app_path.into(), config)
        .await
        .map_err(|e| format!("Failed to sideload app: {:?}", e))
}

#[tauri::command]
pub async fn install_sidestore_operation(
    handle: AppHandle,
    window: Window,
    device_state: State<'_, DeviceInfoMutex>,
) -> Result<(), String> {
    let op = Operation::new("install_sidestore".to_string(), &window);
    Ok(())
}
