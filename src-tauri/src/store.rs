//! Integrated IPA catalog.
//!
//! The store reads from several suppliers and returns a single normalized
//! shape, so the interface does not care where an app came from. Everything is
//! fetched live: the catalog is never bundled, so it stays current without
//! shipping a new build.
//!
//! Downloads pick the fastest mirror available. The suppliers host their files
//! on different CDNs with very different per-IP limits, so the same app can
//! be several times faster on one host than on the other.

use std::collections::{HashMap, HashSet};
use std::fs::File;
use std::io::Write;
use std::path::PathBuf;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex, OnceLock};
use std::time::{Duration, Instant};

use serde::de::DeserializeOwned;
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use tauri::{AppHandle, Emitter};

use crate::error::AppError;

const USER_AGENT: &str = "fhzipa-inject/1.0 (+catalog)";

/// How many connections a single download is split across.
const DOWNLOAD_CONNECTIONS: usize = 6;
/// How much is fetched to measure the speed of a mirror.
const PROBE_BYTES: u64 = 512 * 1024;
/// Whether the mirrors are compared before downloading.
const PROBE_ENABLED: bool = true;

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct StoreSource {
    pub id: &'static str,
    pub name: &'static str,
    pub homepage: &'static str,
    /// How the source is read: `wordpress`, `iappstores` or `github`.
    pub kind: &'static str,
    /// The API endpoint, or the repository for the GitHub source.
    pub endpoint: &'static str,
    /// Whether the entries come with a direct link to the `.ipa` file.
    pub direct: bool,
}

pub const SOURCES: &[StoreSource] = &[
    StoreSource {
        id: "ipaomtk",
        name: "IPAOMTK",
        homepage: "https://ipaomtk.com",
        kind: "wordpress",
        endpoint: "https://ipaomtk.com/wp-json/wp/v2",
        direct: true,
    },
    StoreSource {
        id: "iappstores",
        name: "iAppStores",
        homepage: "https://iappstores.com",
        kind: "iappstores",
        endpoint: "https://iappstores.com/api",
        direct: true,
    },
    StoreSource {
        id: "github",
        name: "GitHub",
        homepage: "https://github.com",
        kind: "github",
        endpoint: "",
        direct: true,
    },
    StoreSource {
        id: "ipalibrary",
        name: "iPA Library",
        homepage: "https://ipalibrary.me",
        kind: "wordpress",
        endpoint: "https://ipalibrary.me/wp-json/wp/v2",
        direct: false,
    },
    StoreSource {
        id: "ipajb",
        name: "IPAJB",
        homepage: "https://ipajb.com",
        kind: "wordpress",
        endpoint: "https://ipajb.com/wp-json/wp/v2",
        direct: false,
    },
];

/// The repositories the GitHub source reads.
const GITHUB_REPOS: &[(&str, &str)] = &[
    ("apptesters-org/AppTesters_Repo", "AppTesters"),
    ("Cyosai/Archive-iGameBox-iPA", "iGameBox"),
    ("NeoFreeBird/app", "NeoFreeBird"),
];

/// The apps shown in the featured row: label, search term and the bundle
/// identifier prefix the real app carries.
const FEATURED: &[(&str, &str, &str)] = &[
    ("Spotify", "Spotify", "com.spotify.client"),
    ("YouTube", "YouTube", "com.google.ios.youtube"),
    ("Instagram", "Instagram", "com.burbn.instagram"),
    ("TikTok", "TikTok", "com.zhiliaoapp.musically"),
];

#[derive(Serialize, Deserialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct StoreMirror {
    pub url: String,
    pub name: String,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct StoreItem {
    pub id: String,
    pub source_id: String,
    pub name: String,
    pub publisher: String,
    pub subtitle: String,
    pub description: String,
    pub version: String,
    pub size: String,
    pub mod_info: String,
    pub bundle_id: String,
    pub icon: Option<String>,
    pub rating: Option<f64>,
    pub votes: Option<u64>,
    pub download_url: String,
    pub page_url: String,
    /// Whether `download_url` points straight at the `.ipa` file.
    pub direct: bool,
    pub category: String,
    pub categories: Vec<String>,
    pub updated: String,
    pub screenshots: Vec<String>,
    /// Other hosts that serve the same file, used to pick the fastest one.
    pub mirrors: Vec<StoreMirror>,
    /// The digest of the file, when the source publishes one.
    pub digest: Option<String>,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct StoreCategory {
    pub id: String,
    pub name: String,
    pub count: u64,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct CatalogPage {
    pub items: Vec<StoreItem>,
    pub total: u64,
    pub page: u64,
    pub total_pages: u64,
    pub source_id: String,
}

#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct DownloadProgress {
    /// The catalog item the download belongs to.
    pub item_id: Option<String>,
    pub download_url: String,
    pub received: u64,
    pub total: u64,
    pub done: bool,
    pub path: Option<String>,
    /// The mirror that served the file, when it is not the requested one.
    pub mirror: Option<String>,
    pub speed: f64,
}

/// The outcome of a finished download.
#[derive(Serialize, Clone, Debug)]
#[serde(rename_all = "camelCase")]
pub struct DownloadResult {
    pub item_id: Option<String>,
    pub path: String,
    /// The real size of the file on disk, in bytes.
    pub size: u64,
    /// The mirror the file came from.
    pub mirror: Option<String>,
    /// The measured speed, in megabytes per second.
    pub speed: f64,
}

fn source_by_id(id: &str) -> Option<&'static StoreSource> {
    SOURCES.iter().find(|source| source.id == id)
}

fn client() -> Result<reqwest::Client, String> {
    reqwest::Client::builder()
        .user_agent(USER_AGENT)
        .timeout(Duration::from_secs(45))
        .redirect(reqwest::redirect::Policy::limited(8))
        .build()
        .map_err(|e| e.to_string())
}

/// The client used for the transfers.
fn download_client() -> Result<reqwest::Client, String> {
    reqwest::Client::builder()
        .user_agent(USER_AGENT)
        .timeout(Duration::from_secs(7200))
        .redirect(reqwest::redirect::Policy::limited(8))
        // One connection per range. Over HTTP/2 the ranges share a single
        // connection and its flow control window, and the server stops
        // delivering the bodies.
        .http1_only()
        .build()
        .map_err(|e| e.to_string())
}

async fn fetch_json<T: DeserializeOwned>(url: &str) -> Result<T, String> {
    client()?
        .get(url)
        .send()
        .await
        .map_err(|e| e.to_string())?
        .json::<T>()
        .await
        .map_err(|e| e.to_string())
}

// ------------------------------------------------------------------ //
// WordPress                                                           //
// ------------------------------------------------------------------ //

#[derive(Deserialize, Default)]
struct WpRendered {
    #[serde(default)]
    rendered: String,
}

#[derive(Deserialize)]
struct WpDownload {
    #[serde(default)]
    download_version: Option<String>,
    #[serde(default)]
    download_mod_info: Option<String>,
    #[serde(default)]
    download_size: Option<String>,
    #[serde(default)]
    download_url: Option<String>,
}

#[derive(Deserialize, Clone)]
struct WpScreenshot {
    #[serde(default)]
    ss_url: Option<String>,
}

#[derive(Deserialize)]
struct WpAppDetails {
    #[serde(default)]
    name: Option<String>,
    #[serde(default)]
    publisher: Option<String>,
    #[serde(default)]
    bundle: Option<String>,
    #[serde(default)]
    version: Option<String>,
    #[serde(default)]
    size: Option<String>,
    #[serde(default)]
    rating: Option<String>,
    #[serde(default)]
    total_votes: Option<String>,
    #[serde(default)]
    screenshots: Option<Vec<WpScreenshot>>,
}

#[derive(Deserialize)]
struct WpAppMod {
    #[serde(default)]
    feature: Option<String>,
    #[serde(default)]
    information: Option<String>,
}

#[derive(Deserialize)]
struct WpOgImage {
    #[serde(default)]
    url: Option<String>,
}

#[derive(Deserialize)]
struct WpYoast {
    #[serde(default)]
    description: Option<String>,
    #[serde(default)]
    og_image: Option<Vec<WpOgImage>>,
}

#[derive(Deserialize)]
struct WpMedia {
    #[serde(default)]
    source_url: Option<String>,
}

#[derive(Deserialize)]
struct WpEmbedded {
    #[serde(rename = "wp:featuredmedia", default)]
    featured: Option<Vec<WpMedia>>,
}

#[derive(Deserialize)]
struct WpPost {
    id: u64,
    #[serde(default)]
    date: Option<String>,
    link: String,
    title: WpRendered,
    #[serde(default)]
    excerpt: WpRendered,
    #[serde(default)]
    content: WpRendered,
    #[serde(default)]
    categories: Vec<u64>,
    #[serde(default)]
    downloads: Vec<WpDownload>,
    #[serde(default)]
    app_details: Option<WpAppDetails>,
    #[serde(default)]
    app_mod: Option<WpAppMod>,
    #[serde(default)]
    yoast_head_json: Option<WpYoast>,
    #[serde(rename = "_embedded", default)]
    embedded: Option<WpEmbedded>,
}

#[derive(Deserialize)]
struct WpCategory {
    id: u64,
    name: String,
    #[serde(default)]
    count: u64,
}

