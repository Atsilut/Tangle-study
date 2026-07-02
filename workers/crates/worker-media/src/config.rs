use std::env;

use anyhow::{bail, Result};
use serde::{Deserialize, Serialize};
use worker_core::Config;

pub const STREAM_KEY: &str = "media.uploaded";

#[derive(Debug, Clone)]
pub struct MediaConfig {
    pub core: Config,
    pub azure_storage_connection_string: String,
    pub media_container_name: String,
}

impl MediaConfig {
    pub fn from_env() -> Result<Self> {
        let mut core = Config::from_env()?;
        core.stream_key = STREAM_KEY.to_owned();

        Ok(Self {
            core,
            azure_storage_connection_string: env_var("AZURE_STORAGE_CONNECTION_STRING", "")?,
            media_container_name: env_var("MEDIA_CONTAINER_NAME", "tangle-media")?,
        })
    }

    pub fn validate_consumer(&self) -> Result<()> {
        self.core.validate_stream_key(&[STREAM_KEY])?;
        Config::require_non_empty(
            &self.azure_storage_connection_string,
            "AZURE_STORAGE_CONNECTION_STRING",
        )?;
        self.core.validate_callback_env()?;
        Ok(())
    }

    pub fn validate_replay(&self) -> Result<()> {
        self.core.validate_stream_key(&[STREAM_KEY])
    }
}

fn env_var(key: &str, default: &str) -> Result<String> {
    match env::var(key) {
        Ok(value) if !value.trim().is_empty() => Ok(value.trim().to_owned()),
        Ok(_) => Ok(default.to_owned()),
        Err(env::VarError::NotPresent) => Ok(default.to_owned()),
        Err(err) => Err(err.into()),
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum MediaKind {
    Video,
    Image,
}

impl MediaKind {
    pub fn parse(kind: &str) -> Result<Self> {
        match kind.to_ascii_lowercase().as_str() {
            "video" => Ok(Self::Video),
            "image" => Ok(Self::Image),
            other => bail!("unsupported media kind {other}"),
        }
    }
}

/// Matches `Api.Global.Queue.MediaUploadedJob` (System.Text.Json web defaults).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct MediaUploadedJob {
    pub media_asset_id: i64,
    pub intended_context: String,
    pub kind: String,
    pub mime_type: String,
    pub original_object_key: String,
    pub original_size_bytes: i64,
    pub target_max_bytes: i64,
}

impl MediaUploadedJob {
    pub fn validate(&self) -> Result<()> {
        if self.target_max_bytes <= 0 {
            bail!("target_max_bytes must be greater than zero");
        }
        Ok(())
    }

    pub fn media_kind(&self) -> Result<MediaKind> {
        MediaKind::parse(&self.kind)
    }
}
