use anyhow::Context;
use worker_core::runtime;
use worker_media::config::MediaConfig;
use worker_media::stream_handler::MediaStreamHandler;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let media_config = MediaConfig::from_env().context("load worker configuration")?;

    if runtime::is_replay_mode() {
        media_config.validate_replay().context("validate worker configuration")?;
    } else {
        media_config
            .validate_consumer()
            .context("validate worker configuration")?;
    }

    let stream_handler = MediaStreamHandler {
        media: media_config.clone(),
    };
    runtime::bootstrap("worker-media", media_config.core, &stream_handler).await
}
