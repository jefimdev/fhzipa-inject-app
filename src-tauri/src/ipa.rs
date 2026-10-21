//! Pre-install repair of the extracted app bundle.
//!
//! The signing pipeline only knows about the app extensions that live in the
//! `PlugIns` directory of the app bundle. Some apps ship an extension
//! somewhere else instead, `Extensions/` being the usual alternative. iOS
//! still treats those bundles as app extensions and creates a placeholder for
//! them, but because they are never re-signed they keep the bundle identifier
//! of the original vendor. The install then fails with
//!
//! ```text
//! Failed to set app extension placeholders for <app>.<team>
//! Attempted to set app extension placeholder promise with bundle ID <id>
//! that does not match required prefix of <app>.<team>. for parent
//! ```
//!
//! Such an extension is signed by the original vendor and cannot run under a
//! different team anyway, so it is removed from the bundle before signing.

use std::fs::File;
use std::io::Read;
use std::path::{Path, PathBuf};

use serde::Serialize;
use tracing::{info, warn};
use zip::ZipArchive;

use crate::error::AppError;

/// The metadata an archive exposes about the application it carries.
#[derive(Serialize, Clone, Default, Debug)]
#[serde(rename_all = "camelCase")]
pub struct IpaInfo {
    pub display_name: Option<String>,
    pub bundle_id: Option<String>,
    pub version: Option<String>,
}

#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
pub struct IpaMetadata {
    pub size: u64,
    pub info: IpaInfo,
}

/// Reads the display name, the bundle identifier and the version of the
/// application stored in an `.ipa`, without extracting the whole archive.
#[tauri::command]
pub fn read_ipa_info(app_path: String) -> Result<IpaMetadata, AppError> {
    let size = std::fs::metadata(&app_path).map(|m| m.len()).unwrap_or(0);

    let file = File::open(&app_path).map_err(|e| {
        AppError::Filesystem("Failed to open the archive".into(), e.to_string())
    })?;
    let mut archive = ZipArchive::new(file)
        .map_err(|e| AppError::Misc(format!("Failed to read the archive: {e}")))?;

    let names: Vec<String> = (0..archive.len())
        .filter_map(|i| {
            archive
                .by_index_raw(i)
                .ok()
                .map(|entry| entry.name().to_string())
        })
        .collect();

    // Payload/<App>.app/Info.plist
    let target = names.iter().position(|name| {
        let parts: Vec<&str> = name.split('/').collect();
        parts.len() == 3
            && parts[0] == "Payload"
            && parts[2] == "Info.plist"
            && parts[1].ends_with(".app")
    });

    let Some(target) = target else {
        return Ok(IpaMetadata {
            size,
            info: IpaInfo::default(),
        });
    };

    let mut contents = Vec::new();
    archive
        .by_index(target)
        .map_err(|e| AppError::Misc(format!("Failed to read the archive: {e}")))?
        .read_to_end(&mut contents)
        .map_err(|e| AppError::Misc(format!("Failed to read the archive: {e}")))?;

    let plist = match plist::from_bytes::<plist::Dictionary>(&contents) {
        Ok(plist) => plist,
        Err(e) => {
            warn!("Failed to parse the Info.plist of the archive: {e}");
            return Ok(IpaMetadata {
                size,
                info: IpaInfo::default(),
            });
        }
    };

    let string = |key: &str| {
        plist
            .get(key)
            .and_then(|value| value.as_string())
            .map(|value| value.to_string())
    };

    Ok(IpaMetadata {
        size,
        info: IpaInfo {
            display_name: string("CFBundleDisplayName").or_else(|| string("CFBundleName")),
            bundle_id: string("CFBundleIdentifier"),
            version: string("CFBundleShortVersionString"),
        },
    })
}

/// An app extension bundle that the signing pipeline would not pick up.
#[derive(Debug, Clone)]
pub struct OrphanExtension {
    pub name: String,
    pub bundle_id: String,
    pub path: PathBuf,
}

/// Finds every app extension bundle that lives outside of the app's `PlugIns`
/// directory and would therefore keep its original bundle identifier.
pub fn find_orphan_extensions(app_dir: &Path) -> Vec<OrphanExtension> {
    let plugins_dir = app_dir.join("PlugIns");
    let mut candidates = Vec::new();
    collect_appex_dirs(app_dir, &mut candidates);

    candidates
        .into_iter()
        // A bundle directly inside `PlugIns` is handled by the signing
        // pipeline: its identifier gets rewritten and it gets signed.
        .filter(|dir| dir.parent() != Some(plugins_dir.as_path()))
        .filter_map(|dir| {
            let info = read_bundle_info(&dir)?;
            Some(OrphanExtension {
                name: info.name,
                bundle_id: info.bundle_id,
                path: dir,
            })
        })
        .collect()
}

