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

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn media_kind_parse_accepts_video_and_image() {
        assert_eq!(MediaKind::parse("Video").unwrap(), MediaKind::Video);
        assert_eq!(MediaKind::parse("image").unwrap(), MediaKind::Image);
    }

    #[test]
    fn media_kind_parse_rejects_unknown_kind() {
        assert!(MediaKind::parse("audio").is_err());
    }

    #[test]
    fn media_uploaded_job_validate_rejects_non_positive_target() {
        let job = MediaUploadedJob {
            media_asset_id: 1,
            intended_context: "Post".to_owned(),
            kind: "Video".to_owned(),
            mime_type: "video/mp4".to_owned(),
            original_object_key: "raw/1/a.mp4".to_owned(),
            original_size_bytes: 100,
            target_max_bytes: 0,
        };
        assert!(job.validate().is_err());
    }
}
