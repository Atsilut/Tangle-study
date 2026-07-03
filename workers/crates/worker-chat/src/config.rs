use anyhow::Result;
use worker_core::Config;

pub const STREAM_KEY: &str = "chat.message.created";

#[derive(Debug, Clone)]
pub struct ChatConfig {
    pub core: Config,
}

impl ChatConfig {
    pub fn from_env() -> Result<Self> {
        let mut core = Config::from_env()?;
        core.stream.stream_key = STREAM_KEY.to_owned();

        Ok(Self { core })
    }

    pub fn validate_consumer(&self) -> Result<()> {
        self.core.stream.validate_stream_key(&[STREAM_KEY])
    }

    pub fn validate_replay(&self) -> Result<()> {
        self.core.stream.validate_stream_key(&[STREAM_KEY])
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use worker_core::config::{CallbackConfig, StreamConfig};

    fn test_core(stream_key: &str) -> Config {
        Config {
            stream: StreamConfig {
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
                metrics_port: 9090,
            },
            callback: CallbackConfig {
                api_base_url: "http://chat:8080".to_owned(),
                worker_callback_secret: String::new(),
                timeout_ms: 30_000,
                connect_timeout_ms: 10_000,
                max_retries: 3,
                retry_base_ms: 500,
            },
        }
    }

    #[test]
    fn validate_consumer_accepts_chat_stream_key() {
        let config = ChatConfig {
            core: test_core(STREAM_KEY),
        };
        config.validate_consumer().unwrap();
    }

    #[test]
    fn validate_consumer_rejects_other_stream_keys() {
        let config = ChatConfig {
            core: test_core("media.uploaded"),
        };
        assert!(config.validate_consumer().is_err());
    }

    #[test]
    fn validate_replay_accepts_chat_stream_key() {
        let config = ChatConfig {
            core: test_core(STREAM_KEY),
        };
        config.validate_replay().unwrap();
    }
}
