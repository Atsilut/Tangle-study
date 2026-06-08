pub mod chat_message_created;
pub mod media_uploaded;

use anyhow::{bail, Result};
use redis::streams::StreamId;

use crate::config::Config;
use crate::job::{ChatMessageCreatedJob, MediaUploadedJob};
use crate::message;

/// Decode and process a stream entry for the configured worker stream.
pub async fn dispatch_entry(
    config: &Config,
    entry: &StreamId,
    http: &reqwest::Client,
) -> Result<()> {
    match config.stream_key.as_str() {
        "chat.message.created" => {
            let job = message::decode_chat_message_created(&config.stream_key, entry)?;
            dispatch_chat_message_created(&job).await
        }
        "media.uploaded" => {
            let job = message::decode_media_uploaded(&config.stream_key, entry)?;
            dispatch_media_uploaded(&job, config, http).await
        }
        other => bail!("unsupported worker stream key {other}"),
    }
}

/// Process a decoded job payload.
pub async fn dispatch_chat_message_created(job: &ChatMessageCreatedJob) -> Result<()> {
    chat_message_created::handle(job).await
}

pub async fn dispatch_media_uploaded(
    job: &MediaUploadedJob,
    config: &Config,
    http: &reqwest::Client,
) -> Result<()> {
    media_uploaded::handle(job, config, http).await
}
