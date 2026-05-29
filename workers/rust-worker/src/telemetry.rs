use anyhow::Result;
use tracing_subscriber::{fmt, EnvFilter};

use crate::config::Config;

pub fn init(config: &Config) -> Result<()> {
    let filter = EnvFilter::try_from_default_env().unwrap_or_else(|_| EnvFilter::new("info"));

    if config.log_json {
        fmt()
            .json()
            .with_env_filter(filter)
            .with_current_span(false)
            .try_init()
            .map_err(|e| anyhow::anyhow!("failed to init json tracing subscriber: {e}"))?;
    } else {
        fmt()
            .with_env_filter(filter)
            .try_init()
            .map_err(|e| anyhow::anyhow!("failed to init tracing subscriber: {e}"))?;
    }

    Ok(())
}
