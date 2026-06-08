use anyhow::{bail, Result};
use serde::{Deserialize, Serialize};

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

/// Matches `Api.Global.Queue.ChatMessageCreatedJob` (System.Text.Json web defaults).
#[derive(Debug, Clone, Serialize, Deserialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct ChatMessageCreatedJob {
    pub message_id: i64,
    pub chat_room_id: i64,
    pub sender_user_id: i64,
    pub body: String,
    pub sent_at: String,
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
    pub fn media_kind(&self) -> Result<MediaKind> {
        MediaKind::parse(&self.kind)
    }
}