/// Removes the orphaned extensions from the bundle and returns what was
/// removed.
pub fn remove_orphan_extensions(app_dir: &Path) -> Result<Vec<OrphanExtension>, AppError> {
    let orphans = find_orphan_extensions(app_dir);

    for orphan in &orphans {
        warn!(
            "Removing app extension '{}' ({}) from '{}': it is not inside the PlugIns directory, so it would keep the original vendor's bundle identifier and make the install fail",
            orphan.name,
            orphan.bundle_id,
            orphan.path.display(),
        );

        std::fs::remove_dir_all(&orphan.path).map_err(|e| {
            AppError::Filesystem(
                format!("Failed to remove the app extension '{}'", orphan.name),
                e.to_string(),
            )
        })?;

        info!("Removed the app extension '{}'", orphan.name);
    }

    Ok(orphans)
}

/// Collects every `*.appex` directory inside `dir`, at any depth.
fn collect_appex_dirs(dir: &Path, out: &mut Vec<PathBuf>) {
    let Ok(entries) = std::fs::read_dir(dir) else {
        return;
    };

    for entry in entries.flatten() {
        let path = entry.path();
        if !path.is_dir() {
            continue;
        }

        if path
            .extension()
            .map(|ext| ext.eq_ignore_ascii_case("appex"))
            .unwrap_or(false)
        {
            out.push(path.clone());
        }

        collect_appex_dirs(&path, out);
    }
}

struct BundleInfo {
    name: String,
    bundle_id: String,
}

fn read_bundle_info(dir: &Path) -> Option<BundleInfo> {
    let data = std::fs::read(dir.join("Info.plist")).ok()?;
    let info: plist::Dictionary = plist::from_bytes(&data).ok()?;

    let string = |key: &str| {
        info.get(key)
            .and_then(|value| value.as_string())
            .map(|value| value.to_string())
    };

    Some(BundleInfo {
        name: string("CFBundleName")
            .or_else(|| string("CFBundleDisplayName"))
            .unwrap_or_default(),
        bundle_id: string("CFBundleIdentifier").unwrap_or_default(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn write_info_plist(dir: &Path, bundle_id: &str, name: &str) {
        std::fs::create_dir_all(dir).unwrap();
        let mut info = plist::Dictionary::new();
        info.insert(
            "CFBundleIdentifier".to_string(),
            plist::Value::String(bundle_id.to_string()),
        );
        info.insert(
            "CFBundleName".to_string(),
            plist::Value::String(name.to_string()),
        );
        info.insert(
            "CFBundlePackageType".to_string(),
            plist::Value::String("XPC!".to_string()),
        );
        let mut data = Vec::new();
        plist::to_writer_binary(&mut data, &info).unwrap();
        std::fs::write(dir.join("Info.plist"), data).unwrap();
    }

    #[test]
    fn only_reports_extensions_outside_of_plugins() {
        let temp = std::env::temp_dir().join("fhzipa_orphan_extension_test");
        let _ = std::fs::remove_dir_all(&temp);
        let app = temp.join("Test.app");

        write_info_plist(
            &app.join("PlugIns/Good.appex"),
            "com.example.good",
            "Good",
        );
        write_info_plist(
            &app.join("Extensions/Bad.appex"),
            "com.example.bad",
            "Bad",
        );
        write_info_plist(
            &app.join("PlugIns/Nested.appex/PlugIns/Deep.appex"),
            "com.example.deep",
            "Deep",
        );

        let orphans = find_orphan_extensions(&app);
        let ids: Vec<&str> = orphans.iter().map(|o| o.bundle_id.as_str()).collect();

        assert_eq!(ids.len(), 2, "expected two orphans, got {ids:?}");
        assert!(ids.contains(&"com.example.bad"));
        assert!(ids.contains(&"com.example.deep"));
        assert!(!ids.contains(&"com.example.good"));

        let _ = std::fs::remove_dir_all(&temp);
    }

    #[test]
    fn removes_the_orphans_and_keeps_the_rest() {
        let temp = std::env::temp_dir().join("fhzipa_orphan_extension_removal_test");
        let _ = std::fs::remove_dir_all(&temp);
        let app = temp.join("Test.app");

        write_info_plist(&app.join("PlugIns/Good.appex"), "com.example.good", "Good");
        write_info_plist(&app.join("Extensions/Bad.appex"), "com.example.bad", "Bad");

        let removed = remove_orphan_extensions(&app).unwrap();

        assert_eq!(removed.len(), 1);
        assert!(!app.join("Extensions/Bad.appex").exists());
        assert!(app.join("PlugIns/Good.appex").exists());

        let _ = std::fs::remove_dir_all(&temp);
    }
}
