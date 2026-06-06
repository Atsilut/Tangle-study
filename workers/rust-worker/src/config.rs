use std::env;

use anyhow::{Context, Result};

/// Worker configuration loaded from environment variables.
#[derive(Debug, Clone)]
pub struct Config {
    pub redis_url: String,
    pub stream_prefix: String,
    pub stream_key: String,
    pub consumer_group: String,
    pub consumer_name: String,
    pub block_ms: u64,
    pub batch_count: usize,
    pub max_attempts: u32,
    pub retry_base_ms: u64,
    pub retry_max_ms: u64,
    pub retry_jitter_pct: f64,
    pub dlq_stream_suffix: String,
    pub replay_count: usize,
    pub replay_dry_run: bool,
    pub replay_delete: bool,
    pub log_json: bool,
}

impl Config {
    pub fn from_env() -> Result<Self> {
        Ok(Self {
            redis_url: env_var("REDIS_URL", "redis://127.0.0.1:6379")?,
            stream_prefix: env_var("WORKER_STREAM_PREFIX", "tangle:queue:")?,
            stream_key: env_var("WORKER_STREAM_KEY", "chat.message.created")?,
            consumer_group: env_var("WORKER_CONSUMER_GROUP", "tangle-workers")?,
            consumer_name: env_var(
                "WORKER_CONSUMER_NAME",
                &format!(
                    "tangle-worker-{}-{}",
                    env::var("HOSTNAME").unwrap_or_else(|_| "unknown".to_owned()),
                    std::process::id()
                ),
            )?,
            block_ms: env_var_parse("WORKER_BLOCK_MS", 5_000)?,
            batch_count: env_var_parse("WORKER_BATCH_COUNT", 10)?,
            max_attempts: env_var_parse("WORKER_MAX_ATTEMPTS", 5)?,
            retry_base_ms: env_var_parse("WORKER_RETRY_BASE_MS", 1_000)?,
            retry_max_ms: env_var_parse("WORKER_RETRY_MAX_MS", 60_000)?,
            retry_jitter_pct: env_var_parse("WORKER_RETRY_JITTER_PCT", 0.1)?,
            dlq_stream_suffix: env_var("WORKER_DLQ_STREAM_SUFFIX", ".dlq")?,
            replay_count: env_var_parse("WORKER_REPLAY_COUNT", 10)?,
            replay_dry_run: env_var_parse("WORKER_REPLAY_DRY_RUN", false)?,
            replay_delete: env_var_parse("WORKER_REPLAY_DELETE", true)?,
            log_json: env_var_parse("WORKER_LOG_JSON", false)?,
        })
    }

    pub fn full_stream_key(&self) -> String {
        if self.stream_prefix.is_empty() {
            return self.stream_key.clone();
        }

        if self.stream_prefix.ends_with(':') {
            format!("{}{}", self.stream_prefix, self.stream_key)
        } else {
            format!("{}:{}", self.stream_prefix, self.stream_key)
        }
    }

    pub fn dlq_stream_key(&self) -> String {
        format!("{}{}", self.full_stream_key(), self.dlq_stream_suffix)
    }
}

fn env_var(key: &str, default: &str) -> Result<String> {
    match env::var(key) {
        Ok(value) if !value.trim().is_empty() => Ok(value.trim().to_owned()),
        Ok(_) => Ok(default.to_owned()),
        Err(env::VarError::NotPresent) => Ok(default.to_owned()),
        Err(err) => Err(err).with_context(|| format!("reading environment variable {key}")),
    }
}

fn env_var_parse<T>(key: &str, default: T) -> Result<T>
where
    T: std::str::FromStr,
    T::Err: std::error::Error + Send + Sync + 'static,
{
    match env::var(key) {
        Ok(value) if value.trim().is_empty() => Ok(default),
        Ok(value) => value
            .trim()
            .parse()
            .with_context(|| format!("parsing environment variable {key}")),
        Err(env::VarError::NotPresent) => Ok(default),
        Err(err) => Err(err).with_context(|| format!("reading environment variable {key}")),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn full_stream_key_uses_prefix() {
        let config = Config {
            redis_url: String::new(),
            stream_prefix: "tangle:queue:".to_owned(),
            stream_key: "chat.message.created".to_owned(),
            consumer_group: String::new(),
            consumer_name: String::new(),
            block_ms: 0,
            batch_count: 0,
            max_attempts: 0,
            retry_base_ms: 1_000,
            retry_max_ms: 60_000,
            retry_jitter_pct: 0.1,
            dlq_stream_suffix: ".dlq".to_owned(),
            replay_count: 10,
            replay_dry_run: false,
            replay_delete: true,
            log_json: false,
        };

        assert_eq!(
            config.full_stream_key(),
            "tangle:queue:chat.message.created"
        );
        assert_eq!(
            config.dlq_stream_key(),
            "tangle:queue:chat.message.created.dlq"
        );
    }
}
