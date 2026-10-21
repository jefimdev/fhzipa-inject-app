use std::path::PathBuf;
use std::sync::Mutex;

use crate::{
    device::{DeviceInfoMutex, get_provider},
    error::AppError,
    ipa,
    operation::Operation,
};
use isideload::{
    sideload::{
        application::{Application, SpecialApp},
        sideloader::Sideloader,
    },
    util::callbacks::{ExtensionsBehaviorCallbackBox, MaxCertsCallbackBox},
};
use tauri::{State, Window};

pub type SideloaderMutex =
    Mutex<Option<Sideloader<MaxCertsCallbackBox, ExtensionsBehaviorCallbackBox>>>;

pub struct SideloaderGuard<'a> {
    state: &'a SideloaderMutex,
    sideloader: Option<Sideloader<MaxCertsCallbackBox, ExtensionsBehaviorCallbackBox>>,
}

impl<'a> SideloaderGuard<'a> {
    pub fn take(state: &'a SideloaderMutex) -> Result<Self, AppError> {
        let mut guard = state.lock().unwrap();
        let sideloader = guard.take().ok_or(AppError::NotLoggedIn)?;
        Ok(Self {
            state,
            sideloader: Some(sideloader),
        })
    }

    pub fn get_mut(
        &mut self,
    ) -> &mut Sideloader<MaxCertsCallbackBox, ExtensionsBehaviorCallbackBox> {
        self.sideloader
            .as_mut()
            .expect("Sideloader should be present")
    }
}

impl Drop for SideloaderGuard<'_> {
    fn drop(&mut self) {
        let mut guard = self.state.lock().unwrap();
        *guard = self.sideloader.take();
    }
}

/// Extracts the archive and repairs the bundle so that it can be signed.
///
/// The signing pipeline works on the extracted bundle, so the repaired copy is
/// the one that gets installed.
pub fn prepare(app_path: String) -> Result<PathBuf, AppError> {
    let app_path = PathBuf::from(app_path);
    let from_archive = app_path.is_file();

    let extracted = Application::new(app_path)
        .map_err(|e| AppError::Misc(format!("Failed to open the app bundle: {e}")))?;
    let app_dir = extracted.bundle.bundle_dir.clone();
    drop(extracted);

    if from_archive {
        ipa::remove_orphan_extensions(&app_dir)?;
    }

    Ok(app_dir)
}

pub async fn sideload(
    device_state: State<'_, DeviceInfoMutex>,
    sideloader_state: State<'_, SideloaderMutex>,
    app_dir: PathBuf,
) -> Result<Option<SpecialApp>, AppError> {
    let device = {
        let device_lock = device_state.lock().unwrap();
        match &*device_lock {
            Some(d) => d.clone(),
            None => return Err(AppError::NoDeviceSelected),
        }
    };

    let provider = get_provider(&device.info).await?;

    let mut sideloader = SideloaderGuard::take(&sideloader_state)?;

    let special = sideloader
        .get_mut()
        .install_app(
            &provider,
            app_dir,
            false,
            None::<fn(f32) -> std::future::Ready<()>>,
            Some(&device.pairing),
        )
        .await?;

    Ok(special)
}

#[tauri::command]
pub async fn sideload_operation(
    window: Window,
    device_state: State<'_, DeviceInfoMutex>,
    sideloader_state: State<'_, SideloaderMutex>,
    app_path: String,
) -> Result<(), AppError> {
    let op = Operation::new("sideload".to_string(), &window);

    // Fail before the archive is extracted, that part is expensive.
    if sideloader_state.lock().unwrap().is_none() {
        return Err(AppError::NotLoggedIn);
    }

    op.start("prepare")?;
    let app_dir = op.fail_if_err("prepare", prepare(app_path))?;
    op.complete("prepare")?;

    op.start("install")?;
    op.fail_if_err(
        "install",
        sideload(device_state, sideloader_state, app_dir).await,
    )?;
    op.complete("install")?;
    Ok(())
}