/// Decodes the numeric entities WordPress returns, such as "&#8211;".
fn decode_numeric_entities(text: &str) -> String {
    let mut out = String::with_capacity(text.len());
    let bytes = text.as_bytes();
    let mut index = 0usize;

    while index < bytes.len() {
        if bytes[index] == b'&' && index + 2 < bytes.len() && bytes[index + 1] == b'#' {
            // The entity ends at the semicolon.
            let end = text[index..].find(';').map(|offset| index + offset);
            if let Some(end) = end {
                let body = &text[index + 2..end];
                let code = if let Some(hex) = body.strip_prefix('x').or_else(|| body.strip_prefix('X')) {
                    u32::from_str_radix(hex, 16).ok()
                } else {
                    body.parse::<u32>().ok()
                };
                if let Some(code) = code {
                    if let Some(character) = char::from_u32(code) {
                        out.push(character);
                        index = end + 1;
                        continue;
                    }
                }
            }
        }

        // Copies the character as it is, a whole one.
        let mut end = index + 1;
        while end < bytes.len() && (bytes[end] & 0xC0) == 0x80 {
            end += 1;
        }
        out.push_str(&text[index..end]);
        index = end;
    }

    out
}

/// Strips the markup and normalises the entities WordPress returns.
fn plain_text(html: &str) -> String {
    // Only rebuild the string when it actually carries markup, otherwise a
    // plain value would be dropped by the tag split.
    let cleaned = if html.contains('<') {
        html.replace('\n', " ")
            .replace('\r', " ")
            .split('<')
            .filter_map(|chunk| chunk.find('>').map(|end| &chunk[end + 1..]))
            .collect::<Vec<_>>()
            .join(" ")
    } else {
        html.to_string()
    };

    decode_numeric_entities(&cleaned)
        .replace("&amp;", "&")
        .replace("&quot;", "\"")
        .replace("&#039;", "'")
        .replace("&nbsp;", " ")
        .replace("&lt;", "<")
        .replace("&gt;", ">")
        .split_whitespace()
        .collect::<Vec<_>>()
        .join(" ")
        .trim()
        .to_string()
}

/// Keeps only the numbers of a version the source wrote with extra words,
/// such as "YouTube Plus 21.39.4".
fn clean_version(value: &str) -> String {
    let trimmed = value.trim();
    if trimmed.is_empty() {
        return String::new();
    }
    if trimmed
        .chars()
        .all(|c| c.is_ascii_digit() || c == '.' || c == '-' || c == '+')
    {
        return trimmed.to_string();
    }
    let found = version_from_text(trimmed);
    if found.is_empty() {
        trimmed.to_string()
    } else {
        found
    }
}

/// Pulls the version out of a text that names it, such as "Version: 26.32".
fn labelled_version(text: &str) -> String {
    let lower = text.to_lowercase();
    for marker in ["version", "versão", "versao"] {
        if let Some(index) = lower.find(marker) {
            let candidate = version_from_text(&lower[index + marker.len()..]);
            if !candidate.is_empty() {
                return candidate;
            }
        }
    }
    String::new()
}

/// Pulls the version out of a title such as "Athkari IPA v3.8.40 For iOS".
fn version_from_text(text: &str) -> String {
    let bytes = text.as_bytes();
    let mut index = 0usize;

    while index < bytes.len() {
        let mut start = index;

        // The version starts at a digit, optionally preceded by a "v".
        if (bytes[index] == b'v' || bytes[index] == b'V')
            && index + 1 < bytes.len()
            && bytes[index + 1].is_ascii_digit()
        {
            start = index + 1;
        } else if !bytes[index].is_ascii_digit() {
            index += 1;
            continue;
        }

        let mut end = start;
        while end < bytes.len() && (bytes[end].is_ascii_digit() || bytes[end] == b'.') {
            end += 1;
        }
        // Trim the trailing dots.
        let mut last = end;
        while last > start && bytes[last - 1] == b'.' {
            last -= 1;
        }

        let candidate = &text[start..last];
        let valid = candidate.contains('.')
            && candidate
                .chars()
                .all(|c| c.is_ascii_digit() || c == '.')
            && candidate.chars().next().is_some_and(|c| c.is_ascii_digit());

        if valid {
            return candidate.to_string();
        }

        index = if end > index { end } else { index + 1 };
    }

    String::new()
}

/// Picks the modifier out of a title such as "Spotify++ IPA (Premium Unlocked)".
fn mod_from_text(text: &str) -> String {
    let mut found = String::new();
    let mut depth = 0usize;
    let mut current = String::new();

    for character in text.chars() {
        match character {
            '(' => {
                depth += 1;
                if depth == 1 {
                    current.clear();
                }
            }
            ')' => {
                if depth == 1 && !current.trim().is_empty() {
                    found = current.trim().to_string();
                }
                depth = depth.saturating_sub(1);
            }
            _ if depth >= 1 => current.push(character),
            _ => {}
        }
    }

    found
}

/// Removes the suffixes the catalogues append to the app name.
fn clean_app_name(title: &str) -> String {
    let mut name = title.trim().to_string();

    for suffix in [" IPA MOD", " IPA", " MOD"] {
        if let Some(stripped) = name.strip_suffix(suffix) {
            name = stripped.trim().to_string();
            break;
        }
    }

    // "Athkari | أذكاري IPA v3.8.40 For iOS" -> "Athkari | أذكاري"
    if let Some(index) = name.find(" IPA ") {
        name = name[..index].trim().to_string();
    }

    name
}

/// Looks for a size such as "234.98 MB" in a block of text.
fn size_from_text(text: &str) -> String {
    let bytes = text.as_bytes();
    let mut index = 0usize;

    while index < bytes.len() {
        if !bytes[index].is_ascii_digit() {
            index += 1;
            continue;
        }

        let start = index;
        while index < bytes.len()
            && (bytes[index].is_ascii_digit() || bytes[index] == b'.' || bytes[index] == b',')
        {
            index += 1;
        }
        let number = &text[start..index];

        // Skip the space between the number and the unit.
        let mut unit_start = index;
        while unit_start < bytes.len() && bytes[unit_start].is_ascii_whitespace() {
            unit_start += 1;
        }

        for unit in ["MB", "GB", "KB"] {
            if text[unit_start..].starts_with(unit) {
                return format!("{} {}", number, unit);
            }
        }
    }

    String::new()
}

/// A value kept for a while, so the suppliers are not asked twice for the
/// same thing.
struct Cached<T> {
    value: T,
    at: Instant,
}

impl<T: Clone> Cached<T> {
    /// The value, when it is still fresh.
    fn fresh(&self) -> Option<T> {
        if self.at.elapsed() < CACHE_TTL {
            Some(self.value.clone())
        } else {
            None
        }
    }
}

/// How long a supplier answer is kept before it is asked again.
const CACHE_TTL: Duration = Duration::from_secs(600);

fn category_cache() -> &'static Mutex<HashMap<String, Cached<Vec<StoreCategory>>>> {
    static CACHE: OnceLock<Mutex<HashMap<String, Cached<Vec<StoreCategory>>>>> = OnceLock::new();
    CACHE.get_or_init(|| Mutex::new(HashMap::new()))
}

fn featured_cache() -> &'static Mutex<HashMap<String, Cached<Vec<StoreItem>>>> {
    static CACHE: OnceLock<Mutex<HashMap<String, Cached<Vec<StoreItem>>>>> = OnceLock::new();
    CACHE.get_or_init(|| Mutex::new(HashMap::new()))
}

/// The releases of the GitHub repositories. The public API of GitHub allows
/// only a few calls per hour, so they are kept for a while.
fn releases_cache() -> &'static Mutex<Option<Cached<GitHubReleases>>> {
    static CACHE: OnceLock<Mutex<Option<Cached<GitHubReleases>>>> = OnceLock::new();
    CACHE.get_or_init(|| Mutex::new(None))
}

/// The releases of the repositories, with the reason they could not be read.
#[derive(Clone, Default)]
struct GitHubReleases {
    value: Vec<(String, String, GhRelease)>,
    error: Option<String>,
}

async fn wp_categories(source: &StoreSource) -> Result<Vec<StoreCategory>, String> {
    if let Ok(cache) = category_cache().lock() {
        if let Some(cached) = cache.get(source.id).and_then(|cached| cached.fresh()) {
            return Ok(cached);
        }
    }

    let mut raw: Vec<WpCategory> = Vec::new();

    for page in 1..=4 {
        let url = format!(
            "{}/categories?per_page=100&page={page}&hide_empty=true",
            source.endpoint
        );
        let response = client()?
            .get(&url)
            .send()
            .await
            .map_err(|e| e.to_string())?;
        if !response.status().is_success() {
            break;
        }
        let batch: Vec<WpCategory> = response.json().await.map_err(|e| e.to_string())?;
        if batch.is_empty() {
            break;
        }
        raw.extend(batch);
    }

    let minimum = if source.id == "ipaomtk" { 15 } else { 1 };

    let categories: Vec<StoreCategory> = raw
        .into_iter()
        .filter(|category| category.count >= minimum)
        .filter(|category| !is_duplicate_category(&category.name))
        .map(|category| StoreCategory {
            id: format!("{}:{}", source.id, category.id),
            name: plain_text(&category.name),
            count: category.count,
        })
        .collect();

    if let Ok(mut cache) = category_cache().lock() {
        cache.insert(
            source.id.to_string(),
            Cached {
                value: categories.clone(),
                at: Instant::now(),
            },
        );
    }

    Ok(categories)
}

/// The WordPress install carries a lot of imported taxonomies that only
/// duplicate the real ones.
fn is_duplicate_category(name: &str) -> bool {
    let name = name.trim();
    name == "Uncategorized"
        || name.ends_with("Application")
        || (name.contains('_') && name.chars().all(|c| c.is_ascii_uppercase() || c == '_'))
}

