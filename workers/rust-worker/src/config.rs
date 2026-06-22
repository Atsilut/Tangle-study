use std::env;

use anyhow::{bail, Context, Result};

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
    pub api_base_url: String,
    pub worker_callback_secret: String,
    pub callback_timeout_ms: u64,
    pub callback_connect_timeout_ms: u64,
    pub callback_max_retries: u32,
    pub callback_retry_base_ms: u64,
    pub azure_storage_connection_string: String,
    pub media_container_name: String,
    pub metrics_port: u16,
}

impl Config {
    pub fn from_env() -> Result<Self> {
        Ok(Self {
            redis_url: env_var("REDIS_URL", "redis://127.0.0.1:6379")?,
            stream_prefix: env_var("WORKER_STREAM_PREFIX", "tangle:queue:")?,
            stream_key: env_var("WORKER_STREAM_KEY", "chat.message.created")?,
            consumer_group: env_var("WORKER_CONSUMER_GROUP", "tangle-study-workers")?,
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
            api_base_url: env_var("API_BASE_URL", "http://127.0.0.1:5000")?,
            worker_callback_secret: env_var("WORKER_CALLBACK_SECRET", "")?,
            callback_timeout_ms: env_var_parse("WORKER_CALLBACK_TIMEOUT_MS", 30_000)?,
            callback_connect_timeout_ms: env_var_parse("WORKER_CALLBACK_CONNECT_TIMEOUT_MS", 10_000)?,
            callback_max_retries: env_var_parse("WORKER_CALLBACK_MAX_RETRIES", 3)?,
            callback_retry_base_ms: env_var_parse("WORKER_CALLBACK_RETRY_BASE_MS", 500)?,
            azure_storage_connection_string: env_var("AZURE_STORAGE_CONNECTION_STRING", "")?,
            media_container_name: env_var("MEDIA_CONTAINER_NAME", "tangle-media")?,
            metrics_port: env_var_parse("WORKER_METRICS_PORT", 9090_u16)?,
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

    /// Validates stream key and, when running the consumer, stream-specific required env vars.
    pub fn validate(&self, consumer: bool) -> Result<()> {
        match self.stream_key.as_str() {
            "chat.message.created" => Ok(()),
            "media.uploaded" => {
                if consumer {
                    require_non_empty(
                        &self.azure_storage_connection_string,
                        "AZURE_STORAGE_CONNECTION_STRING",
                    )?;
                    require_non_empty(&self.worker_callback_secret, "WORKER_CALLBACK_SECRET")?;
                    require_non_empty(&self.api_base_url, "API_BASE_URL")?;
                }
                Ok(())
            }
            "location.cluster" => {
                if consumer {
                    require_non_empty(&self.worker_callback_secret, "WORKER_CALLBACK_SECRET")?;
                    require_non_empty(&self.api_base_url, "API_BASE_URL")?;
                }
                Ok(())
            }
            other => bail!("unsupported worker stream key {other}"),
        }
    }
}

fn require_non_empty(value: &str, name: &str) -> Result<()> {
    if value.trim().is_empty() {
        bail!("{name} is not configured");
    }
    Ok(())
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

    fn test_config(stream_key: &str) -> Config {
        Config {
            redis_url: String::new(),
            stream_prefix: "tangle:queue:".to_owned(),
            stream_key: stream_key.to_owned(),
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
            api_base_url: "http://127.0.0.1:5000".to_owned(),
            worker_callback_secret: "secret".to_owned(),
            callback_timeout_ms: 30_000,
            callback_connect_timeout_ms: 10_000,
            callback_max_retries: 3,
            callback_retry_base_ms: 500,
            azure_storage_connection_string: "UseDevelopmentStorage=true".to_owned(),
            media_container_name: "tangle-media".to_owned(),
            metrics_port: 9090,
        }
    }

    #[test]
    fn validate_accepts_chat_consumer_without_media_env() {
        let mut config = test_config("chat.message.created");
        config.azure_storage_connection_string.clear();
        config.worker_callback_secret.clear();
        config.validate(true).unwrap();
    }

    #[test]
    fn validate_requires_media_env_for_media_consumer() {
        let mut config = test_config("media.uploaded");
        config.azure_storage_connection_string.clear();

        let err = config.validate(true).unwrap_err();
        assert!(err.to_string().contains("AZURE_STORAGE_CONNECTION_STRING"));
    }

    #[test]
    fn validate_allows_media_replay_without_media_env() {
        let mut config = test_config("media.uploaded");
        config.azure_storage_connection_string.clear();
        config.worker_callback_secret.clear();
        config.validate(false).unwrap();
    }

    #[test]
    fn validate_allows_location_cluster_consumer_without_azure_env() {
        let mut config = test_config("location.cluster");
        config.azure_storage_connection_string.clear();
        config.validate(true).unwrap();
    }

    #[test]
    fn validate_rejects_unsupported_stream_key() {
        let config = test_config("unknown.stream");
        let err = config.validate(true).unwrap_err();
        assert!(err.to_string().contains("unsupported worker stream key"));
    }

    #[test]
    fn full_stream_key_uses_prefix() {
        let config = test_config("chat.message.created");

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