async fn wp_posts(
    source: &StoreSource,
    category: Option<&str>,
    search: Option<&str>,
    order_by: &str,
    order: &str,
    page: u64,
    per_page: u64,
) -> Result<(Vec<WpPost>, u64, u64), String> {
    let mut query = format!(
        "{}/posts?per_page={per_page}&page={page}&_embed=1",
        source.endpoint
    );
    query.push_str(&format!("&orderby={order_by}&order={order}"));

    if let Some(category) = category {
        if let Some(id) = category
            .split_once(':')
            .and_then(|(_, id)| id.parse::<u64>().ok())
        {
            query.push_str(&format!("&categories={id}"));
        }
    }

    if let Some(search) = search.filter(|s| !s.trim().is_empty()) {
        query.push_str(&format!("&search={}", urlencode(search)));
    }

    let response = client()?
        .get(&query)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !response.status().is_success() {
        return Err(format!("A fonte respondeu {}.", response.status().as_u16()));
    }

    let total: u64 = response
        .headers()
        .get("x-wp-total")
        .and_then(|value| value.to_str().ok())
        .and_then(|value| value.parse().ok())
        .unwrap_or(0);
    let total_pages: u64 = response
        .headers()
        .get("x-wp-totalpages")
        .and_then(|value| value.to_str().ok())
        .and_then(|value| value.parse().ok())
        .unwrap_or(1);

    let posts: Vec<WpPost> = response.json().await.map_err(|e| e.to_string())?;

    Ok((posts, total, total_pages))
}

fn wp_item(
    source: &StoreSource,
    post: WpPost,
    names: &HashMap<u64, String>,
) -> StoreItem {
    let title = plain_text(&post.title.rendered);
    let download = post.downloads.first();
    let details = post.app_details.as_ref();
    let body = format!("{} {}", post.excerpt.rendered, post.content.rendered);

    // The rich catalogue stores the metadata in its own fields, the other
    // ones only have a title and a description.
    let name = details
        .and_then(|d| d.name.clone())
        .filter(|name| !name.trim().is_empty())
        .unwrap_or_else(|| clean_app_name(&title));

    let version = download
        .and_then(|d| d.download_version.clone())
        .or_else(|| details.and_then(|d| d.version.clone()))
        .map(|value| clean_version(&value))
        .filter(|value| !value.is_empty())
        .or_else(|| {
            let found = version_from_text(&title);
            if found.is_empty() {
                None
            } else {
                Some(found)
            }
        })
        .or_else(|| {
            let found = labelled_version(&plain_text(&body));
            if found.is_empty() {
                None
            } else {
                Some(found)
            }
        })
        .unwrap_or_default();

    let size = download
        .and_then(|d| d.download_size.clone())
        .or_else(|| details.and_then(|d| d.size.clone()))
        .unwrap_or_else(|| size_from_text(&plain_text(&body)));

    let mod_info = post
        .app_mod
        .as_ref()
        .and_then(|m| m.feature.clone().or_else(|| m.information.clone()))
        .or_else(|| download.and_then(|d| d.download_mod_info.clone()))
        .unwrap_or_else(|| mod_from_text(&title));

    let download_url = download
        .and_then(|d| d.download_url.clone())
        .filter(|url| !url.trim().is_empty());

    let icon = post
        .embedded
        .as_ref()
        .and_then(|embedded| embedded.featured.as_ref())
        .and_then(|media| media.first())
        .and_then(|media| media.source_url.clone())
        .or_else(|| {
            post.yoast_head_json
                .as_ref()
                .and_then(|yoast| yoast.og_image.as_ref())
                .and_then(|images| images.first())
                .and_then(|image| image.url.clone())
        });

    let mut category_names: Vec<String> = post
        .categories
        .iter()
        .filter_map(|id| names.get(id).cloned())
        .filter(|name| name != "Uncategorized")
        .collect();
    category_names.dedup();

    let description = post
        .yoast_head_json
        .as_ref()
        .and_then(|yoast| yoast.description.clone())
        .filter(|text| !text.trim().is_empty())
        .unwrap_or_else(|| plain_text(&post.excerpt.rendered));

    StoreItem {
        id: format!("{}:{}", source.id, post.id),
        source_id: source.id.to_string(),
        name,
        publisher: details.and_then(|d| d.publisher.clone()).unwrap_or_default(),
        subtitle: title,
        description,
        version,
        size,
        mod_info,
        bundle_id: details.and_then(|d| d.bundle.clone()).unwrap_or_default(),
        icon,
        rating: details
            .and_then(|d| d.rating.as_ref())
            .and_then(|rating| rating.parse::<f64>().ok()),
        votes: details
            .and_then(|d| d.total_votes.as_ref())
            .and_then(|votes| votes.parse::<u64>().ok()),
        download_url: download_url.clone().unwrap_or_else(|| post.link.clone()),
        page_url: post.link.clone(),
        direct: download_url.is_some(),
        category: category_names.first().cloned().unwrap_or_default(),
        categories: category_names,
        updated: post.date.unwrap_or_default(),
        screenshots: details
            .and_then(|d| d.screenshots.clone())
            .unwrap_or_default()
            .into_iter()
            .filter_map(|shot| shot.ss_url)
            .collect(),
        mirrors: Vec::new(),
        digest: None,
    }
}

async fn wp_names(source: &StoreSource) -> HashMap<u64, String> {
    wp_categories(source)
        .await
        .unwrap_or_default()
        .into_iter()
        .filter_map(|category| {
            category
                .id
                .split_once(':')
                .and_then(|(_, id)| id.parse::<u64>().ok())
                .map(|id| (id, category.name))
        })
        .collect()
}

// ------------------------------------------------------------------ //
// iAppStores                                                         //
// ------------------------------------------------------------------ //

const IAS_SEARCH: &str = "https://iappstores.com/api/search";
const IAS_APPS: &str = "https://iappstores.com/api/apps";

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct IasApp {
    #[serde(default)]
    id: String,
    #[serde(default)]
    source_name: Option<String>,
    #[serde(default)]
    name: String,
    #[serde(default)]
    bundle_identifier: Option<String>,
    #[serde(default)]
    developer_name: Option<String>,
    #[serde(default)]
    subtitle: Option<String>,
    #[serde(default)]
    description: Option<String>,
    #[serde(default)]
    category: Option<String>,
    #[serde(default)]
    icon_url: Option<String>,
    #[serde(default)]
    screenshots: Option<Vec<String>>,
    #[serde(default)]
    latest_version: Option<String>,
    #[serde(default)]
    version_date: Option<String>,
    #[serde(rename = "downloadURL", default)]
    download_url: Option<String>,
    #[serde(default)]
    size: Option<i64>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct IasCategory {
    id: String,
    name: String,
    #[serde(default)]
    app_count: u64,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct IasResponse {
    #[serde(default)]
    apps: Vec<IasApp>,
    #[serde(default)]
    categories: Vec<IasCategory>,
    #[serde(default)]
    pagination: Option<IasPagination>,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct IasPagination {
    #[serde(default)]
    total_items: u64,
    #[serde(default)]
    total_pages: u64,
}

fn human_size(bytes: i64) -> String {
    if bytes <= 0 {
        return String::new();
    }
    let units = ["B", "KB", "MB", "GB"];
    let mut value = bytes as f64;
    let mut unit = 0;
    while value >= 1024.0 && unit < units.len() - 1 {
        value /= 1024.0;
        unit += 1;
    }
    if unit == 0 {
        format!("{value} {}", units[unit])
    } else {
        format!("{value:.1} {}", units[unit])
    }
}

/// The catalogue of the iAppStores, with the categories and their names.
#[derive(Clone)]
struct IasCatalog {
    categories: Vec<StoreCategory>,
    names: HashMap<String, String>,
}

/// The answer of the catalogue carries a whole page of apps, it is kept for a
/// while so it is not fetched again for every card.
fn ias_cache() -> &'static Mutex<Option<Cached<IasCatalog>>> {
    static CACHE: OnceLock<Mutex<Option<Cached<IasCatalog>>>> = OnceLock::new();
    CACHE.get_or_init(|| Mutex::new(None))
}

async fn ias_catalog() -> Result<IasCatalog, String> {
    if let Ok(cache) = ias_cache().lock() {
        if let Some(fresh) = cache.as_ref().and_then(|cached| cached.fresh()) {
            return Ok(fresh);
        }
    }

    let body: IasResponse = fetch_json(IAS_APPS).await?;

    let categories: Vec<StoreCategory> = body
        .categories
        .iter()
        .filter(|category| category.id != "recent")
        .map(|category| StoreCategory {
            id: format!("iappstores:{}", category.id),
            name: category.name.clone(),
            count: category.app_count,
        })
        .collect();
    let names = body
        .categories
        .into_iter()
        .map(|category| (category.id, category.name))
        .collect();

    let catalog = IasCatalog { categories, names };

    if let Ok(mut cache) = ias_cache().lock() {
        *cache = Some(Cached {
            value: catalog.clone(),
            at: Instant::now(),
        });
    }

    Ok(catalog)
}

async fn ias_categories() -> Result<Vec<StoreCategory>, String> {
    Ok(ias_catalog().await?.categories)
}

/// Picks the tweak out of the text the catalogue writes for the app, such as
/// "Tweak: Premium Unlocked".
fn tweak_from_text(text: &str) -> String {
    let lower = text.to_lowercase();
    let Some(index) = lower.find("tweak:") else {
        return String::new();
    };

    let tail = &text[index + "tweak:".len()..];
    // The tweak ends where the next field starts.
    let end = tail
        .find("Version:")
        .or_else(|| tail.find("version:"))
        .or_else(|| tail.find("Additional Information"))
        .or_else(|| tail.find("Min OS"))
        .unwrap_or(tail.len());

    tail[..end]
        .split_whitespace()
        .collect::<Vec<_>>()
        .join(" ")
        .trim()
        .to_string()
}

fn ias_item(app: IasApp, names: &HashMap<String, String>) -> StoreItem {
    let category_id = app.category.clone().unwrap_or_default();
    let category_name = names.get(&category_id).cloned().unwrap_or_default();
    let download = app.download_url.clone().unwrap_or_default();
    let direct = !download.is_empty();

    // The catalogue lists the other providers of the same app, but they
    // serve other builds of it, so they are not used as mirrors.
    let subtitle = app.subtitle.clone().unwrap_or_default();
    let mod_info = {
        let tweak = tweak_from_text(&subtitle);
        if tweak.is_empty() {
            app.source_name.clone().unwrap_or_default()
        } else {
            tweak
        }
    };

    StoreItem {
        id: format!("iappstores:{}", app.id),
        source_id: "iappstores".into(),
        publisher: app.developer_name.clone().unwrap_or_default(),
        subtitle,
        description: app.description.clone().unwrap_or_default(),
        version: app.latest_version.clone().unwrap_or_default(),
        size: human_size(app.size.unwrap_or(0)),
        mod_info,
        bundle_id: app.bundle_identifier.clone().unwrap_or_default(),
        icon: app.icon_url.clone(),
        rating: None,
        votes: None,
        download_url: download.clone(),
        page_url: download,
        direct,
        category: category_name.clone(),
        categories: if category_name.is_empty() {
            Vec::new()
        } else {
            vec![category_name]
        },
        updated: app.version_date.clone().unwrap_or_default(),
        screenshots: app.screenshots.clone().unwrap_or_default(),
        name: app.name.clone(),
        mirrors: Vec::new(),
        digest: None,
    }
}

// ------------------------------------------------------------------ //
// GitHub releases                                                     //
// ------------------------------------------------------------------ //

/// How many releases of each repository are read. The feed brings ten.
const GITHUB_RELEASES_PER_REPO: usize = 10;

/// A release of a repository.
#[derive(Clone)]
struct GhRelease {
    tag: String,
    title: String,
    published: String,
    assets: Vec<GhAsset>,
}

/// A file of a release.
#[derive(Clone)]
struct GhAsset {
    name: String,
    url: String,
    /// The size as the site writes it, such as "213 MB".
    size: String,
    /// The digest of the file, when the release carries one.
    digest: String,
    updated: String,
}

/// Reads the text of a tag of the feed.
fn xml_value(entry: &str, tag: &str) -> Option<String> {
    let start = entry.find(tag)? + tag.len();
    let end = entry[start..].find("</")? + start;
    Some(entry[start..end].trim().to_string())
}

/// Reads the releases of a repository from the feed of the site.
///
/// The public API of GitHub only allows a few calls per hour and the answer
/// is an error once the quota is gone. The feed is a plain page of the site,
/// it has no limit.
fn parse_atom(xml: &str) -> Vec<GhRelease> {
    let mut releases = Vec::new();

    for chunk in xml.split("<entry>").skip(1) {
        let Some(end) = chunk.find("</entry>") else {
            continue;
        };
        let entry = &chunk[..end];

        // The tag is at the end of the identifier.
        let tag = xml_value(entry, "<id>")
            .and_then(|id| id.rsplit('/').next().map(|tag| tag.to_string()))
            .unwrap_or_default();
        if tag.is_empty() {
            continue;
        }

        releases.push(GhRelease {
            tag,
            title: xml_value(entry, "<title>").unwrap_or_default(),
            published: xml_value(entry, "<updated>").unwrap_or_default(),
            assets: Vec::new(),
        });
    }

    releases
}

/// Reads the files of a release from the fragment the site shows.
fn parse_assets(html: &str) -> Vec<GhAsset> {
    let mut assets = Vec::new();

    for chunk in html.split("<li").skip(1) {
        // The link to the file.
        let Some(href) = chunk.find("href=\"/") else {
            continue;
        };
        let rest = &chunk[href + "href=\"".len()..];
        let Some(end) = rest.find('"') else {
            continue;
        };
        let path = &rest[..end];
        if !path.contains("/releases/download/") {
            continue;
        }

        // The name of the file, in the bold text.
        let Some(marker) = chunk.find("text-bold\">") else {
            continue;
        };
        let start = marker + "text-bold\">".len();
        let Some(end) = chunk[start..].find("</span>") else {
            continue;
        };
        let name = chunk[start..start + end].trim().to_string();

        if !name.to_lowercase().ends_with(".ipa") {
            continue;
        }

        // The size, in the span that carries the number.
        let size = chunk
            .find("color-fg-muted text-right")
            .and_then(|at| {
                let tail = &chunk[at..];
                let start = tail.find('>')? + 1;
                let end = tail[start..].find("</span>")?;
                Some(tail[start..start + end].trim().to_string())
            })
            .unwrap_or_default();

        // The digest, when the release carries one.
        let digest = chunk
            .find("sha256:")
            .and_then(|at| {
                let tail = &chunk[at + "sha256:".len()..];
                let end = tail.find(|c: char| !c.is_ascii_hexdigit())?;
                Some(tail[..end].to_string())
            })
            .unwrap_or_default();

        // The moment the file was put in.
        let updated = chunk
            .find("<relative-time datetime=\"")
            .and_then(|at| {
                let tail = &chunk[at + "<relative-time datetime=\"".len()..];
                let end = tail.find('"')?;
                Some(tail[..end].to_string())
            })
            .unwrap_or_default();

        assets.push(GhAsset {
            name,
            url: format!("https://github.com{path}"),
            size,
            digest,
            updated,
        });
    }

    assets
}

/// Reads the name of the app out of the name of the file.
///
/// "VDIT_4.3.1_Subscription_Unlocked@someone.ipa" -> "VDIT"
/// "2-bit_Cowboy_Rides_Again_1.03.ipa" -> "2-bit"
/// "Instagram.v356.1.0.+.Regram.B15.Cracked.ipa" -> "Instagram"
fn github_app_name(file_name: &str) -> String {
    let stem = file_name
        .strip_suffix(".ipa")
        .or_else(|| file_name.strip_suffix(".IPA"))
        .unwrap_or(file_name);

    // The name comes first, the version and the tags after it.
    let mut name = stem
        .split(|c| c == '_' || c == '@')
        .next()
        .unwrap_or(stem)
        .to_string();

    // A name that carries the version inside it is cut at the version.
    if name.len() > 18 {
        for (index, character) in name.char_indices().skip(1) {
            if character == '.' {
                let rest = &name[index + 1..];
                if rest.starts_with('v') || rest.starts_with(|c: char| c.is_ascii_digit()) {
                    name = name[..index].to_string();
                    break;
                }
            }
        }
    }

    name
}

fn github_item(repo: &str, label: &str, release: &GhRelease, asset: &GhAsset) -> StoreItem {
    let name = github_app_name(&asset.name);
    let stem = asset
        .name
        .strip_suffix(".ipa")
        .or_else(|| asset.name.strip_suffix(".IPA"))
        .unwrap_or(&asset.name);
    let version = version_from_text(stem).trim_start_matches('v').to_string();

    StoreItem {
        id: format!("github:{}:{}:{}", repo, release.tag, asset.name),
        source_id: "github".into(),
        name: if name.is_empty() {
            asset.name.clone()
        } else {
            name
        },
        publisher: label.to_string(),
        subtitle: asset.name.clone(),
        description: format!(
            "{} — {}",
            if release.title.is_empty() {
                release.tag.as_str()
            } else {
                release.title.as_str()
            },
            label
        ),
        version,
        size: asset.size.clone(),
        mod_info: String::new(),
        bundle_id: String::new(),
        icon: None,
        rating: None,
        votes: None,
        download_url: asset.url.clone(),
        page_url: format!("https://github.com/{repo}/releases/tag/{}", release.tag),
        direct: true,
        category: label.to_string(),
        categories: vec![label.to_string()],
        updated: if asset.updated.is_empty() {
            release.published.clone()
        } else {
            asset.updated.clone()
        },
        screenshots: Vec::new(),
        mirrors: Vec::new(),
        digest: if asset.digest.is_empty() {
            None
        } else {
            Some(asset.digest.clone())
        },
    }
}

/// Reads a page of the site of GitHub.
async fn fetch_page(url: &str) -> Result<String, String> {
    let response = client()?
        .get(url)
        .send()
        .await
        .map_err(|e| e.to_string())?;

    if !response.status().is_success() {
        return Err(format!("O GitHub respondeu {}.", response.status()));
    }

    response.text().await.map_err(|e| e.to_string())
}

async fn github_releases() -> Result<Vec<(String, String, GhRelease)>, String> {
    if let Ok(cache) = releases_cache().lock() {
        if let Some(fresh) = cache.as_ref().and_then(|cached| cached.fresh()) {
            // The answer is kept even when it failed, so the site is not
            // asked again on every opening of the store.
            return match &fresh.error {
                Some(error) => Err(error.clone()),
                None => Ok(fresh.value),
            };
        }
    }

    let mut all = Vec::new();
    let mut failure: Option<String> = None;

    for (repo, label) in GITHUB_REPOS {
        let feed = format!("https://github.com/{repo}/releases.atom");
        let xml = match fetch_page(&feed).await {
            Ok(xml) => xml,
            Err(e) => {
                tracing::warn!("Falha ao ler o feed de {repo}: {e}");
                failure = Some(e);
                continue;
            }
        };

        // The files of the releases are read at the same time, they are the
        // same number of small pages.
        let mut releases = parse_atom(&xml);
        releases.truncate(GITHUB_RELEASES_PER_REPO);

        let files = futures::future::join_all(releases.iter().map(|release| {
            let url = format!(
                "https://github.com/{repo}/releases/expanded_assets/{}",
                release.tag
            );
            async move { (release.tag.clone(), fetch_page(&url).await) }
        }))
        .await;

        for (release, (tag, page)) in releases.iter_mut().zip(files) {
            if let Ok(html) = page {
                release.assets = parse_assets(&html);
            } else {
                tracing::warn!("Falha ao ler os arquivos de {repo}/{tag}");
            }
        }

        for release in releases {
            all.push(((*repo).to_string(), (*label).to_string(), release));
        }
    }

    // Nothing came back and there was a reason, the user has to know it.
    let error = if all.is_empty() { failure } else { None };

    if let Ok(mut cache) = releases_cache().lock() {
        *cache = Some(Cached {
            value: GitHubReleases {
                value: all.clone(),
                error: error.clone(),
            },
            at: Instant::now(),
        });
    }

    match error {
        Some(error) => Err(error),
        None => Ok(all),
    }
}

async fn github_categories() -> Vec<StoreCategory> {
    let releases = github_releases().await.unwrap_or_default();
    let mut categories = Vec::new();

    for (repo, label) in GITHUB_REPOS {
        let count: usize = releases
            .iter()
            .filter(|(entry_repo, _, _)| entry_repo == repo)
            .map(|(_, _, release)| {
                release
                    .assets
                    .iter()
                    .filter(|asset| asset.name.to_lowercase().ends_with(".ipa"))
                    .count()
            })
            .sum();
        categories.push(StoreCategory {
            id: format!("github:{repo}"),
            name: (*label).to_string(),
            count: count as u64,
        });
    }

    categories
}

// Lookup by name, used for the featured row and to find a mirror       //
// ------------------------------------------------------------------ //

/// The score of a candidate against the wanted app name. Higher is better,
/// zero means no match.
///
/// The match has to end on a word boundary, otherwise "Youtubers Life" would
/// be taken for YouTube.
fn name_score(candidate: &str, wanted: &str) -> u32 {
    let candidate = candidate.trim().to_lowercase();
    let wanted = wanted.trim().to_lowercase();

    if candidate == wanted {
        return 100;
    }

    if let Some(rest) = candidate.strip_prefix(&wanted) {
        return if rest.starts_with(|c: char| c.is_alphanumeric()) {
            0
        } else {
            80
        };
    }

    if candidate.contains(&wanted) {
        return 40;
    }

    0
}

/// Looks an app up by name in a source, returning the best match.
async fn find_app(
    source_id: &str,
    name: &str,
    bundle_prefix: &str,
) -> Result<Option<StoreItem>, String> {
    match source_id {
        "iappstores" => {
            let names = ias_names().await;
            let mut best: Option<(u32, StoreItem)> = None;

            for page in 1..=3u64 {
                let url = format!("{IAS_SEARCH}?q={}&page={page}", urlencode(name));
                let body: IasResponse = fetch_json(&url).await?;
                if body.apps.is_empty() {
                    break;
                }

                for app in body.apps {
                    let bundle = app.bundle_identifier.clone().unwrap_or_default();
                    let mut score = name_score(&app.name, name);

                    // The bundle identifier is the strongest signal.
                    if !bundle_prefix.is_empty()
                        && (bundle == bundle_prefix
                            || bundle.starts_with(&format!("{bundle_prefix}.")))
                    {
                        score = score.max(150);
                    }

                    // An exact name match beats everything else, it tells the
                    // real app apart from its siblings ("YouTube" and
                    // "YouTube Music").
                    if app.name.trim().eq_ignore_ascii_case(name.trim()) {
                        score = score.max(200);
                    }

                    if score == 0 {
                        continue;
                    }
                    if best.as_ref().map(|(best, _)| score > *best).unwrap_or(true) {
                        best = Some((score, ias_item(app, &names)));
                    }
                }

                if best.as_ref().map(|(score, _)| *score >= 200).unwrap_or(false) {
                    break;
                }
            }

            Ok(best.map(|(_, item)| item))
        }
        "github" => {
            let releases = github_releases().await?;
            let mut best: Option<(u32, StoreItem)> = None;

            for (repo, label, release) in releases {
                for asset in &release.assets {
                    if !asset.name.to_lowercase().ends_with(".ipa") {
                        continue;
                    }
                    let item = github_item(&repo, &label, &release, asset);
                    let mut score = name_score(&item.name, name);
                    if score == 0 {
                        score = name_score(&asset.name, name) / 2;
                    }
                    if score == 0 {
                        continue;
                    }
                    if best.as_ref().map(|(best, _)| score > *best).unwrap_or(true) {
                        best = Some((score, item));
                    }
                }
            }

            Ok(best.map(|(_, item)| item))
        }
        _ => {
            // The WordPress search matches the article body, so the results
            // have to be filtered by the title.
            let source = source_by_id(source_id).ok_or("Fonte desconhecida")?;
            let names = wp_names(source).await;
            let mut best: Option<(u32, StoreItem)> = None;

            for page in 1..=3u64 {
                let (posts, _, _) = wp_posts(
                    source,
                    None,
                    Some(name),
                    "relevance",
                    "desc",
                    page,
                    20,
                )
                .await?;
                if posts.is_empty() {
                    break;
                }

                for post in posts {
                    let title = plain_text(&post.title.rendered);
                    let mut score = name_score(&title, name);

                    // The name the source stores for the app is the cleanest
                    // signal, it tells the real app apart from its siblings
                    // ("YouTube" and "YouTube Music").
                    let exact_name = post
                        .app_details
                        .as_ref()
                        .and_then(|details| details.name.clone())
                        .unwrap_or_default();
                    if !exact_name.is_empty()
                        && exact_name.trim().eq_ignore_ascii_case(name.trim())
                    {
                        score = score.max(200);
                    }

                    let file_name = post
                        .downloads
                        .first()
                        .and_then(|download| download.download_url.clone())
                        .and_then(|url| {
                            url.split('/')
                                .next_back()
                                .map(|name| name.to_string())
                        })
                        .unwrap_or_default();
                    if !file_name.is_empty() && name_score(&file_name, name) >= 80 {
                        score = score.max(120);
                    }

                    if score == 0 {
                        continue;
                    }
                    if best.as_ref().map(|(best, _)| score > *best).unwrap_or(true) {
                        let item = wp_item(source, post, &names);
                        best = Some((score, item));
                    }
                }

                if best.as_ref().map(|(score, _)| *score >= 200).unwrap_or(false) {
                    break;
                }
            }

            Ok(best.map(|(_, item)| item))
        }
    }
}

async fn ias_names() -> HashMap<String, String> {
    match ias_catalog().await {
        Ok(catalog) => catalog.names,
        Err(_) => HashMap::new(),
    }
}

// ------------------------------------------------------------------ //
// Public API                                                         //
// ------------------------------------------------------------------ //

fn urlencode(value: &str) -> String {
    let mut out = String::with_capacity(value.len());
    for byte in value.as_bytes() {
        match byte {
            b'A'..=b'Z' | b'a'..=b'z' | b'0'..=b'9' | b'-' | b'_' | b'.' | b'~' => {
                out.push(*byte as char)
            }
            b' ' => out.push('+'),
            _ => out.push_str(&format!("%{byte:02X}")),
        }
    }
    out
}

#[tauri::command]
pub fn store_sources() -> Vec<StoreSource> {
    SOURCES.to_vec()
}

#[tauri::command]
pub async fn store_categories(source_id: String) -> Result<Vec<StoreCategory>, String> {
    if let Ok(cache) = category_cache().lock() {
        if let Some(cached) = cache.get(&source_id).and_then(|cached| cached.fresh()) {
            return Ok(cached);
        }
    }

    let categories = match source_id.as_str() {
        "iappstores" => ias_categories().await?,
        "github" => github_categories().await,
        other => {
            let source = source_by_id(other).ok_or("Fonte desconhecida")?;
            // The WordPress adapter keeps its own answer.
            return wp_categories(source).await;
        }
    };

    if let Ok(mut cache) = category_cache().lock() {
        cache.insert(
            source_id,
            Cached {
                value: categories.clone(),
                at: Instant::now(),
            },
        );
    }

    Ok(categories)
}

#[tauri::command]
pub async fn store_page(
    source_id: String,
    category: Option<String>,
    search: Option<String>,
    order_by: Option<String>,
    order: Option<String>,
    page: u64,
    per_page: Option<u64>,
) -> Result<CatalogPage, String> {
    let source = source_by_id(&source_id).ok_or("Fonte desconhecida")?;
    let per_page = per_page.unwrap_or(24).clamp(1, 60);
    let page = page.max(1);
    let order_by = order_by.unwrap_or_else(|| "date".to_string());
    let order = order.unwrap_or_else(|| "desc".to_string());

    match source.kind {
        "iappstores" => {
            let term = search.filter(|s| !s.trim().is_empty());
            let chosen = category
                .as_ref()
                .map(|category| {
                    category
                        .split_once(':')
                        .map(|(_, id)| id)
                        .unwrap_or(category)
                        .to_string()
                })
                .filter(|id| id != "all" && id != "recent");

            let url = match (term, chosen) {
                // The search only takes a term.
                (Some(term), _) => {
                    format!("{IAS_SEARCH}?q={}&page={page}", urlencode(&term))
                }
                // The listing takes the category.
                (None, Some(category)) => format!(
                    "{IAS_APPS}?category={}&page={page}&pageSize={per_page}",
                    urlencode(&category)
                ),
                (None, None) => format!("{IAS_APPS}?page={page}&pageSize={per_page}"),
            };

            let body: IasResponse = fetch_json(&url).await?;
            let names = ias_names().await;
            let items = body
                .apps
                .into_iter()
                .map(|app| ias_item(app, &names))
                .collect();

            Ok(CatalogPage {
                items,
                total: body.pagination.as_ref().map(|p| p.total_items).unwrap_or(0),
                page,
                total_pages: body.pagination.as_ref().map(|p| p.total_pages).unwrap_or(1),
                source_id: "iappstores".into(),
            })
        }
        "github" => {
            let releases = github_releases().await?;
            let mut items = Vec::new();
            // The same build is published again on the following releases,
            // only the newest one is worth listing.
            let mut seen: HashSet<String> = HashSet::new();

            for (repo, label, release) in &releases {
                if let Some(category) = &category {
                    let wanted = category
                        .split_once(':')
                        .map(|(_, id)| id)
                        .unwrap_or(category);
                    if wanted != repo {
                        continue;
                    }
                }
                for asset in &release.assets {
                    if !asset.name.to_lowercase().ends_with(".ipa") {
                        continue;
                    }
                    if !seen.insert(format!("{repo}/{}", asset.name)) {
                        continue;
                    }
                    items.push(github_item(repo, label, release, asset));
                }
            }

            let total = items.len() as u64;
            let per = per_page as usize;
            let start = ((page - 1) as usize) * per;
            let slice: Vec<StoreItem> = items.into_iter().skip(start).take(per).collect();

            Ok(CatalogPage {
                items: slice,
                total,
                page,
                total_pages: if per == 0 { 1 } else { total.div_ceil(per as u64) },
                source_id: "github".into(),
            })
        }
        _ => {
            // With a search term the relevance order is what the user expects,
            // the newest first only makes sense without one.
            let order_by = if search.as_ref().is_some_and(|s| !s.trim().is_empty())
                && order_by == "date"
            {
                "relevance"
            } else {
                order_by.as_str()
            };

            let (posts, total, total_pages) = wp_posts(
                source,
                category.as_deref(),
                search.as_deref(),
                order_by,
                &order,
                page,
                per_page,
            )
            .await?;

            let names = wp_names(source).await;
            let items = posts
                .into_iter()
                .map(|post| wp_item(source, post, &names))
                .collect();

            Ok(CatalogPage {
                items,
                total,
                page,
                total_pages,
                source_id: source.id.to_string(),
            })
        }
    }
}

/// The apps shown at the top of the store.
#[tauri::command]
pub async fn store_featured(source_id: String) -> Result<Vec<StoreItem>, String> {
    if let Ok(cache) = featured_cache().lock() {
        if let Some(cached) = cache.get(&source_id).and_then(|cached| cached.fresh()) {
            return Ok(cached);
        }
    }

    // The four lookups are independent, so they run at the same time.
    let source = source_id.clone();
    let found = futures::future::join_all(FEATURED.iter().map(|(label, name, bundle)| {
        let source = source.clone();
        async move { (*label, find_app(&source, name, bundle).await) }
    }))
    .await;

    let mut items = Vec::new();
    let mut failure: Option<String> = None;

    for (label, result) in found {
        match result {
            Ok(Some(mut item)) => {
                if item.name.is_empty() {
                    item.name = label.to_string();
                }
                items.push(item);
            }
            Ok(None) => {}
            Err(e) => failure = Some(e),
        }
    }

    // Nothing came back and there was a reason, the user has to know it.
    if items.is_empty() {
        if let Some(failure) = failure {
            return Err(failure);
        }
    }

    if let Ok(mut cache) = featured_cache().lock() {
        cache.insert(
            source_id,
            Cached {
                value: items.clone(),
                at: Instant::now(),
            },
        );
    }

    Ok(items)
}

/// Measures how fast a mirror delivers, in megabytes per second.
async fn probe_speed(url: &str) -> Option<f64> {
    let client = download_client().ok()?;
    let started = Instant::now();

    let mut response = client
        .get(url)
        .header("Range", format!("bytes=0-{}", PROBE_BYTES - 1))
        .send()
        .await
        .ok()?;

    let mut received = 0u64;
    while let Some(chunk) = response.chunk().await.ok()? {
        received += chunk.len() as u64;
        if received >= PROBE_BYTES {
            break;
        }
    }

    let seconds = started.elapsed().as_secs_f64();
    if seconds <= 0.0 || received == 0 {
        return None;
    }

    let speed = (received as f64 / 1_048_576.0) / seconds;
    tracing::info!("Probe: {} -> {:.2} MB/s", url, speed);
    Some(speed)
}

/// The first number of a version, the part that tells which build it is.
fn major_version(version: &str) -> Option<u64> {
    let trimmed = version.trim().trim_start_matches(['v', 'V']);
    let digits: String = trimmed
        .chars()
        .take_while(|c| c.is_ascii_digit() || *c == '.')
        .collect();
    let first = digits.split('.').next()?;
    if first.is_empty() {
        return None;
    }
    first.parse::<u64>().ok()
}

/// Whether the mirror serves the same build as the one the card shows.
fn same_build(left: &str, right: &str) -> bool {
    match (major_version(left), major_version(right)) {
        (Some(a), Some(b)) => a == b,
        // Without both versions the bundle identifier is the only signal.
        _ => true,
    }
}

/// Looks for the same app in the other sources, to use a faster mirror.
async fn find_mirrors(
    source_id: &str,
    name: &str,
    bundle_id: &str,
) -> Vec<(String, String, String)> {
    let mut found = Vec::new();

    for other in SOURCES.iter().filter(|source| source.id != source_id) {
        if !other.direct {
            continue;
        }

        // The GitHub archives are read from the pages of the site, that
        // takes long and would hold the download back.
        if other.id == "github" {
            continue;
        }

        // A mirror that cannot be read is not a reason to stop the download.
        let Ok(Some(item)) = find_app(&other.id, name, bundle_id).await else {
            continue;
        };

        if !bundle_id.is_empty()
            && !item.bundle_id.is_empty()
            && !item.bundle_id.eq_ignore_ascii_case(bundle_id)
        {
            // A different app that happens to share the name.
            continue;
        }

        if item.download_url.is_empty() || !item.direct {
            continue;
        }

        found.push((item.download_url, other.name.to_string(), item.version));
    }

    found
}

/// Downloads an IPA into the temporary folder and reports the progress.
///
/// The file is split across several connections and, when the same app is
/// available on a faster mirror, that mirror is used instead.
#[tauri::command]
pub async fn store_download(
    app: AppHandle,
    download_url: String,
    file_name: String,
    app_name: Option<String>,
    bundle_id: Option<String>,
    app_version: Option<String>,
    item_id: Option<String>,
    source_id: Option<String>,
    mirrors: Option<Vec<StoreMirror>>,
    digest: Option<String>,
) -> Result<DownloadResult, String> {
    if !(download_url.starts_with("https://") || download_url.starts_with("http://")) {
        return Err("Endereco de download invalido.".into());
    }

    // Collect the hosts that could serve this file.
    let mut candidates: Vec<(String, Option<String>, Option<String>)> =
        vec![(download_url.clone(), None, app_version.clone())];

    for mirror in mirrors.unwrap_or_default() {
        if mirror.url.trim().is_empty() || mirror.url == download_url {
            continue;
        }
        if !(mirror.url.starts_with("https://") || mirror.url.starts_with("http://")) {
            continue;
        }
        candidates.push((mirror.url, Some(mirror.name), None));
    }

    if let Some(name) = app_name.as_ref().filter(|n| !n.trim().is_empty()) {
        let source = source_id.clone().unwrap_or_else(|| "ipaomtok".to_string());
        let found = find_mirrors(&source, name, bundle_id.as_deref().unwrap_or("")).await;
        tracing::info!("Mirrors procurados para '{}': {:?}", name, found);
        for (url, mirror_name, version) in found {
            if url == download_url {
                continue;
            }
            // The mirror may serve another build of the app, the version
            // goes along so the user knows which one is coming.
            let label = match (version.is_empty(), app_version.as_deref()) {
                (false, Some(wanted)) if !same_build(&version, wanted) => {
                    format!("{mirror_name} v{version}")
                }
                _ => mirror_name,
            };
            candidates.push((url, Some(label), Some(version)));
        }
    }

    // Measure every host and keep the fastest one. A host that serves the
    // same build is worth a little more, but the speed comes first.
    let mut chosen: Option<(f64, f64, String, Option<String>)> = None;

    if PROBE_ENABLED {
        for (url, mirror, version) in candidates {
            let Some(speed) = probe_speed(&url).await else {
                continue;
            };
            let same = match (version.as_deref(), app_version.as_deref()) {
                (Some(version), Some(wanted)) => same_build(version, wanted),
                // Without the version of the app there is nothing to compare.
                _ => true,
            };
            let score = if same { speed * 3.0 } else { speed };
            if chosen
                .as_ref()
                .map(|(best, _, _, _)| score > *best)
                .unwrap_or(true)
            {
                chosen = Some((score, speed, url, mirror));
            }
        }
    }

    let (url, mirror, speed) = match chosen {
        Some((_, speed, url, mirror)) => (url, mirror, speed),
        // No host answered the probe, fall back to the requested one.
        None => (download_url.clone(), None, 0.0),
    };

    let safe_name: String = file_name
        .chars()
        .map(|c| {
            if c.is_ascii_alphanumeric() || c == '.' || c == '-' || c == '_' {
                c
            } else {
                '_'
            }
        })
        .collect();
    let safe_name = safe_name.trim_matches('.').to_string();
    let safe_name = if safe_name.is_empty() {
        "app.ipa".to_string()
    } else if safe_name.to_lowercase().ends_with(".ipa") {
        safe_name
    } else {
        format!("{safe_name}.ipa")
    };

    let dir: PathBuf = std::env::temp_dir().join("fhzipa-inject-downloads");
    std::fs::create_dir_all(&dir).map_err(|e| e.to_string())?;
    let path = dir.join(&safe_name);

    // The transfer runs on its own connections. Every range gets a file of
    // its own, the parts are joined at the end.
    let total = remote_size(&url)
        .await
        .ok_or("Nao foi possivel saber o tamanho do arquivo.")?;

    let connections = if total < 4 * 1024 * 1024 {
        1
    } else {
        DOWNLOAD_CONNECTIONS
    };
    let chunk_size = (total / connections as u64).max(1);

    tracing::info!(
        "Iniciando download: {} ({} bytes, {} conexoes, mirror {:?})",
        url,
        total,
        connections,
        mirror
    );

    let received = Arc::new(AtomicU64::new(0));
    let last_emit = Arc::new(AtomicU64::new(0));
    let client = Arc::new(download_client()?);
    let mut tasks = Vec::new();

    for index in 0..connections {
        let start = index as u64 * chunk_size;
        if start >= total {
            break;
        }
        let end = if index == connections - 1 {
            total - 1
        } else {
            (start + chunk_size - 1).min(total - 1)
        };

        let url = url.clone();
        let part = part_path(&path, index);
        let app = app.clone();
        let client = client.clone();
        let received = received.clone();
        let last_emit = last_emit.clone();
        let mirror = mirror.clone();
        let item = item_id.clone();

        tasks.push(tokio::spawn(async move {
            let mut response = client
                .get(&url)
                .header("Range", format!("bytes={start}-{end}"))
                .send()
                .await
                .map_err(|e| e.to_string())?;

            if !response.status().is_success() {
                return Err(format!(
                    "Conexao {} respondeu {}.",
                    index,
                    response.status()
                ));
            }

            // Every range writes its own part, the parts are joined once the
            // download is done.
            let mut file = File::create(&part).map_err(|e| e.to_string())?;
            let mut written = 0u64;

            while let Some(chunk) = response.chunk().await.map_err(|e| e.to_string())? {
                std::io::Write::write_all(&mut file, &chunk).map_err(|e| e.to_string())?;
                written += chunk.len() as u64;

                let done = received.fetch_add(chunk.len() as u64, Ordering::Relaxed)
                    + chunk.len() as u64;
                let now = Instant::now();
                let last = last_emit.load(Ordering::Relaxed);
                if now.elapsed().as_millis() as u64 > last {
                    last_emit.store(now.elapsed().as_millis() as u64, Ordering::Relaxed);
                    let _ = app.emit(
                        "store-download-progress",
                        DownloadProgress {
                            item_id: item.clone(),
                            download_url: url.clone(),
                            received: done,
                            total,
                            done: false,
                            path: None,
                            mirror: mirror.clone(),
                            speed,
                        },
                    );
                }
            }

            file.flush().map_err(|e| e.to_string())?;
            Ok::<u64, String>(written)
        }));
    }

    let mut failure: Option<String> = None;
    for task in tasks {
        match task.await {
            Ok(Ok(_)) => {}
            Ok(Err(e)) => failure = Some(e),
            Err(e) => failure = Some(e.to_string()),
        }
    }

    if let Some(error) = failure {
        let _ = std::fs::remove_file(&path);
        for index in 0..connections {
            let _ = std::fs::remove_file(part_path(&path, index));
        }
        return Err(format!("Falha no download: {error}"));
    }

    // Join the parts into the final file.
    let join_target = path.clone();
    let joined = tokio::task::spawn_blocking(move || join_parts(&join_target, connections))
        .await
        .map_err(|e| e.to_string())?;

    if let Err(error) = joined {
        let _ = std::fs::remove_file(&path);
        return Err(format!("Falha ao juntar o arquivo: {error}"));
    }

    // The source may publish the digest of the file. When it does, the
    // download is checked against it, so a mirror that serves another file
    // is caught here.
    if let Some(expected) = digest
        .as_ref()
        .map(|value| value.trim().to_lowercase())
        .filter(|value| value.len() == 64)
    {
        let digest_path = path.clone();
        let calculated = tokio::task::spawn_blocking(move || file_digest(&digest_path))
            .await
            .map_err(|e| e.to_string())?
            .map_err(|e| format!("Falha ao conferir o arquivo: {e}"))?;

        if calculated != expected {
            let _ = std::fs::remove_file(&path);
            return Err(
                "O arquivo baixado não confere com o digest publicado pela fonte."
                    .into(),
            );
        }
    }

    let size = std::fs::metadata(&path).map(|meta| meta.len()).unwrap_or(0);

    tracing::info!(
        "Download concluido: item={:?} size={} mirror={:?}",
        item_id,
        size,
        mirror
    );

    let _ = app.emit(
        "store-download-progress",
        DownloadProgress {
            item_id: item_id.clone(),
            download_url: url.clone(),
            received: size,
            total,
            done: true,
            path: Some(path.to_string_lossy().to_string()),
            mirror: mirror.clone(),
            speed,
        },
    );

    Ok(DownloadResult {
        item_id: item_id.clone(),
        path: path.to_string_lossy().to_string(),
        size,
        mirror,
        speed,
    })
}

/// The path of the part a connection writes into.
fn part_path(path: &std::path::Path, index: usize) -> PathBuf {
    let mut name = path.file_name().unwrap_or_default().to_os_string();
    name.push(format!(".part{index}"));
    path.with_file_name(name)
}

/// The digest of a file, in the hexadecimal form.
fn file_digest(path: &std::path::Path) -> Result<String, String> {
    let mut file = File::open(path).map_err(|e| e.to_string())?;
    let mut hasher = Sha256::new();
    let mut buffer = vec![0u8; 1024 * 1024];

    loop {
        let read = std::io::Read::read(&mut file, &mut buffer).map_err(|e| e.to_string())?;
        if read == 0 {
            break;
        }
        hasher.update(&buffer[..read]);
    }

    Ok(hasher
        .finalize()
        .iter()
        .map(|byte| format!("{byte:02x}"))
        .collect())
}

/// Joins the parts of a parallel download into the final file.
fn join_parts(path: &std::path::Path, connections: usize) -> Result<(), String> {
    let mut target = File::create(path).map_err(|e| e.to_string())?;

    for index in 0..connections {
        let part = part_path(path, index);
        if !part.exists() {
            continue;
        }
        let mut source = File::open(&part).map_err(|e| e.to_string())?;
        std::io::copy(&mut source, &mut target).map_err(|e| e.to_string())?;
        drop(source);
        let _ = std::fs::remove_file(&part);
    }

    target.flush().map_err(|e| e.to_string())?;
    Ok(())
}

/// Reads the total size of a remote file.
async fn remote_size(url: &str) -> Option<u64> {
    let client = download_client().ok()?;

    let head = client
        .head(url)
        .send()
        .await
        .ok()
        .filter(|response| response.status().is_success());

    if let Some(head) = head {
        if let Some(length) = head.content_length() {
            if length > 0 {
                return Some(length);
            }
        }
    }

    let response = client
        .get(url)
        .header("Range", "bytes=0-0")
        .send()
        .await
        .ok()?;

    response
        .headers()
        .get("content-range")
        .and_then(|value| value.to_str().ok())
        .and_then(|value| value.rsplit('/').next())
        .and_then(|value| value.trim().parse::<u64>().ok())
}

/// Removes a file that was downloaded by the store.
#[tauri::command]
pub fn store_remove_file(path: String) -> Result<(), String> {
    let file = PathBuf::from(&path);
    if !file.starts_with(std::env::temp_dir().join("fhzipa-inject-downloads")) {
        return Err("Caminho fora da pasta de downloads.".into());
    }
    std::fs::remove_file(&file).map_err(|e| e.to_string())
}

/// Reads the metadata of a file that was downloaded by the store, so the
/// interface can show the real name and version instead of the file name.
#[tauri::command]
pub fn store_file_info(path: String) -> Result<StoreItem, AppError> {
    let file = PathBuf::from(&path);
    let size = std::fs::metadata(&file).map(|m| m.len()).unwrap_or(0);

    Ok(StoreItem {
        id: path.clone(),
        source_id: "local".into(),
        name: file
            .file_stem()
            .map(|stem| stem.to_string_lossy().to_string())
            .unwrap_or_default(),
        publisher: String::new(),
        subtitle: String::new(),
        description: String::new(),
        version: String::new(),
        size: human_size(size as i64),
        mod_info: String::new(),
        bundle_id: String::new(),
        icon: None,
        rating: None,
        votes: None,
        download_url: String::new(),
        page_url: String::new(),
        direct: false,
        category: String::new(),
        categories: Vec::new(),
        updated: String::new(),
        screenshots: Vec::new(),
        mirrors: Vec::new(),
        digest: None,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn reads_the_version_out_of_a_title() {
        assert_eq!(version_from_text("Athkari IPA v3.8.40 For iOS"), "3.8.40");
        assert_eq!(version_from_text("VDIT_4.3.1_Subscription_Unlocked"), "4.3.1");
        assert_eq!(version_from_text("App 1.2 IPA"), "1.2");
        // A bare number is not a version.
        assert_eq!(version_from_text("Spotify"), "");
        assert_eq!(version_from_text("App for iOS 16"), "");
    }

    #[test]
    fn reads_the_size_out_of_a_text() {
        assert_eq!(size_from_text("Size: 146.22 MB here"), "146.22 MB");
        assert_eq!(size_from_text("about 1,4 GB in total"), "1,4 GB");
        assert_eq!(size_from_text("no size at all"), "");
    }

    #[test]
    fn strips_the_suffixes_from_the_name() {
        assert_eq!(clean_app_name("Spotify IPA v9.1.90"), "Spotify");
        assert_eq!(clean_app_name("Spotify Music MOD"), "Spotify Music");
        assert_eq!(clean_app_name("TrollDecrypt"), "TrollDecrypt");
    }

    #[test]
    fn picks_the_modifier_out_of_the_title() {
        assert_eq!(mod_from_text("Spotify++ IPA (Premium Unlocked)"), "Premium Unlocked");
        assert_eq!(mod_from_text("No parentheses"), "");
    }

    #[test]
    fn scores_the_name_candidates() {
        // The exact name is the best signal.
        assert!(name_score("Spotify", "spotify") > name_score("Spotify Music", "spotify"));
        // A name that merely starts with the term still counts.
        assert!(name_score("Spotify Music", "Spotify") > 0);
        // A different app that has the term inside the word does not.
        assert_eq!(name_score("Youtubers Life", "YouTube"), 0);
        assert_eq!(name_score("Instagram", "TikTok"), 0);
    }

    #[test]
    fn joins_the_parts_of_a_parallel_download() {
        let dir = std::env::temp_dir().join("fhzipa-store-tests");
        std::fs::create_dir_all(&dir).unwrap();
        let path = dir.join("sample.ipa");

        // Two parts with a known content.
        let first = b"primeira parte";
        let second = b"segunda parte";
        std::fs::write(part_path(&path, 0), first).unwrap();
        std::fs::write(part_path(&path, 1), second).unwrap();

        join_parts(&path, 2).unwrap();

        let joined = std::fs::read(&path).unwrap();
        assert_eq!(joined, [first.as_slice(), second.as_slice()].concat());
        // The parts are gone, they were merged.
        assert!(!part_path(&path, 0).exists());
        assert!(!part_path(&path, 1).exists());

        let _ = std::fs::remove_file(&path);
    }

    #[test]
    fn compares_the_builds_by_their_major_version() {
        // The same build, a different release.
        assert!(same_build("21.39.4", "21.41.5"));
        assert!(same_build("v1.2.3", "1.2.3"));
        assert!(same_build("9.1.78", "9.1.92"));
        // Another build of the same app.
        assert!(!same_build("433.0.0", "177.0"));
        // Without both versions there is nothing to compare.
        assert!(same_build("", "1.2.3"));
        assert!(same_build("1.2.3", ""));
    }

    #[test]
    fn picks_the_tweak_out_of_the_catalogue_text() {
        let text = "@riftysIPAlibrary | AA Game  Tweak: Premium Unlocked  Version: 7.1.7  Patched ✓";
        assert_eq!(tweak_from_text(text), "Premium Unlocked");
        // Without the marker there is no tweak.
        assert_eq!(tweak_from_text("Spotify Music"), "");
    }

    #[test]
    fn reads_the_name_out_of_the_file_name() {
        assert_eq!(github_app_name("VDIT_4.3.1_Subscription_Unlocked@x.ipa"), "VDIT");
        assert_eq!(github_app_name("foriOS_13.3_Pro@x.ipa"), "foriOS");
        assert_eq!(github_app_name("2-bit_Cowboy_Rides_Again_1.03.ipa"), "2-bit");
        assert_eq!(github_app_name("4x4_OffRoad_Rally_2_UNLIMITED.ipa"), "4x4");
        assert_eq!(github_app_name("3D.Pool.Game.Plus.ipa"), "3D.Pool.Game.Plus");
        assert_eq!(
            github_app_name("Instagram.v356.1.0.+.Regram.B15.Cracked.ipa"),
            "Instagram"
        );
    }

    #[tokio::test]
    #[ignore = "reaches the suppliers over the network"]
    async fn every_source_lists_apps() {
        for source in SOURCES {
            let page = match store_page(
                source.id.to_string(),
                None,
                None,
                None,
                None,
                1,
                Some(8),
            )
            .await
            {
                Ok(page) => page,
                // The public API of GitHub limits the calls per hour, that is
                // not a fault of the catalogue.
                Err(e) if e.contains("limitou") => {
                    println!("{} foi pulado: {e}", source.name);
                    continue;
                }
                Err(e) => panic!("{} falhou: {e}", source.id),
            };

            assert!(
                !page.items.is_empty(),
                "{} devolveu a lista vazia",
                source.id
            );

            let first = &page.items[0];
            assert!(!first.name.is_empty(), "{} sem nome", source.id);
            assert!(!first.download_url.is_empty(), "{} sem link", source.id);
        }
    }

    #[tokio::test]
    #[ignore = "reaches the suppliers over the network"]
    async fn the_search_finds_the_apps_by_name() {
        for source in SOURCES.iter().filter(|source| source.direct) {
            let page = match store_page(
                source.id.to_string(),
                None,
                Some("Spotify".to_string()),
                None,
                None,
                1,
                Some(10),
            )
            .await
            {
                Ok(page) => page,
                Err(e) if e.contains("limitou") => {
                    println!("{} foi pulado: {e}", source.name);
                    continue;
                }
                Err(e) => panic!("{} falhou: {e}", source.id),
            };

            assert!(
                !page.items.is_empty(),
                "{} nao achou nada para Spotify",
                source.id
            );
        }
    }

    #[tokio::test]
    #[ignore = "reaches the suppliers over the network"]
    async fn the_featured_row_is_filled() {
        for source in SOURCES.iter().filter(|source| source.direct) {
            let items = match store_featured(source.id.to_string()).await {
                Ok(items) => items,
                Err(e) if e.contains("limitou") => {
                    println!("{} foi pulado: {e}", source.name);
                    continue;
                }
                Err(e) => panic!("{} falhou: {e}", source.id),
            };

            // The archives of GitHub rotate the apps every day, the four of
            // the row are not there every time.
            if source.id == "github" {
                if items.is_empty() {
                    println!("GitHub nao tinha nenhum dos apps em destaque hoje");
                }
                continue;
            }

            assert!(
                !items.is_empty(),
                "{} nao achou os apps em destaque",
                source.id
            );
        }
    }

    #[tokio::test]
    #[ignore = "reaches the suppliers over the network"]
    async fn the_github_source_lists_the_files() {
        let page = store_page(
            "github".to_string(),
            None,
            None,
            None,
            None,
            1,
            Some(10),
        )
        .await
        .expect("o GitHub respondeu");

        assert!(!page.items.is_empty(), "o GitHub devolveu a lista vazia");
        assert!(
            page.items.iter().all(|item| item.direct),
            "algum app do GitHub ficou sem o link direto"
        );
        assert!(
            page.items
                .iter()
                .all(|item| item.download_url.starts_with("https://github.com/")),
            "algum app do GitHub ficou com o link errado"
        );
    }

    #[test]
    fn reads_the_assets_of_a_release() {
        let html = r#"<ul>
        <li data-view-component="true" class="Box-row">
        <span class="d-flex flex-items-start">
        <a href="/owner/repo/releases/download/v1/Instagram_1.2.3.ipa" rel="nofollow" class="wb-break-all">
        <span class="text-bold">Instagram_1.2.3.ipa</span>
        </a>
        </span>
        <span data-view-component="true" class="Truncate-text">sha256:47a9bfd3c59ae63cc4aed2c297e27e3ea8b55007f3608638456a26cf7eaac790</span>
        <span class="color-fg-muted text-right ml-2">213 MB</span>
        <relative-time datetime="2026-10-20T19:05:58Z" class="no-wrap"></relative-time>
        </li>
        <li><a href="/owner/repo/releases/download/v1/notes.txt"><span class="text-bold">notes.txt</span></a></li>
        </ul>"#;

        let assets = parse_assets(html);
        assert_eq!(assets.len(), 1, "so o .ipa entra na lista");

        let asset = &assets[0];
        assert_eq!(asset.name, "Instagram_1.2.3.ipa");
        assert_eq!(
            asset.url,
            "https://github.com/owner/repo/releases/download/v1/Instagram_1.2.3.ipa"
        );
        assert_eq!(asset.size, "213 MB");
        assert_eq!(
            asset.digest,
            "47a9bfd3c59ae63cc4aed2c297e27e3ea8b55007f3608638456a26cf7eaac790"
        );
        assert_eq!(asset.updated, "2026-10-20T19:05:58Z");
    }

    #[test]
    fn reads_the_releases_of_a_feed() {
        let xml = r#"<?xml version="1.0" encoding="UTF-8"?>
        <feed>
          <entry>
            <id>tag:github.com,2008:Repository/898610442/21-10-2026</id>
            <updated>2026-10-20T19:06:14Z</updated>
            <title>Release notes from AppTesters_Repo</title>
          </entry>
          <entry>
            <id>tag:github.com,2008:Repository/898610442/20-10-2026</id>
            <updated>2026-10-19T19:06:14Z</updated>
            <title>Older release</title>
          </entry>
        </feed>"#;

        let releases = parse_atom(xml);
        assert_eq!(releases.len(), 2);
        assert_eq!(releases[0].tag, "21-10-2026");
        assert_eq!(releases[0].published, "2026-10-20T19:06:14Z");
        assert_eq!(releases[1].tag, "20-10-2026");
    }
}
